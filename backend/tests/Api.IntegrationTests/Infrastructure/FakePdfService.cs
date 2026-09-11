using ThucLuc.Application.Common.Contracts;

namespace ThucLuc.Api.IntegrationTests.Infrastructure;

public sealed class FakePdfService : IPdfService
{
    public string? LastHtml { get; private set; }

    public Task<byte[]> GenerateFromHtmlAsync(string html, CancellationToken cancellationToken = default)
    {
        LastHtml = html;
        return Task.FromResult(System.Text.Encoding.UTF8.GetBytes($"PDF::{html}"));
    }

    public Task<byte[]> ConvertOfficeToPdfAsync(byte[] fileBytes, string fileName, CancellationToken cancellationToken = default)
    {
        var marker = $"PDF::OFFICE::{fileName}::";
        var prefix = System.Text.Encoding.UTF8.GetBytes(marker);
        var result = new byte[prefix.Length + fileBytes.Length];
        Buffer.BlockCopy(prefix, 0, result, 0, prefix.Length);
        Buffer.BlockCopy(fileBytes, 0, result, prefix.Length, fileBytes.Length);
        return Task.FromResult(result);
    }
}
