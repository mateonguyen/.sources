using System.Text;
using System.Text.RegularExpressions;

namespace ThucLuc.DbManager.Services;

internal static partial class OracleDumpVersionReader
{
    private const int HeaderReadLimit = 1024 * 1024;

    public static async Task<string?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            HeaderReadLimit,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[Math.Min(HeaderReadLimit, checked((int)Math.Min(stream.Length, HeaderReadLimit)))];
        var read = await stream.ReadAsync(buffer, cancellationToken);
        var header = Encoding.ASCII.GetString(buffer, 0, read);
        return DumpVersionRegex().Matches(header)
            .Select(match => match.Value)
            .FirstOrDefault();
    }

    public static int? GetMajor(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return null;
        var firstPart = version.Split('.', 2)[0];
        return int.TryParse(firstPart, out var major) ? major : null;
    }

    public static Version? GetRelease(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return null;
        var parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !int.TryParse(parts[0], out var major)) return null;
        var minor = parts.Length > 1 && int.TryParse(parts[1], out var parsedMinor) ? parsedMinor : 0;
        return new Version(major, minor);
    }

    [GeneratedRegex(@"(?<!\d)(\d{2})\.\d{2}\.\d{2}\.\d{2}\.\d{2}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex DumpVersionRegex();
}
