namespace ThucLuc.Infrastructure.Options;

public sealed class PdfOptions
{
    public const string SectionName = "Pdf";

    public bool UseGotenberg { get; set; } = true;

    public string GotenbergUrl { get; set; } = "http://localhost:3000";

    public int TimeoutSeconds { get; set; } = 30;

    public bool EnableFallbackRenderer { get; set; } = true;

    public int MaxSnapshotJsonBytes { get; set; } = 524288;

    public int MaxGeneratedPdfBytes { get; set; } = 10485760;
}