using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ThucLuc.DbManager.Configuration;
using ThucLuc.DbManager.Models;

namespace ThucLuc.DbManager.Services;

public interface IReleasePackageService
{
    IReadOnlyList<ReleaseBundle> GetAll();

    ReleaseBundle GetRequired(string version);

    Task<ReleaseImportResult> ImportAsync(
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);
}

public sealed partial class ReleasePackageService(
    OpsRuntimePaths runtimePaths,
    ILogger<ReleasePackageService> logger) : IReleasePackageService
{
    private const long MaxCompressedBytes = 12L * 1024 * 1024 * 1024;
    private const long MaxExpandedBytes = 30L * 1024 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly SemaphoreSlim _importLock = new(1, 1);

    public IReadOnlyList<ReleaseBundle> GetAll()
    {
        Directory.CreateDirectory(runtimePaths.ReleaseDirectory);
        return Directory.EnumerateDirectories(runtimePaths.ReleaseDirectory)
            .Where(path => !Path.GetFileName(path).StartsWith(".", StringComparison.Ordinal))
            .Select(TryReadBundle)
            .Where(bundle => bundle is not null)
            .Cast<ReleaseBundle>()
            .OrderByDescending(bundle => bundle.Manifest.CreatedAt)
            .ToList();
    }

    public ReleaseBundle GetRequired(string version)
    {
        ValidateVersion(version);
        var directory = Path.Combine(runtimePaths.ReleaseDirectory, version);
        return TryReadBundle(directory)
            ?? throw new InvalidOperationException($"Không tìm thấy release hợp lệ {version}.");
    }

    public async Task<ReleaseImportResult> ImportAsync(
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return new ReleaseImportResult(false, "Gói release phải là file .zip.");
        }

        Directory.CreateDirectory(runtimePaths.ReleaseDirectory);
        await _importLock.WaitAsync(cancellationToken);
        var workId = Guid.NewGuid().ToString("N");
        var uploadFile = Path.Combine(runtimePaths.ReleaseDirectory, $".upload-{workId}.zip");
        var extractDirectory = Path.Combine(runtimePaths.ReleaseDirectory, $".extract-{workId}");
        try
        {
            var packageSha = await CopyAndHashAsync(content, uploadFile, cancellationToken);
            Directory.CreateDirectory(extractDirectory);
            await ExtractSafelyAsync(uploadFile, extractDirectory, cancellationToken);

            var manifestPath = Path.Combine(extractDirectory, "release-manifest.json");
            var checksumPath = Path.Combine(extractDirectory, "checksums.sha256");
            if (!File.Exists(manifestPath) || !File.Exists(checksumPath))
            {
                throw new InvalidOperationException("Gói release thiếu release-manifest.json hoặc checksums.sha256.");
            }

            await VerifyChecksumsAsync(extractDirectory, checksumPath, cancellationToken);
            var manifest = JsonSerializer.Deserialize<ReleaseManifest>(
                await File.ReadAllTextAsync(manifestPath, cancellationToken),
                JsonOptions) ?? throw new InvalidOperationException("Không đọc được release manifest.");
            ValidateManifest(manifest, extractDirectory);

            var destination = Path.Combine(runtimePaths.ReleaseDirectory, manifest.Version);
            if (Directory.Exists(destination))
            {
                throw new InvalidOperationException($"Release {manifest.Version} đã tồn tại.");
            }

            await File.WriteAllTextAsync(
                Path.Combine(extractDirectory, ".package-sha256"),
                packageSha,
                cancellationToken);
            Directory.Move(extractDirectory, destination);
            var bundle = new ReleaseBundle(manifest, destination, packageSha);
            logger.LogInformation("Đã nhập release {ReleaseVersion} ({PackageSha})", manifest.Version, packageSha);
            return new ReleaseImportResult(true, $"Đã nhập release {manifest.Version} và xác minh checksum.", bundle);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Không thể nhập gói release {ReleaseFile}", fileName);
            return new ReleaseImportResult(false, exception.GetBaseException().Message);
        }
        finally
        {
            if (File.Exists(uploadFile))
            {
                File.Delete(uploadFile);
            }

            if (Directory.Exists(extractDirectory))
            {
                Directory.Delete(extractDirectory, recursive: true);
            }

            _importLock.Release();
        }
    }

    private static async Task<string> CopyAndHashAsync(
        Stream source,
        string destination,
        CancellationToken cancellationToken)
    {
        await using var output = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 1024,
            useAsync: true);
        using var sha = SHA256.Create();
        var buffer = new byte[1024 * 1024];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > MaxCompressedBytes)
            {
                throw new InvalidOperationException("Gói release vượt quá giới hạn 12 GB.");
            }

            sha.TransformBlock(buffer, 0, read, null, 0);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    private static async Task ExtractSafelyAsync(
        string archivePath,
        string destination,
        CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var destinationPrefix = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        var totalExpanded = archive.Entries.Sum(entry => entry.Length);
        if (totalExpanded > MaxExpandedBytes)
        {
            throw new InvalidOperationException("Nội dung giải nén vượt quá giới hạn 30 GB.");
        }

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!target.StartsWith(destinationPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Gói release chứa đường dẫn không an toàn.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var source = entry.Open();
            await using var output = new FileStream(
                target,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1024 * 1024,
                useAsync: true);
            await source.CopyToAsync(output, 1024 * 1024, cancellationToken);
        }
    }

    private static async Task VerifyChecksumsAsync(
        string root,
        string checksumPath,
        CancellationToken cancellationToken)
    {
        var entries = await File.ReadAllLinesAsync(checksumPath, cancellationToken);
        if (entries.Length == 0)
        {
            throw new InvalidOperationException("File checksum đang trống.");
        }

        var verified = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in entries.Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            var match = ChecksumRegex().Match(line.Trim());
            if (!match.Success)
            {
                throw new InvalidOperationException("Định dạng checksums.sha256 không hợp lệ.");
            }

            var relativePath = match.Groups[2].Value.Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
            var rootPrefix = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            {
                throw new InvalidOperationException($"Checksum tham chiếu file không hợp lệ: {relativePath}");
            }

            await using var stream = File.OpenRead(fullPath);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
            if (!string.Equals(actual, match.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Checksum không khớp: {relativePath}");
            }

            verified.Add(relativePath);
        }

        var filesToVerify = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(path, checksumPath, StringComparison.OrdinalIgnoreCase));
        foreach (var file in filesToVerify)
        {
            var relative = Path.GetRelativePath(root, file);
            if (!verified.Contains(relative))
            {
                throw new InvalidOperationException($"File chưa có checksum: {relative}");
            }
        }
    }

    private static ReleaseBundle? TryReadBundle(string directory)
    {
        try
        {
            var manifestPath = Path.Combine(directory, "release-manifest.json");
            var packageShaPath = Path.Combine(directory, ".package-sha256");
            if (!File.Exists(manifestPath)) return null;

            var manifest = JsonSerializer.Deserialize<ReleaseManifest>(File.ReadAllText(manifestPath), JsonOptions);
            if (manifest is null) return null;
            ValidateManifest(manifest, directory);
            var packageSha = File.Exists(packageShaPath) ? File.ReadAllText(packageShaPath).Trim() : "verified-on-import";
            return new ReleaseBundle(manifest, directory, packageSha);
        }
        catch
        {
            return null;
        }
    }

    private static void ValidateManifest(ReleaseManifest manifest, string directory)
    {
        ValidateVersion(manifest.Version);
        ValidateOptionalImage(
            manifest.BackendImage,
            $"thucluc-backend:{manifest.Version}",
            Path.Combine(directory, $"backend-{manifest.Version}.tar"),
            "Backend");
        ValidateOptionalImage(
            manifest.FrontendImage,
            $"thucluc-frontend:{manifest.Version}",
            Path.Combine(directory, $"frontend-{manifest.Version}.tar"),
            "Frontend");
        ValidateOptionalImage(
            manifest.OpsConsoleImage,
            $"thucluc-ops-console:{manifest.Version}",
            Path.Combine(directory, $"ops-console-{manifest.Version}.tar"),
            "Ops Console");
        ValidateOptionalImage(
            manifest.FlywayImage,
            $"thucluc-flyway:{manifest.Version}",
            Path.Combine(directory, $"flyway-{manifest.Version}.tar"),
            "Flyway");

        if (string.IsNullOrWhiteSpace(manifest.BackendImage)
            && string.IsNullOrWhiteSpace(manifest.FrontendImage)
            && string.IsNullOrWhiteSpace(manifest.OpsConsoleImage)
            && string.IsNullOrWhiteSpace(manifest.FlywayImage)
            && string.IsNullOrWhiteSpace(manifest.GotenbergImage)
            && string.IsNullOrWhiteSpace(manifest.MinioImage))
        {
            throw new InvalidOperationException("Gói release không chứa thành phần nào.");
        }

        ValidateOptionalImage(
            manifest.GotenbergImage,
            $"thucluc-gotenberg:{manifest.Version}",
            Path.Combine(directory, $"gotenberg-{manifest.Version}.tar"),
            "Gotenberg");
        ValidateOptionalImage(
            manifest.MinioImage,
            $"thucluc-minio:{manifest.Version}",
            Path.Combine(directory, $"minio-{manifest.Version}.tar"),
            "MinIO");
    }

    private static void ValidateOptionalImage(
        string imageName,
        string expectedImageName,
        string archivePath,
        string component)
    {
        var hasImageName = !string.IsNullOrWhiteSpace(imageName);
        var hasArchive = File.Exists(archivePath);
        if (!hasImageName && !hasArchive)
        {
            return;
        }

        if (!hasImageName || !hasArchive || !string.Equals(imageName, expectedImageName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Image {component} trong gói release không đầy đủ hoặc không đúng quy ước.");
        }
    }

    private static void ValidateVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version) || !VersionRegex().IsMatch(version))
        {
            throw new InvalidOperationException("Version trong release manifest không hợp lệ.");
        }
    }

    [GeneratedRegex("^([a-fA-F0-9]{64})\\s{2}(.+)$")]
    private static partial Regex ChecksumRegex();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")]
    private static partial Regex VersionRegex();
}
