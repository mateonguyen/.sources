using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using ThucLuc.Api.IntegrationTests.Infrastructure;

namespace ThucLuc.Api.IntegrationTests;

public sealed class FileUploadValidationIntegrationTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public FileUploadValidationIntegrationTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("DonViId")]
    [InlineData("EntityType")]
    [InlineData("EntityId")]
    [InlineData("KyCode")]
    [InlineData("file")]
    public async Task Upload_Should_Return_BadRequest_When_Required_Field_Missing(string missingField)
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("admin", "Admin@123");
        using var content = CreateUploadContent(missingField);

        var response = await client.PostAsync("/api/v1/files/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().Contain("VALIDATION_ERROR");
        payload.Should().Contain(missingField);
    }

    [Fact]
    public async Task Upload_Should_Succeed_When_Payload_Is_Valid()
    {
        await _factory.ResetDataAsync();
        using var client = await _factory.CreateAuthorizedClientAsync("admin", "Admin@123");
        using var content = CreateUploadContent();

        var response = await client.PostAsync("/api/v1/files/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static MultipartFormDataContent CreateUploadContent(string? missingField = null)
    {
        var content = new MultipartFormDataContent();

        if (!IsMissing("DonViId", missingField))
        {
            content.Add(new StringContent("2002"), "DonViId");
        }

        if (!IsMissing("EntityType", missingField))
        {
            content.Add(new StringContent("snapshot"), "EntityType");
        }

        if (!IsMissing("EntityId", missingField))
        {
            content.Add(new StringContent("1"), "EntityId");
        }

        if (!IsMissing("KyCode", missingField))
        {
            content.Add(new StringContent("2026Q3_E2E_TEST"), "KyCode");
        }

        if (!IsMissing("file", missingField))
        {
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("test-file-content"));
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
            content.Add(fileContent, "file", "test.txt");
        }

        return content;
    }

    private static bool IsMissing(string fieldName, string? missingField)
    {
        return string.Equals(fieldName, missingField, StringComparison.OrdinalIgnoreCase);
    }
}
