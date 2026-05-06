namespace ThucLuc.Application.Features.Files;

public sealed class FileMetadataDto
{
    public long Id { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public long EntityId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string MimeType { get; set; } = string.Empty;

    public long FileSize { get; set; }
}

public sealed class UploadFileRequest
{
    public long DonViId { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public long EntityId { get; set; }

    public string KyCode { get; set; } = string.Empty;
}