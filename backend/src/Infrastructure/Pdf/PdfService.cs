using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Infrastructure.Options;

namespace ThucLuc.Infrastructure.Pdf;

public sealed class PdfService : IPdfService
{
    private readonly HttpClient _httpClient;
    private readonly PdfOptions _options;

    public PdfService(HttpClient httpClient, IOptions<PdfOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task<byte[]> GenerateFromHtmlAsync(string html, CancellationToken cancellationToken = default)
    {
        if (_options.UseGotenberg)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var htmlContent = new ByteArrayContent(Encoding.UTF8.GetBytes(html));
                htmlContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/html");
                content.Add(htmlContent, "files", "document.html");
                var response = await _httpClient.PostAsync("forms/chromium/convert/html", content, cancellationToken);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            catch when (_options.EnableFallbackRenderer)
            {
            }
        }

        if (!_options.EnableFallbackRenderer)
        {
            throw new InvalidOperationException("PDF renderer is not available.");
        }

        return MinimalPdfBuilder.Build(StripHtml(html));
    }

    public async Task<byte[]> ConvertOfficeToPdfAsync(byte[] fileBytes, string fileName, CancellationToken cancellationToken = default)
    {
        if (!_options.UseGotenberg)
        {
            throw new InvalidOperationException("Chuyển đổi Office sang PDF cần Gotenberg (UseGotenberg=true).");
        }

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(fileContent, "files", fileName);
        var response = await _httpClient.PostAsync("forms/libreoffice/convert", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private static string StripHtml(string html)
    {
        var builder = new StringBuilder(html.Length);
        var insideTag = false;
        foreach (var character in html)
        {
            if (character == '<')
            {
                insideTag = true;
                continue;
            }

            if (character == '>')
            {
                insideTag = false;
                continue;
            }

            if (!insideTag)
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static class MinimalPdfBuilder
    {
        public static byte[] Build(string text)
        {
            var sanitizedText = text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)").Replace("\r", string.Empty);
            var contentStream = $"BT /F1 12 Tf 50 780 Td ({sanitizedText}) Tj ET";
            var objects = new[]
            {
                "1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj",
                "2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj",
                "3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >> endobj",
                $"4 0 obj << /Length {Encoding.ASCII.GetByteCount(contentStream)} >> stream\n{contentStream}\nendstream endobj",
                "5 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj"
            };

            var builder = new StringBuilder();
            builder.AppendLine("%PDF-1.4");
            var offsets = new List<int>();
            foreach (var obj in objects)
            {
                offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
                builder.AppendLine(obj);
            }

            var xrefPosition = Encoding.ASCII.GetByteCount(builder.ToString());
            builder.AppendLine($"xref\n0 {objects.Length + 1}\n0000000000 65535 f ");
            foreach (var offset in offsets)
            {
                builder.AppendLine($"{offset:D10} 00000 n ");
            }

            builder.AppendLine($"trailer << /Size {objects.Length + 1} /Root 1 0 R >>");
            builder.AppendLine($"startxref\n{xrefPosition}\n%%EOF");
            return Encoding.ASCII.GetBytes(builder.ToString());
        }
    }
}