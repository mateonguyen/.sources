using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
        using var client = await _factory.CreateAuthorizedClientAsync("admin", "Test@123");
        var unitId = await _factory.ExecuteDbContextAsync(db => db.Users.Where(x => x.UserName == "donvi.user").Select(x => x.DonViId).FirstAsync());

        var response = await client.PutAsJsonAsync("/api/v1/users/5003/roles", new
        {
            roleIds = new[] { 4003L },
            donViId = unitId
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Assign_CAP_XA_To_TINH_DonVi_Should_Fail_With_Validation()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("admin", "Test@123");
        var unitId = await _factory.ExecuteDbContextAsync(db => db.Users.Where(x => x.UserName == "donvi.user").Select(x => x.DonViId).FirstAsync());

        var response = await client.PutAsJsonAsync("/api/v1/users/5003/roles", new
        {
            roleIds = new[] { 4004L },
            donViId = unitId
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("USER_ROLE_DONVI_CAP_MISMATCH");
    }
}
