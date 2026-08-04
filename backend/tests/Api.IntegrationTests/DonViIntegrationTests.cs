using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ThucLuc.Api.IntegrationTests.Infrastructure;

namespace ThucLuc.Api.IntegrationTests;

public sealed class DonViIntegrationTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public DonViIntegrationTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetTree_Should_Return_Ok_For_Authorized_User()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("viewer.user", "ViewerUser@123");

        var response = await client.GetAsync("/api/v1/don-vi/tree");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_Should_Return_Forbidden_For_User_Without_Create_Permission()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("viewer.user", "ViewerUser@123");

        var response = await client.PostAsJsonAsync("/api/v1/don-vi", new
        {
            maDonVi = "NEW_DV",
            tenDonVi = "Don vi moi",
            parentId = 2001,
            capDonVi = "PHONG",
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetById_Should_Return_Derived_Counts_After_Creating_Phong_Child()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("admin", "Admin@123");

        var createResponse = await client.PostAsJsonAsync("/api/v1/don-vi", new
        {
            maDonVi = "CA_HN_P1",
            tenDonVi = "Phong CNTT Ha Noi",
            tenVietTat = "P1",
            parentId = 2002,
            diaChi = "Ha Noi",
            capDonVi = "PHONG",
            websiteNoiBo = "http://phong1-noibo.local",
            websiteInternet = "https://phong1.example.gov.vn",
            tongBienChe = 120,
            isActive = true
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await client.GetAsync("/api/v1/don-vi/2002");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = System.Text.Json.JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        var data = document.RootElement.GetProperty("data");
        data.GetProperty("capDonVi").GetString().Should().Be("TINH");
        data.GetProperty("soDonViCapPhong").GetInt32().Should().Be(1);
        data.GetProperty("soDonViCapXa").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Create_Should_Reject_TongHop_Mode_For_Phong_Level()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("admin", "Admin@123");

        var response = await client.PostAsJsonAsync("/api/v1/don-vi", new
        {
            maDonVi = "P_MODE_INVALID",
            tenDonVi = "Phong mode invalid",
            parentId = 2002,
            capDonVi = "PHONG",
            cheDoNhapLieu = "TONG_HOP",
            isActive = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("DONVI_CHE_DO_NHAP_LIEU_INVALID");
    }
}
