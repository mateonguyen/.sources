using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Api.IntegrationTests.Infrastructure;
using ThucLuc.Domain.Enums;

namespace ThucLuc.Api.IntegrationTests;

public sealed class SnapshotSubmitIntegrationTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public SnapshotSubmitIntegrationTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Submit_Should_Succeed_With_Valid_Payload()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        var snapshotId = await CreateDraftAsync(client, "{\"nhanLuc\":10}");
        var submitResponse = await client.PostAsJsonAsync($"/api/v1/snapshot/{snapshotId}/submit", new { ghiChu = "ok" });

        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = System.Text.Json.JsonDocument.Parse(await submitResponse.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("data").GetProperty("trangThai").GetInt32().Should().Be((int)SnapshotStatus.Locked);
    }

    [Fact]
    public async Task Submit_Should_Fail_When_Snapshot_Data_Is_Empty_Object()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        var snapshotId = await CreateDraftAsync(client, "{}");
        var submitResponse = await client.PostAsJsonAsync($"/api/v1/snapshot/{snapshotId}/submit", new { ghiChu = "invalid" });

        submitResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Submit_Should_Fail_When_Report_Period_Is_Not_Open()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        var snapshotId = await CreateDraftAsync(client, "{\"v\":1}");

        await _factory.ExecuteDbContextAsync(async dbContext =>
        {
            var ky = await dbContext.KyBaoCaos.FirstAsync(x => x.Id == 6001);
            ky.TrangThai = KyBaoCaoStatus.DaDong;
            await dbContext.SaveChangesAsync();
        });

        var submitResponse = await client.PostAsJsonAsync($"/api/v1/snapshot/{snapshotId}/submit", new { ghiChu = "closed" });

        submitResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Submit_Should_Update_KyTrangThaiDonVi_And_Write_Audit_Log()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        var snapshotId = await CreateDraftAsync(client, "{\"v\":1}");
        var submitResponse = await client.PostAsJsonAsync($"/api/v1/snapshot/{snapshotId}/submit", new { ghiChu = "audit" });
        submitResponse.EnsureSuccessStatusCode();

        var assertions = await _factory.ExecuteDbContextAsync(async dbContext =>
        {
            var status = await dbContext.KyTrangThaiDonVis.FirstAsync(x => x.KyBaoCaoId == 6001 && x.DonViId == 2002);
            var hasAudit = await dbContext.SystemLogs.AnyAsync(x => x.ActionType == AuditActionType.Submit && x.RecordId == snapshotId);
            return (status.TrangThai, hasAudit);
        });

        assertions.TrangThai.Should().Be(KyTrangThaiDonViStatus.DaNop);
        assertions.hasAudit.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitCurrent_Should_Create_Immutable_Snapshot_From_Current_Live_Data()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        await _factory.ExecuteDbContextAsync(async dbContext =>
        {
            var mau = await dbContext.MauBaoCaos.FirstAsync(x => x.Id == 8001);
            mau.DanhSachModule = "[\"DAO_TAO_HOC_VIEN\"]";
            await dbContext.SaveChangesAsync();
        });

        var response = await client.PostAsJsonAsync("/api/v1/snapshot/submit-current", new
        {
            kyBaoCaoId = 6001,
            donViId = 2002,
            ghiChu = "submit current"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("data").GetProperty("trangThai").GetInt32().Should().Be((int)SnapshotStatus.Locked);
        document.RootElement.GetProperty("data").GetProperty("snapshotJson").GetString().Should().Contain("DAO_TAO_HOC_VIEN");
    }

    [Fact]
    public async Task Delete_Should_Cancel_Submitted_Snapshot_And_Allow_Submit_Again()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        var snapshotId = await CreateDraftAsync(client, "{\"v\":1}");
        var submitResponse = await client.PostAsJsonAsync($"/api/v1/snapshot/{snapshotId}/submit", new { ghiChu = "cancel" });
        submitResponse.EnsureSuccessStatusCode();

        var deleteResponse = await client.DeleteAsync($"/api/v1/snapshot/{snapshotId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var assertions = await _factory.ExecuteDbContextAsync(async dbContext =>
        {
            var snapshot = await dbContext.BaoCaoSnapshots.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == snapshotId);
            var status = await dbContext.KyTrangThaiDonVis.FirstAsync(x => x.KyBaoCaoId == 6001 && x.DonViId == 2002);
            return (DeletedAt: snapshot?.DeletedAt, SnapshotStatus: snapshot?.TrangThai, DonViStatus: status.TrangThai);
        });

        assertions.DeletedAt.Should().NotBeNull();
        assertions.SnapshotStatus.Should().Be(SnapshotStatus.Superseded);
        assertions.DonViStatus.Should().Be(KyTrangThaiDonViStatus.ChuaNhap);
    }

    private static async Task<long> CreateDraftAsync(HttpClient client, string snapshotJson)
    {
        var createResponse = await client.PostAsJsonAsync("/api/v1/snapshot/create-draft", new
        {
            kyBaoCaoId = 6001,
            donViId = 2002,
            snapshotJson
        });

        createResponse.EnsureSuccessStatusCode();
        using var document = System.Text.Json.JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").GetProperty("id").GetInt64();
    }
}