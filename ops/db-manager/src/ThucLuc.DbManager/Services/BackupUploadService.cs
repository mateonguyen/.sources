using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ThucLuc.DbManager.Configuration;
using ThucLuc.DbManager.Models;

namespace ThucLuc.DbManager.Services;

public interface IBackupUploadService
{
    long MaximumFileSizeBytes { get; }

    Task<BackupUploadResult> ImportAsync(
        Stream source,
        string originalFileName,
        long declaredSize,
        string sourceSchema,
        CancellationToken cancellationToken = default);
}

public sealed partial class BackupUploadService(
    BackupStoragePathResolver storagePathResolver,
    IOperationJournal journal,
    ILogger<BackupUploadService> logger) : IBackupUploadService
{
    public long MaximumFileSizeBytes => 50L * 1024 * 1024 * 1024;

    private static readonly JsonSerializerOptions ManifestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<BackupUploadResult> ImportAsync(
        Stream source,
        string originalFileName,
        long declaredSize,
        string sourceSchema,
        CancellationToken cancellationToken = default)
    {
        Validate(source, originalFileName, declaredSize, sourceSchema);
        var storage = storagePathResolver.ResolveAndEnsure();
        var startedAt = DateTimeOffset.Now;
        var id = $"{startedAt:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..24];
        var safeFileName = $"{SanitizeFilePart(Path.GetFileNameWithoutExtension(originalFileName))}.DMP";
        var stagingDirectory = Path.Combine(storage.Inbox, id);
        var repositoryDirectory = Path.Combine(storage.Repository, "uploaded", id);
        var stagingDumpPath = Path.Combine(stagingDirectory, safeFileName);
        var operation = journal.AddRunning("Nhập file sao lưu", "uploaded", $"Đang nhận {safeFileName} từ máy trạm.");

        try
        {
            Directory.CreateDirectory(stagingDirectory);
            long written = 0;
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var destination = new FileStream(
                             stagingDumpPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             1024 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[1024 * 1024];
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    written += read;
                    if (written > MaximumFileSizeBytes)
                    {
                        throw new InvalidOperationException($"File vượt quá giới hạn {FormatBytes(MaximumFileSizeBytes)}.");
                    }

                    hasher.AppendData(buffer, 0, read);
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }

                await destination.FlushAsync(cancellationToken);
            }

            if (written == 0 || written != declaredSize)
            {
                throw new InvalidOperationException("File tải lên không đầy đủ hoặc dung lượng không khớp.");
            }

            var completedAt = DateTimeOffset.Now;
            var dumpVersion = await OracleDumpVersionReader.ReadAsync(stagingDumpPath, cancellationToken);
            var manifest = new BackupManifest
            {
                Id = id,
                EnvironmentKey = "uploaded",
                EnvironmentName = "File tải lên",
                Schema = sourceSchema.Trim().ToUpperInvariant(),
                DatabaseEndpoint = "Máy trạm",
                OracleDirectory = string.Empty,
                DumpFileName = safeFileName,
                LogFileName = string.Empty,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                State = BackupState.Succeeded,
                SizeBytes = written,
                Sha256 = Convert.ToHexString(hasher.GetHashAndReset()),
                DataPumpVersion = dumpVersion
            };

            await WriteManifestAsync(stagingDirectory, manifest, cancellationToken);
            Directory.CreateDirectory(Path.GetDirectoryName(repositoryDirectory)!);
            Directory.Move(stagingDirectory, repositoryDirectory);

            var message = $"Đã nhận {safeFileName} ({FormatBytes(written)}) và kiểm tra SHA-256.";
            journal.Complete(operation.Id, OperationState.Succeeded, message);
            logger.LogInformation("Đã nhập file DMP {FileName} thành bản sao {BackupId}", safeFileName, id);
            return new BackupUploadResult(manifest, repositoryDirectory, message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var message = ToSafeError(exception.Message);
            journal.Complete(operation.Id, OperationState.Failed, message);
            logger.LogError(exception, "Không thể nhập file DMP {FileName}", originalFileName);
            MoveToQuarantineIfPresent(stagingDirectory, storage.Quarantine, id);
            throw new InvalidOperationException(message, exception);
        }
    }

    private void Validate(Stream source, string fileName, long declaredSize, string sourceSchema)
    {
        if (!source.CanRead)
            throw new InvalidOperationException("Không đọc được file đã chọn.");
        if (!string.Equals(Path.GetExtension(fileName), ".dmp", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ chấp nhận file Oracle Data Pump có đuôi .dmp.");
        if (declaredSize <= 0)
            throw new InvalidOperationException("File .dmp đang rỗng.");
        if (declaredSize > MaximumFileSizeBytes)
            throw new InvalidOperationException($"File vượt quá giới hạn {FormatBytes(MaximumFileSizeBytes)}.");
        if (!OracleIdentifierRegex().IsMatch(sourceSchema))
            throw new InvalidOperationException("Schema nguồn trong file .dmp không hợp lệ.");
    }

    private static async Task WriteManifestAsync(
        string directory,
        BackupManifest manifest,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            Path.Combine(directory, "manifest.json"),
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        await JsonSerializer.SerializeAsync(stream, manifest, ManifestJsonOptions, cancellationToken);
    }

    private static void MoveToQuarantineIfPresent(string stagingDirectory, string quarantineRoot, string id)
    {
        if (!Directory.Exists(stagingDirectory)) return;
        try
        {
            Directory.Move(stagingDirectory, Path.Combine(quarantineRoot, $"upload-{id}"));
        }
        catch (IOException)
        {
            // Giữ nguyên staging để quản trị viên có thể kiểm tra; không che lỗi tải lên ban đầu.
        }
    }

    private static string SanitizeFilePart(string value)
    {
        var safe = FilePartRegex().Replace(value.Trim(), "_").Trim('_');
        return string.IsNullOrWhiteSpace(safe) ? "BACKUP" : safe[..Math.Min(safe.Length, 100)];
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024d * 1024 * 1024):0.##} GB",
        >= 1024L * 1024 => $"{bytes / (1024d * 1024):0.##} MB",
        >= 1024L => $"{bytes / 1024d:0.##} KB",
        _ => $"{bytes} B"
    };

    private static string ToSafeError(string message) => message
        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
        .FirstOrDefault()?.Trim() ?? "Không thể nhập file sao lưu.";

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_$#]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex OracleIdentifierRegex();

    [GeneratedRegex(@"[^A-Za-z0-9_-]+", RegexOptions.CultureInvariant)]
    private static partial Regex FilePartRegex();
}
