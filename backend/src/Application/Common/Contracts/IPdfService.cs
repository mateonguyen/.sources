namespace ThucLuc.Application.Common.Contracts;

public interface IPdfService
{
    Task<byte[]> GenerateFromHtmlAsync(string html, CancellationToken cancellationToken = default);

    /// <summary>Convert file Office (xlsx/docx...) sang PDF qua Gotenberg route LibreOffice.</summary>
    Task<byte[]> ConvertOfficeToPdfAsync(byte[] fileBytes, string fileName, CancellationToken cancellationToken = default);
}