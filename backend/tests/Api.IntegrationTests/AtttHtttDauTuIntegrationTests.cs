using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Api.IntegrationTests.Infrastructure;
using ThucLuc.Domain.Entities.Business;
using ThucLuc.Domain.Enums;

namespace ThucLuc.Api.IntegrationTests;

public sealed class AtttHtttDauTuIntegrationTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public AtttHtttDauTuIntegrationTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAll_Should_Return_Live_Data_Even_When_Legacy_KyCode_Is_Sent()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        var seeded = await _factory.ExecuteDbContextAsync(async dbContext =>
        {
            var donViId = await dbContext.Users
                .Where(x => x.UserName == "donvi.user")
                .Select(x => x.DonViId)
                .FirstAsync();
            var kyCode = await dbContext.KyBaoCaos
                .Where(x => x.Id == 6001)
                .Select(x => x.KyCode)
                .FirstAsync();

            var httt = new HeThongThongTin
            {
                DonViId = donViId,
                TenPhanMem = "HTTT kiểm thử API live",
                LoaiPhanMem = LoaiPhanMemCodes.DungChung,
                PhamViHoatDongKyThuat = "BCANet",
                ValidFrom = DateTime.UtcNow,
                VersionNo = 1,
            };
            dbContext.HeThongThongTins.Add(httt);
            await dbContext.SaveChangesAsync();

            var live = new AtttHtttDauTu
            {
                DonViId = donViId,
                HtttId = httt.Id,
                ChuQuan = "Dữ liệu live mới nhất",
            };
            dbContext.AtttHtttDauTus.Add(live);
            await dbContext.SaveChangesAsync();

            dbContext.AtttHtttDauTuHis.Add(new AtttHtttDauTuHis
            {
                SourceId = live.Id,
                DonViId = donViId,
                HtttId = httt.Id,
                ChuQuan = "Dữ liệu HIS cũ",
                KyBaoCaoCode = kyCode,
                SnapshotCreatedAt = DateTime.UtcNow.AddDays(-1),
            });
            await dbContext.SaveChangesAsync();

            return (donViId, kyCode);
        });

        var response = await client.GetAsync(
            $"/api/v1/attt-httt-dau-tu?donViId={seeded.donViId}&kyCode={seeded.kyCode}&kyBaoCaoCode={seeded.kyCode}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = document.RootElement.GetProperty("data").EnumerateArray().ToList();
        rows.Should().ContainSingle();
        rows[0].GetProperty("chuQuan").GetString().Should().Be("Dữ liệu live mới nhất");
        rows[0].TryGetProperty("kyBaoCaoCode", out _).Should().BeFalse();
    }
}
