using System.ComponentModel.DataAnnotations;

namespace ThucLuc.Infrastructure.Options;

public sealed class MinioOptions
{
    public const string SectionName = "Minio";

    [Required]
    public string ServiceUrl { get; set; } = string.Empty;

    [Required]
    public string AccessKey { get; set; } = string.Empty;

    [Required]
    public string SecretKey { get; set; } = string.Empty;

    [Required]
    public string BucketName { get; set; } = string.Empty;

    public bool UseSsl { get; set; } = false;

    public string Region { get; set; } = "us-east-1";
}