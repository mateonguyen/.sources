using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ThucLuc.Api.IntegrationTests.Infrastructure;

namespace ThucLuc.Api.IntegrationTests;

[Trait("Category", "Smoke")]
public sealed class SmokeIntegrationTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public SmokeIntegrationTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_Endpoints_Should_Return_Ok()
    {
        await _factory.ResetDataAsync();
        using var client = _factory.CreateClient();

        var healthResponse = await client.GetAsync("/health");
        var liveResponse = await client.GetAsync("/health/live");
        var readyResponse = await client.GetAsync("/health/ready");

        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        liveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Version_Should_Return_Metadata()
    {
        await _factory.ResetDataAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/version");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("application").GetString().Should().NotBeNullOrWhiteSpace();
        document.RootElement.GetProperty("environment").GetString().Should().NotBeNullOrWhiteSpace();
        document.RootElement.GetProperty("version").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Swagger_Should_Be_Available_In_Testing()
    {
        await _factory.ResetDataAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_Then_Me_Should_Succeed()
    {
        await _factory.ResetDataAsync();
        using var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "admin",
            password = "Admin@123"
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var loginDocument = System.Text.Json.JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var token = loginDocument.RootElement.GetProperty("data").GetProperty("accessToken").GetString();
        token.Should().NotBeNullOrWhiteSpace();

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var meResponse = await client.GetAsync("/api/v1/auth/me");

        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
