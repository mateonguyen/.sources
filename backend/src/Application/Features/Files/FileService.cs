using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using FluentValidation.Results;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Domain.Entities.System;

namespace ThucLuc.Application.Features.Files;

public interface IFileService
{
    Task<IReadOnlyCollection<FileMetadataDto>> GetByEntityAsync(string entityType, long entityId, CancellationToken cancellationToken = default);

    Task<FileMetadataDto> UploadAsync(UploadFileRequest request, IFormFile file, CancellationToken cancellationToken = default);

    Task<string> GetDownloadUrlAsync(long id, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class FileService : IFileService
{
    private static readonly TimeSpan PresignedUrlTtl = TimeSpan.FromMinutes(15);

    private readonly IApplicationDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<UploadFileRequest> _uploadValidator;

    public FileService(
        IApplicationDbContext dbContext,
        IFileStorageService fileStorageService,
        IDateTimeProvider dateTimeProvider,
        ICurrentUserService currentUserService,
        IValidator<UploadFileRequest> uploadValidator)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
        _dateTimeProvider = dateTimeProvider;
        _currentUserService = currentUserService;
        _uploadValidator = uploadValidator;
    }

    public async Task<FileMetadataDto> UploadAsync(UploadFileRequest request, IFormFile file, CancellationToken cancellationToken = default)
    {
        await _uploadValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (file is null || file.Length <= 0)
        {
            throw new ValidationException([
                new ValidationFailure("file", "file là bắt buộc.")
            ]);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var ext = Path.GetExtension(file.FileName);
        var objectKey = $"{request.EntityType.ToLower()}/{request.DonViId}/{request.EntityId}/{Guid.NewGuid():N}{ext}";

        await using var stream = file.OpenReadStream();
        var filePath = await _fileStorageService.UploadAsync(objectKey, stream, file.ContentType, cancellationToken);

        var entity = new FileDinhKem
        {
            DonViId = request.DonViId,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            FileName = file.FileName,
            FilePath = filePath,
            FileSize = file.Length,
            MimeType = file.ContentType,
            UploadedBy = currentUser.UserId,
            UploadedAt = _dateTimeProvider.Now,
        };

        _dbContext.FileDinhKems.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new FileMetadataDto
        {
            Id = entity.Id,
            EntityType = entity.EntityType,
            EntityId = entity.EntityId,
            FileName = entity.FileName,
            MimeType = entity.MimeType,
            FileSize = entity.FileSize,
        };
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