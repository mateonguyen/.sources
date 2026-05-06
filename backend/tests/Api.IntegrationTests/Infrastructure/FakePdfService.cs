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
}
