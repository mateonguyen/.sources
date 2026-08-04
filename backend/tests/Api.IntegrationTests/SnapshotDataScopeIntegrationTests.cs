using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ThucLuc.Api.IntegrationTests.Infrastructure;
using ThucLuc.Domain.Entities.System;

namespace ThucLuc.Api.IntegrationTests;

public sealed class SnapshotDataScopeIntegrationTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public SnapshotDataScopeIntegrationTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task H05_Viewer_Should_See_All_Snapshots_In_Ky()
    {
        await _factory.ResetDataAsync();
        var setup = await SeedScopeDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("h05.viewer", "H05Viewer@123");

        var response = await client.GetAsync("/api/v1/snapshot?kyBaoCaoId=6001");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var donViIds = await ReadSnapshotDonViIdsAsync(response);
        donViIds.Should().Contain([2002L, 2003L, 2103L]);
    }

    [Fact]
    public async Task LanhDao_Tinh_Should_Only_See_Own_And_Child_Units()
    {
        await _factory.ResetDataAsync();
        var setup = await SeedScopeDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("lanhdao.tinh", "LanhDaoTinh@123");

        var response = await client.GetAsync("/api/v1/snapshot?kyBaoCaoId=6001");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var donViIds = await ReadSnapshotDonViIdsAsync(response);
        donViIds.Should().Contain([2003L, 2103L]);
        donViIds.Should().NotContain(2002L);
    }

    [Fact]
    public async Task LanhDao_Tinh_Should_Not_Access_HaNoi_Snapshot_Detail_And_Pdf()
    {
        await _factory.ResetDataAsync();
        var setup = await SeedScopeDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("lanhdao.tinh", "LanhDaoTinh@123");

        var detailResponse = await client.GetAsync($"/api/v1/snapshot/{setup.HaNoiSnapshotId}");
        var pdfResponse = await client.GetAsync($"/api/v1/snapshot/{setup.HaNoiSnapshotId}/pdf");

        detailResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        pdfResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Viewer_User_Should_Be_Scoped_To_CaDaNang_And_Children()
    {
        await _factory.ResetDataAsync();
        var setup = await SeedScopeDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("viewer.user", "ViewerUser@123");

        var listResponse = await client.GetAsync("/api/v1/snapshot?kyBaoCaoId=6001");
        var pdfResponse = await client.GetAsync($"/api/v1/snapshot/{setup.HaNoiSnapshotId}/pdf");

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var donViIds = await ReadSnapshotDonViIdsAsync(listResponse);
        donViIds.Should().Contain([2003L, 2103L]);
        donViIds.Should().NotContain(2002L);
        pdfResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<(long HaNoiSnapshotId, long DaNangSnapshotId, long ChildSnapshotId)> SeedScopeDataAsync()
    {
        using var adminClient = await _factory.CreateAuthorizedClientAsync("admin", "Admin@123");

        await _factory.ExecuteDbContextAsync(async dbContext =>
        {
            var childExists = await dbContext.DonVis.FindAsync(2103L);
            if (childExists is null)
            {
                dbContext.DonVis.Add(new DonVi
                {
                    Id = 2103,
                    MaDonVi = "CA_DN_XA",
                    TenDonVi = "CA Da Nang - Xa E2E",
                    ParentId = 2003,
                    CapDonVi = "XA",
                    IsActive = true,
                    CheDoNhapLieu = "TU_NHAP"
                });

                await dbContext.SaveChangesAsync();
            }
        });

        var userCreateResponse = await adminClient.PostAsJsonAsync("/api/v1/users", new
        {
            username = "lanhdao.tinh",
            password = "LanhDaoTinh@123",
            hoTen = "Lanh dao tinh",
            email = "lanhdao.tinh@thuc-luc.local",
            soDienThoai = "0900999000",
            donViId = 2003,
            roleIds = new[] { 4006L }
        });

        userCreateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var haNoiSnapshotId = await CreateLockedSnapshotAsync(adminClient, 2002);
        var daNangSnapshotId = await CreateLockedSnapshotAsync(adminClient, 2003);
        var childSnapshotId = await CreateLockedSnapshotAsync(adminClient, 2103);

        return (haNoiSnapshotId, daNangSnapshotId, childSnapshotId);
    }

    private static async Task<long> CreateLockedSnapshotAsync(HttpClient client, long donViId)
    {
        var createResponse = await client.PostAsJsonAsync("/api/v1/snapshot/create-draft", new
        {
            kyBaoCaoId = 6001,
            donViId,
            snapshotJson = "{\"scope\":true}"
        });

        createResponse.EnsureSuccessStatusCode();
        using var createDocument = System.Text.Json.JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var snapshotId = createDocument.RootElement.GetProperty("data").GetProperty("id").GetInt64();

        var submitResponse = await client.PostAsJsonAsync($"/api/v1/snapshot/{snapshotId}/submit", new
        {
            ghiChu = "seed scope"
        });

        submitResponse.EnsureSuccessStatusCode();
        return snapshotId;
    }

    private static async Task<IReadOnlyCollection<long>> ReadSnapshotDonViIdsAsync(HttpResponseMessage response)
    {
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement
            .GetProperty("data")
            .EnumerateArray()
            .Select(x => x.GetProperty("donViId").GetInt64())
            .ToList();
    }
}
