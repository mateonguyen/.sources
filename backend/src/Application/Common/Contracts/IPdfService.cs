namespace ThucLuc.Application.Common.Contracts;

public interface IPdfService
{
    Task<byte[]> GenerateFromHtmlAsync(string html, CancellationToken cancellationToken = default);
}