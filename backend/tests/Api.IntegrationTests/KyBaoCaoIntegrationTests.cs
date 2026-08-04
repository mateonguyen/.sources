using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ThucLuc.Api.IntegrationTests.Infrastructure;

namespace ThucLuc.Api.IntegrationTests;

public sealed class KyBaoCaoIntegrationTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public KyBaoCaoIntegrationTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCurrent_Should_Return_Open_Period()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        var response = await client.GetAsync("/api/v1/ky-bao-cao/current");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("data").GetProperty("kyCode").GetString().Should().Be("2026Q1_NHAN_LUC");
    }

    [Fact]
    public async Task Create_Should_Be_Forbidden_For_User_Without_Permission()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        var response = await client.PostAsJsonAsync("/api/v1/ky-bao-cao", new
        {
            nam = 2026,
            quy = 2,
            ngayBatDau = "2026-04-01",
            ngayKetThuc = "2026-06-30",
            ghiChu = "Q2"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateStatus_Should_Reject_ChuanBi_To_Khoa_And_Allow_Delete_ChuanBi()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("admin", "Admin@123");

        var createResponse = await client.PostAsJsonAsync("/api/v1/ky-bao-cao", new
        {
            mauBaoCaoId = 8001,
            nam = 2027,
            quy = 1,
            ngayBatDau = "2027-01-01",
            ngayKetThuc = "2027-03-31",
            ghiChu = "test transition",
            tenKy = "Ky test ChuanBi"
        });

        createResponse.EnsureSuccessStatusCode();
        using var createDoc = System.Text.Json.JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var kyId = createDoc.RootElement.GetProperty("data").GetProperty("id").GetInt64();

        var lockResponse = await client.PatchAsJsonAsync($"/api/v1/ky-bao-cao/{kyId}/status", new
        {
            trangThai = 4,
            ghiChu = "lock from test"
        });

        lockResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var lockBody = await lockResponse.Content.ReadAsStringAsync();
        lockBody.Should().Contain("KY_INVALID_TRANSITION");

        var deleteResponse = await client.DeleteAsync($"/api/v1/ky-bao-cao/{kyId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateStatus_Should_Allow_DangMo_To_Khoa()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("admin", "Admin@123");

        var response = await client.PatchAsJsonAsync("/api/v1/ky-bao-cao/6001/status", new
        {
            trangThai = 4,
            ghiChu = "lock open period"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetProperty("trangThai").GetInt32().Should().Be(4);
    }
}
