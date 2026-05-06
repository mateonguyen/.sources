using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ThucLuc.Api.IntegrationTests.Infrastructure;

namespace ThucLuc.Api.IntegrationTests;

public sealed class UserRoleAssignmentIntegrationTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public UserRoleAssignmentIntegrationTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Assign_CAP_TINH_To_TINH_DonVi_Should_Succeed()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("admin", "Admin@123");

        var response = await client.PutAsJsonAsync("/api/v1/users/5003/roles", new
        {
            roleIds = new[] { 4003L },
            donViId = 2002L
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Assign_QUAN_LY_To_TINH_DonVi_Should_Fail_With_Validation()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("admin", "Admin@123");

        var response = await client.PutAsJsonAsync("/api/v1/users/5003/roles", new
        {
            roleIds = new[] { 4002L },
            donViId = 2002L
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("USER_ROLE_DONVI_CAP_MISMATCH");
    }
}
