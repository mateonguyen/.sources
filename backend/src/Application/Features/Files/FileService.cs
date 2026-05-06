using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Domain.Entities.System;

namespace ThucLuc.Application.Features.Files;

public interface IFileService
{
    Task<IReadOnlyCollection<FileMetadataDto>> GetByEntityAsync(string entityType, long entityId, CancellationToken cancellationToken = default);

    Task<string> GetDownloadUrlAsync(long id, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class FileService : IFileService
{
    private static readonly TimeSpan PresignedUrlTtl = TimeSpan.FromMinutes(15);

    private readonly IApplicationDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public FileService(IApplicationDbContext dbContext, IFileStorageService fileStorageService, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyCollection<FileMetadataDto>> GetByEntityAsync(string entityType, long entityId, CancellationToken cancellationToken = default)
        => await _dbContext.FileDinhKems
            .Where(x => x.EntityType == entityType && x.EntityId == entityId)
            .Select(x => new FileMetadataDto
            {
                Id = x.Id,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                FileName = x.FileName,
                MimeType = x.MimeType,
                FileSize = x.FileSize
            })
            .ToListAsync(cancellationToken);

    public async Task<string> GetDownloadUrlAsync(long id, CancellationToken cancellationToken = default)
    {
        var file = await _dbContext.FileDinhKems.FirstAsync(x => x.Id == id, cancellationToken);
        return await _fileStorageService.GetPresignedDownloadUrlAsync(file.FilePath, PresignedUrlTtl, cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var file = await _dbContext.FileDinhKems.FirstAsync(x => x.Id == id, cancellationToken);
        file.DeletedAt = _dateTimeProvider.Now;
        await _fileStorageService.DeleteAsync(file.FilePath, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}