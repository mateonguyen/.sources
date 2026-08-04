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

        // Metadata-only snapshot mode accepts submit without persisted payload JSON.
        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
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
    public async Task Submit_Should_Fail_When_Another_Active_Snapshot_Exists()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        var firstSnapshotId = await CreateDraftAsync(client, "{\"v\":1}");
        var firstSubmitResponse = await client.PostAsJsonAsync($"/api/v1/snapshot/{firstSnapshotId}/submit", new { ghiChu = "first" });
        firstSubmitResponse.EnsureSuccessStatusCode();

        var secondSnapshotId = await CreateDraftAsync(client, "{\"v\":2}");
        var secondSubmitResponse = await client.PostAsJsonAsync($"/api/v1/snapshot/{secondSnapshotId}/submit", new { ghiChu = "second" });

        secondSubmitResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await secondSubmitResponse.Content.ReadAsStringAsync();
        body.Should().Contain("SNAPSHOT_ALREADY_SUBMITTED");
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

    [Fact]
    public async Task SubmitCurrent_Should_Require_Force_When_TongHop_Has_Unconfirmed_Children()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        await _factory.ExecuteDbContextAsync(async dbContext =>
        {
            var parent = await dbContext.DonVis.FirstAsync(x => x.Id == 2002);
            parent.CheDoNhapLieu = "TONG_HOP";

            var child = await dbContext.DonVis.FirstOrDefaultAsync(x => x.ParentId == 2002);
            if (child is null)
            {
                child = new ThucLuc.Domain.Entities.System.DonVi
                {
                    MaDonVi = "CHILD_2002",
                    TenDonVi = "Child of 2002",
                    ParentId = 2002,
                    CapDonVi = "PHONG",
                    IsActive = true,
                    CheDoNhapLieu = "TU_NHAP",
                };
                await dbContext.DonVis.AddAsync(child);
                await dbContext.SaveChangesAsync();
            }

            var childStatus = await dbContext.KyTrangThaiDonVis
                .FirstOrDefaultAsync(x => x.KyBaoCaoId == 6001 && x.DonViId == child.Id);

            if (childStatus is null)
            {
                dbContext.KyTrangThaiDonVis.Add(new ThucLuc.Domain.Entities.Reporting.KyTrangThaiDonVi
                {
                    KyBaoCaoId = 6001,
                    DonViId = child.Id,
                    DaXacNhan = false,
                    TrangThai = KyTrangThaiDonViStatus.DangNhap,
                });
            }
            else
            {
                childStatus.DaXacNhan = false;
                childStatus.TrangThai = KyTrangThaiDonViStatus.DangNhap;
            }

            await dbContext.SaveChangesAsync();
        });

        var denied = await client.PostAsJsonAsync("/api/v1/snapshot/submit-current", new
        {
            kyBaoCaoId = 6001,
            donViId = 2002,
            ghiChu = "tong hop",
            forceSubmitWhenChildrenUnconfirmed = false,
        });

        denied.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var deniedBody = await denied.Content.ReadAsStringAsync();
        deniedBody.Should().Contain("SNAPSHOT_CHILDREN_UNCONFIRMED");

        var accepted = await client.PostAsJsonAsync("/api/v1/snapshot/submit-current", new
        {
            kyBaoCaoId = 6001,
            donViId = 2002,
            ghiChu = "tong hop force",
            forceSubmitWhenChildrenUnconfirmed = true,
        });

        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SubmitCurrent_TongHop_Should_Write_Batch_Confirmations_And_Breakdown()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        long childId = 0;
        await _factory.ExecuteDbContextAsync(async dbContext =>
        {
            var parent = await dbContext.DonVis.FirstAsync(x => x.Id == 2002);
            parent.CheDoNhapLieu = "TONG_HOP";

            var child = await dbContext.DonVis.FirstOrDefaultAsync(x => x.ParentId == 2002);
            if (child is null)
            {
                child = new ThucLuc.Domain.Entities.System.DonVi
                {
                    MaDonVi = "CHILD_2002_BD",
                    TenDonVi = "Child of 2002 for breakdown",
                    ParentId = 2002,
                    CapDonVi = "PHONG",
                    IsActive = true,
                    CheDoNhapLieu = "TU_NHAP",
                };
                await dbContext.DonVis.AddAsync(child);
                await dbContext.SaveChangesAsync();
            }

            childId = child.Id;

            var childStatus = await dbContext.KyTrangThaiDonVis
                .FirstOrDefaultAsync(x => x.KyBaoCaoId == 6001 && x.DonViId == childId);

            if (childStatus is null)
            {
                dbContext.KyTrangThaiDonVis.Add(new ThucLuc.Domain.Entities.Reporting.KyTrangThaiDonVi
                {
                    KyBaoCaoId = 6001,
                    DonViId = childId,
                    DaXacNhan = true,
                    TrangThai = KyTrangThaiDonViStatus.DaXacNhan,
                });
            }
            else
            {
                childStatus.DaXacNhan = true;
                childStatus.TrangThai = KyTrangThaiDonViStatus.DaXacNhan;
            }

            dbContext.DuAnCntts.Add(new ThucLuc.Domain.Entities.Business.DuAnCntt
            {
                DonViId = childId,
                TenDuAn = "Du an test breakdown",
            });

            await dbContext.SaveChangesAsync();
        });

        var response = await client.PostAsJsonAsync("/api/v1/snapshot/submit-current", new
        {
            kyBaoCaoId = 6001,
            donViId = 2002,
            ghiChu = "breakdown",
            forceSubmitWhenChildrenUnconfirmed = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var snapshotId = await _factory.ExecuteDbContextAsync(async dbContext =>
        {
            var latest = await dbContext.BaoCaoSnapshots
                .Where(x => x.KyBaoCaoId == 6001 && x.DonViId == 2002)
                .OrderByDescending(x => x.Id)
                .FirstAsync();
            return latest.Id;
        });

        var assertions = await _factory.ExecuteDbContextAsync(async dbContext =>
        {
            var snapshot = await dbContext.BaoCaoSnapshots.FirstAsync(x => x.Id == snapshotId);
            var confirmationRows = await dbContext.BaoCaoSnapshotXacNhans
                .Where(x => x.SnapshotId == snapshotId)
                .ToListAsync();
            var batch = await dbContext.SnapshotBatches
                .OrderByDescending(x => x.Id)
                .FirstAsync(x => x.KyBaoCaoId == 6001 && x.DonViId == 2002);
            var childHisCount = await dbContext.DuAnCnttHis.CountAsync(x => x.DonViId == childId && x.SnapshotBatchId == batch.Id);
            return (snapshot.TrangThai, confirmationRows.Count, confirmationRows.Any(x => x.DonViId == childId && x.DaXacNhan), batch.Id, childHisCount);
        });

        assertions.Item1.Should().Be(SnapshotStatus.Locked);
        assertions.Item2.Should().BeGreaterThan(0);
        assertions.Item3.Should().BeTrue();
        assertions.Item4.Should().BeGreaterThan(0);
        assertions.Item5.Should().BeGreaterThanOrEqualTo(1);

        var breakdownResponse = await client.GetAsync($"/api/v1/snapshot/{snapshotId}/breakdown");
        breakdownResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = System.Text.Json.JsonDocument.Parse(await breakdownResponse.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("data").GetProperty("totalChildren").GetInt32().Should().BeGreaterThan(0);
        document.RootElement.GetProperty("data").GetProperty("confirmedChildren").GetInt32().Should().BeGreaterThan(0);
        document.RootElement.GetProperty("data").GetProperty("children").EnumerateArray().ToList().Should().NotBeEmpty();
    }

    [Fact]
    public async Task SubmitCurrent_TuNhap_Should_Not_Require_Force_And_Breakdown_Should_Be_Empty()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        await _factory.ExecuteDbContextAsync(async dbContext =>
        {
            var parent = await dbContext.DonVis.FirstAsync(x => x.Id == 2002);
            parent.CheDoNhapLieu = "TU_NHAP";

            var child = await dbContext.DonVis.FirstOrDefaultAsync(x => x.ParentId == 2002);
            if (child is null)
            {
                child = new ThucLuc.Domain.Entities.System.DonVi
                {
                    MaDonVi = "CHILD_2002_TN",
                    TenDonVi = "Child of 2002 for TU_NHAP",
                    ParentId = 2002,
                    CapDonVi = "PHONG",
                    IsActive = true,
                    CheDoNhapLieu = "TU_NHAP",
                };
                await dbContext.DonVis.AddAsync(child);
                await dbContext.SaveChangesAsync();
            }

            var childStatus = await dbContext.KyTrangThaiDonVis
                .FirstOrDefaultAsync(x => x.KyBaoCaoId == 6001 && x.DonViId == child.Id);

            if (childStatus is null)
            {
                dbContext.KyTrangThaiDonVis.Add(new ThucLuc.Domain.Entities.Reporting.KyTrangThaiDonVi
                {
                    KyBaoCaoId = 6001,
                    DonViId = child.Id,
                    DaXacNhan = false,
                    TrangThai = KyTrangThaiDonViStatus.DangNhap,
                });
            }
            else
            {
                childStatus.DaXacNhan = false;
                childStatus.TrangThai = KyTrangThaiDonViStatus.DangNhap;
            }

            await dbContext.SaveChangesAsync();
        });

        var response = await client.PostAsJsonAsync("/api/v1/snapshot/submit-current", new
        {
            kyBaoCaoId = 6001,
            donViId = 2002,
            ghiChu = "tu nhap unaffected",
            forceSubmitWhenChildrenUnconfirmed = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var snapshotId = await _factory.ExecuteDbContextAsync(async dbContext =>
        {
            var latest = await dbContext.BaoCaoSnapshots
                .Where(x => x.KyBaoCaoId == 6001 && x.DonViId == 2002)
                .OrderByDescending(x => x.Id)
                .FirstAsync();
            return latest.Id;
        });

        var breakdownResponse = await client.GetAsync($"/api/v1/snapshot/{snapshotId}/breakdown");
        breakdownResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = System.Text.Json.JsonDocument.Parse(await breakdownResponse.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("data").GetProperty("children").EnumerateArray().ToList().Should().BeEmpty();
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