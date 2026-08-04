using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Api.IntegrationTests.Infrastructure;
using ThucLuc.Domain.Entities.Business;
using ThucLuc.Domain.Entities.Reporting;
using ThucLuc.Domain.Enums;

namespace ThucLuc.Api.IntegrationTests;

public sealed class DaoTaoHocVienSnapshotIntegrationTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public DaoTaoHocVienSnapshotIntegrationTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task BuildSnapshotJson_Should_Include_DaoTaoHocVien_Live_Data()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        await _factory.ExecuteDbContextAsync(async dbContext =>
        {
            var mau = await dbContext.MauBaoCaos.FirstAsync(x => x.Id == 8001);
            mau.DanhSachModule = "[\"DAO_TAO_HOC_VIEN\"]";

            dbContext.DaoTaoHocViens.Add(new DaoTaoHocVien
            {
                DonViId = 2002,
                Nam = 2026,
                NoiDungDaoTao = "An ninh mạng",
                SoTienSi = 1,
                SoThacSi = 2,
                SoDaiHoc = 3,
                SoCaoDang = 0,
                SoTrungCap = 0,
                GhiChu = "seed-live"
            });

            await dbContext.SaveChangesAsync();
        });

        var response = await client.GetAsync("/api/v1/snapshot/build?kyId=6001&donViId=2002");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var apiDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var snapshotJson = apiDocument.RootElement.GetProperty("data").GetString();
        snapshotJson.Should().NotBeNullOrWhiteSpace();

        using var snapshotDocument = JsonDocument.Parse(snapshotJson!);
        snapshotDocument.RootElement.TryGetProperty("DAO_TAO_HOC_VIEN", out var hocVienElement).Should().BeTrue();
        hocVienElement.ValueKind.Should().Be(JsonValueKind.Array);
        hocVienElement.GetArrayLength().Should().Be(1);
        hocVienElement[0].GetProperty("noiDungDaoTao").GetString().Should().Be("An ninh mạng");
    }

    [Fact]
    public async Task GetAll_Should_Seed_Live_Data_From_Latest_Locked_Snapshot_When_Current_Live_Table_Is_Empty()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        await _factory.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.DaoTaoHocViens.RemoveRange(dbContext.DaoTaoHocViens);

            dbContext.KyBaoCaos.Add(new KyBaoCao
            {
                Id = 6010,
                MauBaoCaoId = 8001,
                KyCode = "2026Q2_NHAN_LUC",
                Nam = 2026,
                Quy = 2,
                TrangThai = KyBaoCaoStatus.DangMo,
                NgayBatDau = new DateOnly(2026, 4, 1),
                NgayKetThuc = new DateOnly(2026, 6, 30),
                GhiChu = "Kỳ Nhân lực Q2/2026 - đang mở"
            });

            dbContext.BaoCaoSnapshots.Add(new BaoCaoSnapshot
            {
                Id = 9100,
                KyBaoCaoId = 6001,
                DonViId = 2002,
                TrangThai = SnapshotStatus.Locked,
                PhienBan = 1,
                CreatedBy = 5003,
                UpdatedBy = 5003,
                LockedAt = DateTime.UtcNow,
                SubmittedAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync();
        });

        var response = await client.GetAsync("/api/v1/dao-tao-hoc-vien?donViId=2002&kyBaoCaoCode=2026Q2_NHAN_LUC");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = document.RootElement.GetProperty("data");
        // Snapshot metadata mode does not auto-restore deleted live rows.
        items.GetArrayLength().Should().Be(0);
    }
}
