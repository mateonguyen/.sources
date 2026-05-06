using System.Net;
using System.Net.Http.Json;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Api.IntegrationTests.Infrastructure;

namespace ThucLuc.Api.IntegrationTests;

public sealed class NhanLucCnttIntegrationTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public NhanLucCnttIntegrationTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Scd2_Create_Update_AsOf_Delete_Should_Work_EndToEnd()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        var createResponse = await client.PostAsJsonAsync("/api/v1/nhan-luc-cntt", new
        {
            donViId = 2002,
            hoTen = "Nguyen Van A",
            trinhDoCntt = "Dai hoc",
            chucVu = "Chuyen vien",
            gioiTinh = "Nam",
            loaiNhanLuc = "CHUYEN_TRACH"
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var createDocument = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var created = createDocument.RootElement.GetProperty("data");
        var createdId = created.GetProperty("id").GetInt64();
        var nhanSuKey = created.GetProperty("nhanSuKey").GetString();
        nhanSuKey.Should().NotBeNullOrWhiteSpace();

        // Capture timestamp from version 1 to query as-of old snapshot after update.
        var version1ValidFrom = created.GetProperty("validFrom").GetDateTime();

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/nhan-luc-cntt/{createdId}", new
        {
            donViId = 2002,
            hoTen = "Nguyen Van A",
            trinhDoCntt = "Dai hoc",
            chucVu = "Pho phong",
            gioiTinh = "Nam",
            loaiNhanLuc = "CHUYEN_TRACH"
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var updateDocument = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
        var updated = updateDocument.RootElement.GetProperty("data");
        var updatedId = updated.GetProperty("id").GetInt64();
        updated.GetProperty("versionNo").GetInt32().Should().Be(2);
        updated.GetProperty("chucVu").GetString().Should().Be("Pho phong");

        var currentListResponse = await client.GetAsync("/api/v1/nhan-luc-cntt?page=1&pageSize=20");
        currentListResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var currentListDocument = JsonDocument.Parse(await currentListResponse.Content.ReadAsStringAsync());
        var currentItems = currentListDocument.RootElement.GetProperty("data").GetProperty("items");
        currentItems.GetArrayLength().Should().BeGreaterThan(0);
        currentItems.EnumerateArray().Any(x => x.GetProperty("id").GetInt64() == updatedId).Should().BeTrue();

        var asOfParam = Uri.EscapeDataString(version1ValidFrom.ToString("O"));
        var asOfResponse = await client.GetAsync($"/api/v1/nhan-luc-cntt?asOfDate={asOfParam}&page=1&pageSize=20");
        asOfResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var asOfDocument = JsonDocument.Parse(await asOfResponse.Content.ReadAsStringAsync());
        var asOfItems = asOfDocument.RootElement.GetProperty("data").GetProperty("items");
        asOfItems.EnumerateArray().Any(x => x.GetProperty("id").GetInt64() == createdId).Should().BeTrue();

        var timelineResponse = await client.GetAsync($"/api/v1/nhan-luc-cntt/timeline/{nhanSuKey}");
        timelineResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var timelineDocument = JsonDocument.Parse(await timelineResponse.Content.ReadAsStringAsync());
        var timelineItems = timelineDocument.RootElement.GetProperty("data");
        timelineItems.GetArrayLength().Should().Be(2);
        timelineItems[0].GetProperty("versionNo").GetInt32().Should().Be(2);
        timelineItems[1].GetProperty("versionNo").GetInt32().Should().Be(1);

        var deleteResponse = await client.DeleteAsync($"/api/v1/nhan-luc-cntt/{updatedId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getDeletedCurrentResponse = await client.GetAsync($"/api/v1/nhan-luc-cntt/{updatedId}");
        getDeletedCurrentResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var currentCount = await _factory.ExecuteDbContextAsync(db => db.NhanLucCntts.CountAsync(x => x.NhanSuKey == nhanSuKey));
        currentCount.Should().Be(0);

        var hisCount = await _factory.ExecuteDbContextAsync(db => db.NhanLucCnttHis.CountAsync(x => x.NhanSuKey == nhanSuKey));
        hisCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Delete_Should_Return_Forbidden_For_Viewer()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("viewer.user", "ViewerUser@123");

        var response = await client.DeleteAsync("/api/v1/nhan-luc-cntt/1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_NonTrigger_Fields_Should_Update_InPlace_Without_New_Version()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("donvi.user", "DonViUser@123");

        var createResponse = await client.PostAsJsonAsync("/api/v1/nhan-luc-cntt", new
        {
            donViId = 2002,
            hoTen = "Tran Thi B",
            trinhDoCntt = "Dai hoc",
            chucVu = "Chuyen vien",
            gioiTinh = "Nu",
            loaiNhanLuc = "CHUYEN_TRACH",
            dienThoai = "0900000001"
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var createDocument = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var created = createDocument.RootElement.GetProperty("data");
        var createdId = created.GetProperty("id").GetInt64();
        var nhanSuKey = created.GetProperty("nhanSuKey").GetString();
        var validFrom = created.GetProperty("validFrom").GetDateTime();

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/nhan-luc-cntt/{createdId}", new
        {
            donViId = 2002,
            hoTen = "Tran Thi B",
            trinhDoCntt = "Dai hoc",
            chucVu = "Chuyen vien",
            gioiTinh = "Nu",
            loaiNhanLuc = "CHUYEN_TRACH",
            dienThoai = "0900000002"
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var updateDocument = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
        var updated = updateDocument.RootElement.GetProperty("data");
        updated.GetProperty("id").GetInt64().Should().Be(createdId);
        updated.GetProperty("versionNo").GetInt32().Should().Be(1);
        updated.GetProperty("dienThoai").GetString().Should().Be("0900000002");
        updated.GetProperty("validFrom").GetDateTime().Should().Be(validFrom);

        var timelineResponse = await client.GetAsync($"/api/v1/nhan-luc-cntt/timeline/{nhanSuKey}");
        timelineResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var timelineDocument = JsonDocument.Parse(await timelineResponse.Content.ReadAsStringAsync());
        var timelineItems = timelineDocument.RootElement.GetProperty("data");
        timelineItems.GetArrayLength().Should().Be(1);
        timelineItems[0].GetProperty("versionNo").GetInt32().Should().Be(1);
    }
}
