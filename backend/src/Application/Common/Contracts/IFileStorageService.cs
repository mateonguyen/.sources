namespace ThucLuc.Application.Common.Contracts;

public interface IFileStorageService
{
    Task<string> UploadAsync(string objectKey, Stream stream, string contentType, CancellationToken cancellationToken = default);

    Task<string> GetPresignedDownloadUrlAsync(string objectKey, TimeSpan ttl, CancellationToken cancellationToken = default);

    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);
}