using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Options;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Infrastructure.Options;

namespace ThucLuc.Infrastructure.Files;

public sealed class S3FileStorageService : IFileStorageService
{
    private readonly IAmazonS3 _amazonS3;
    private readonly MinioOptions _options;
    private readonly IDateTimeProvider _dateTimeProvider;

    public S3FileStorageService(IAmazonS3 amazonS3, IOptions<MinioOptions> options, IDateTimeProvider dateTimeProvider)
    {
        _amazonS3 = amazonS3;
        _options = options.Value;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<string> UploadAsync(string objectKey, Stream stream, string contentType, CancellationToken cancellationToken = default)
    {
        await EnsureBucketExistsAsync(cancellationToken);
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = NormalizeKey(objectKey),
            InputStream = stream,
            AutoCloseStream = false,
            ContentType = contentType
        };

        await _amazonS3.PutObjectAsync(request, cancellationToken);
        return request.Key;
    }

    public Task<string> GetPresignedDownloadUrlAsync(string objectKey, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (ttl <= TimeSpan.Zero || ttl > TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be between 1 second and 24 hours.");
        }

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = NormalizeKey(objectKey),
            Expires = _dateTimeProvider.Now.Add(ttl),
            Verb = HttpVerb.GET
        };

        return Task.FromResult(_amazonS3.GetPreSignedURL(request));
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        await _amazonS3.DeleteObjectAsync(_options.BucketName, NormalizeKey(objectKey), cancellationToken);
    }

    private async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
    {
        var exists = await AmazonS3Util.DoesS3BucketExistV2Async(_amazonS3, _options.BucketName);
        if (!exists)
        {
            await _amazonS3.PutBucketAsync(_options.BucketName, cancellationToken);
        }
    }

    private static string NormalizeKey(string objectKey)
    {
        var normalized = objectKey.Trim().TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("Invalid object key.", nameof(objectKey));
        }

        return normalized;
    }
}