using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Api.IntegrationTests.Infrastructure;

namespace ThucLuc.Api.IntegrationTests;

public sealed class SnapshotPdfIntegrationTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public SnapshotPdfIntegrationTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetPdf_Should_Be_Rejected_When_Snapshot_Is_Draft()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");
        var snapshotId = await CreateDraftAsync(client, "{\"field\":\"draft\"}");

        var response = await client.GetAsync($"/api/v1/snapshot/{snapshotId}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetPdf_Should_Succeed_After_Submit()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");
        var snapshotId = await CreateDraftAndSubmitAsync(client, "{\"field\":\"submitted\"}");

        var response = await client.GetAsync($"/api/v1/snapshot/{snapshotId}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("data").GetProperty("downloadUrl").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Pdf_Should_Be_Generated_From_Stored_Snapshot()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");
        _ = await CreateDraftAndSubmitAsync(client, "{\"marker\":\"payload-marker-2026\"}");

        _factory.FakePdfService.LastHtml.Should().NotBeNull();
        _factory.FakePdfService.LastHtml.Should().Contain("Báo cáo #");
        _factory.FakePdfService.LastHtml.Should().Contain("Kỳ 6001");
        _factory.FakePdfService.LastHtml.Should().Contain("Đơn vị 2002");
    }

    [Fact]
    public async Task Submit_And_GeneratePdf_Should_Create_File_Metadata()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        var snapshotId = await CreateDraftAndSubmitAsync(client, "{\"meta\":true}");

        var hasFileMetadata = await _factory.ExecuteDbContextAsync(async dbContext =>
            await dbContext.BaoCaoFiles.AnyAsync(x => x.BaoCaoSnapshotId == snapshotId && x.LoaiFile == "pdf"));

        hasFileMetadata.Should().BeTrue();
    }

    private static async Task<long> CreateDraftAsync(HttpClient client, string snapshotJson)
    {
        var createResponse = await client.PostAsJsonAsync("/api/v1/snapshot/create-draft", new
        {
            kyBaoCaoId = 6001,
            donViId = 2002,
            snapshotJson
        });
        createResponse.EnsureSuccessStatusCode();
        using var document = System.Text.Json.JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").GetProperty("id").GetInt64();
    }

    private static async Task<long> CreateDraftAndSubmitAsync(HttpClient client, string snapshotJson)
    {
        var snapshotId = await CreateDraftAsync(client, snapshotJson);
        var submitResponse = await client.PostAsJsonAsync($"/api/v1/snapshot/{snapshotId}/submit", new { ghiChu = "submit" });
        submitResponse.EnsureSuccessStatusCode();
        return snapshotId;
    }
}