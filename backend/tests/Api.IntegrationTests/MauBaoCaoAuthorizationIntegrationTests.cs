using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ThucLuc.Api.IntegrationTests.Infrastructure;

namespace ThucLuc.Api.IntegrationTests;

public sealed class MauBaoCaoAuthorizationIntegrationTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public MauBaoCaoAuthorizationIntegrationTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_Should_Be_Forbidden_For_Cap_Tinh_Without_Create_Permission()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "Test@123");

        var response = await client.PostAsJsonAsync("/api/v1/mau-bao-cao", new
        {
            maMau = "CAP_TINH_NO_CREATE",
            tenMau = "Mẫu không được phép tạo",
            danhSachModule = new[] { "nhan_luc_cntt" },
            tanSuat = 2,
            moTa = "Kiểm thử phân quyền tạo mẫu báo cáo",
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
