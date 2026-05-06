using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ThucLuc.Api.IntegrationTests.Infrastructure;

namespace ThucLuc.Api.IntegrationTests;

public sealed class AuthorizationIntegrationTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public AuthorizationIntegrationTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task User_Without_Permission_Should_Not_Call_Snapshot_Create()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("viewer.user", "ViewerUser@123");

        var response = await client.PostAsJsonAsync("/api/v1/snapshot/create-draft", new
        {
            kyBaoCaoId = 6001,
            donViId = 2003,
            snapshotJson = "{\"sample\":1}"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SystemAdmin_Should_Bypass_Permission_Checks()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("admin", "Admin@123");

        var response = await client.PostAsJsonAsync("/api/v1/snapshot/create-draft", new
        {
            kyBaoCaoId = 6001,
            donViId = 2001,
            snapshotJson = "{\"sample\":1}"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}