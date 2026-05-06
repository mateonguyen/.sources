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
        document.RootElement.GetProperty("data").GetProperty("kyCode").GetString().Should().Be("2026Q1");
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
}
