using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ThucLuc.Api.IntegrationTests.Infrastructure;

namespace ThucLuc.Api.IntegrationTests;

public sealed class SnapshotDraftIntegrationTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public SnapshotDraftIntegrationTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateDraft_Should_Succeed_When_Period_Open_And_User_Has_Permission()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        var response = await client.PostAsJsonAsync("/api/v1/snapshot/create-draft", new
        {
            kyBaoCaoId = 6001,
            donViId = 2002,
            snapshotJson = "{\"tongHop\":1}",
            summaryJson = "{\"ok\":true}"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateDraft_Should_Reject_When_User_Missing_Permission()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("viewer.user", "ViewerUser@123");

        var response = await client.PostAsJsonAsync("/api/v1/snapshot/create-draft", new
        {
            kyBaoCaoId = 6001,
            donViId = 2003,
            snapshotJson = "{\"tongHop\":1}",
            summaryJson = "{\"ok\":true}"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateDraft_Should_Succeed_While_Status_Is_Draft()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        var createResponse = await client.PostAsJsonAsync("/api/v1/snapshot/create-draft", new
        {
            kyBaoCaoId = 6001,
            donViId = 2002,
            snapshotJson = "{\"v\":1}"
        });
        createResponse.EnsureSuccessStatusCode();
        using var createDocument = System.Text.Json.JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var snapshotId = createDocument.RootElement.GetProperty("data").GetProperty("id").GetInt64();

        var updateResponse = await client.PatchAsJsonAsync($"/api/v1/snapshot/{snapshotId}", new
        {
            snapshotJson = "{\"v\":2}",
            summaryJson = "{\"sum\":2}",
            ghiChu = "updated"
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateDraft_Should_Fail_When_Snapshot_Already_Submitted_Or_Locked()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        var createResponse = await client.PostAsJsonAsync("/api/v1/snapshot/create-draft", new
        {
            kyBaoCaoId = 6001,
            donViId = 2002,
            snapshotJson = "{\"v\":1}"
        });
        createResponse.EnsureSuccessStatusCode();
        using var createDocument = System.Text.Json.JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var snapshotId = createDocument.RootElement.GetProperty("data").GetProperty("id").GetInt64();

        var submitResponse = await client.PostAsJsonAsync($"/api/v1/snapshot/{snapshotId}/submit", new
        {
            ghiChu = "submit"
        });
        submitResponse.EnsureSuccessStatusCode();

        var updateResponse = await client.PatchAsJsonAsync($"/api/v1/snapshot/{snapshotId}", new
        {
            snapshotJson = "{\"v\":3}"
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}