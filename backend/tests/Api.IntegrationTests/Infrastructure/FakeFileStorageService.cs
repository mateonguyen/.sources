using System.Collections.Concurrent;
using ThucLuc.Application.Common.Contracts;

namespace ThucLuc.Api.IntegrationTests.Infrastructure;

public sealed class FakeFileStorageService : IFileStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task<string> UploadAsync(string objectKey, Stream stream, string contentType, CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        _store[objectKey] = memoryStream.ToArray();
        return Task.FromResult(objectKey);
    }

    public Task<string> GetPresignedDownloadUrlAsync(string objectKey, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"https://fake-storage.local/download/{Uri.EscapeDataString(objectKey)}?ttl={(int)ttl.TotalSeconds}");
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(objectKey, out _);
        return Task.CompletedTask;
    }

    public byte[]? TryGet(string objectKey)
    {
        return _store.TryGetValue(objectKey, out var payload) ? payload : null;
    }
}
