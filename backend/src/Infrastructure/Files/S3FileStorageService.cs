using Minio;
using Minio.DataModel.Args;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Infrastructure.Options;

namespace ThucLuc.Infrastructure.Files;

public sealed class S3FileStorageService : IFileStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly MinioOptions _options;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<S3FileStorageService> _logger;
    private readonly SemaphoreSlim _bucketEnsureLock = new(1, 1);
    private volatile bool _bucketEnsured;

    public S3FileStorageService(
        IMinioClient minioClient,
        IOptions<MinioOptions> options,
        IDateTimeProvider dateTimeProvider,
        ILogger<S3FileStorageService> logger)
    {
        _minioClient = minioClient;
        _options = options.Value;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<string> UploadAsync(string objectKey, Stream stream, string contentType, CancellationToken cancellationToken = default)
    {
        await EnsureBucketExistsAsync(cancellationToken);

        var normalizedKey = NormalizeKey(objectKey);
        var (uploadStream, objectSize, resetPosition) = await PrepareUploadStreamAsync(stream, cancellationToken);

        try
        {
            var putArgs = new PutObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(normalizedKey)
                .WithStreamData(uploadStream)
                .WithObjectSize(objectSize)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(putArgs, cancellationToken);
            return normalizedKey;
        }
        finally
        {
            if (resetPosition && uploadStream.CanSeek)
            {
                uploadStream.Position = 0;
            }
        }
    }

    public Task<string> GetPresignedDownloadUrlAsync(string objectKey, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (ttl <= TimeSpan.Zero || ttl > TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be between 1 second and 24 hours.");
        }

        var expiresInSeconds = (int)Math.Floor(ttl.TotalSeconds);
        var request = new PresignedGetObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(NormalizeKey(objectKey))
            .WithExpiry(expiresInSeconds);

        return _minioClient.PresignedGetObjectAsync(request);
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var args = new RemoveObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(NormalizeKey(objectKey));

        await _minioClient.RemoveObjectAsync(args, cancellationToken);
    }

    private async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
    {
        if (_bucketEnsured)
        {
            return;
        }

        await _bucketEnsureLock.WaitAsync(cancellationToken);
        try
        {
            if (_bucketEnsured)
            {
                return;
            }

            var exists = await _minioClient.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_options.BucketName),
                cancellationToken);

            if (!exists)
            {
                var makeBucketArgs = new MakeBucketArgs()
                    .WithBucket(_options.BucketName)
                    .WithLocation(_options.Region);
                await _minioClient.MakeBucketAsync(makeBucketArgs, cancellationToken);
            }

            _bucketEnsured = true;
        }
        finally
        {
            _bucketEnsureLock.Release();
        }
    }

    private static async Task<(Stream UploadStream, long ObjectSize, bool ResetPosition)> PrepareUploadStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            var remaining = Math.Max(0, stream.Length - stream.Position);
            return (stream, remaining, true);
        }

        var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        return (buffer, buffer.Length, false);
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