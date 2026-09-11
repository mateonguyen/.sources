using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using ThucLuc.DbManager.Configuration;
using ThucLuc.DbManager.Models;

namespace ThucLuc.DbManager.Services;

public interface IOracleBackupService
{
    Task<BackupRunResult> CreateAsync(
        ManagedEnvironmentOptions environment,
        CancellationToken cancellationToken = default);

    Task<BackupRunResult> CreateCompatibleAsync(
        ManagedEnvironmentOptions environment,
        string dataPumpVersion,
        CancellationToken cancellationToken = default);

    Task<BackupRunResult> CreateForTargetAsync(
        ManagedEnvironmentOptions environment,
        string dataPumpVersion,
        int? targetTimeZoneVersion,
        CancellationToken cancellationToken = default);

    Task<BackupRunResult> CreateSafetyBackupAsync(
        ManagedEnvironmentOptions environment,
        string sysPassword,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupManifest>> GetRecentAsync(
        int count = 20,
        CancellationToken cancellationToken = default);
}

public sealed partial class OracleBackupService(
    BackupStoragePathResolver backupStoragePathResolver,
    IToolLocator toolLocator,
    IOracleCredentialResolver credentialResolver,
    IOperationJournal journal,
    IOptions<OpsOptions> options,
    ILogger<OracleBackupService> logger) : IOracleBackupService
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _environmentLocks =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<BackupRunResult> CreateAsync(
        ManagedEnvironmentOptions environment,
        CancellationToken cancellationToken = default)
    {
        return await CreateCoreAsync(
            environment,
            suppliedCredential: null,
            useSysDba: false,
            requireBackupPermission: true,
            isSafetyBackup: false,
            dataPumpVersionOverride: null,
            targetTimeZoneVersion: null,
            cancellationToken);
    }

    public async Task<BackupRunResult> CreateCompatibleAsync(
        ManagedEnvironmentOptions environment,
        string dataPumpVersion,
        CancellationToken cancellationToken = default)
    {
        return await CreateCoreAsync(
            environment,
            suppliedCredential: null,
            useSysDba: false,
            requireBackupPermission: true,
            isSafetyBackup: false,
            dataPumpVersionOverride: dataPumpVersion,
            targetTimeZoneVersion: null,
            cancellationToken);
    }

    public async Task<BackupRunResult> CreateForTargetAsync(
        ManagedEnvironmentOptions environment,
        string dataPumpVersion,
        int? targetTimeZoneVersion,
        CancellationToken cancellationToken = default)
    {
        return await CreateCoreAsync(
            environment,
            suppliedCredential: null,
            useSysDba: false,
            requireBackupPermission: true,
            isSafetyBackup: false,
            dataPumpVersionOverride: dataPumpVersion,
            targetTimeZoneVersion,
            cancellationToken);
    }

    public async Task<BackupRunResult> CreateSafetyBackupAsync(
        ManagedEnvironmentOptions environment,
        string sysPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sysPassword))
        {
            throw new InvalidOperationException("Hãy nhập mật khẩu SYS để sao lưu schema đích trước khi thay thế.");
        }

        return await CreateCoreAsync(
            environment,
            new OracleCredential("SYS", sysPassword),
            useSysDba: true,
            requireBackupPermission: false,
            isSafetyBackup: true,
            dataPumpVersionOverride: null,
            targetTimeZoneVersion: null,
            cancellationToken);
    }

    private async Task<BackupRunResult> CreateCoreAsync(
        ManagedEnvironmentOptions environment,
        OracleCredential? suppliedCredential,
        bool useSysDba,
        bool requireBackupPermission,
        bool isSafetyBackup,
        string? dataPumpVersionOverride,
        int? targetTimeZoneVersion,
        CancellationToken cancellationToken)
    {
        var environmentLock = _environmentLocks.GetOrAdd(environment.Key, _ => new SemaphoreSlim(1, 1));
        if (!await environmentLock.WaitAsync(0, cancellationToken))
        {
            throw new InvalidOperationException($"{environment.DisplayName} đang được sao lưu. Vui lòng chờ thao tác hiện tại hoàn tất.");
        }

        OperationEntry? operation = null;
        string? backupDirectory = null;
        BackupManifest? manifest = null;
        try
        {
            ValidateEnvironment(environment, requireBackupPermission);
            var credential = suppliedCredential;
            if (credential is null)
            {
                var credentialResolution = await credentialResolver.ResolveAsync(
                    environment,
                    includeCredential: true,
                    cancellationToken);
                credential = credentialResolution.Credential
                    ?? throw new InvalidOperationException(
                        "Chưa có tài khoản DB nguồn. Hãy nhập tên đăng nhập và mật khẩu ở khu vực kết nối phía trên.");
            }

            var sourceInfo = await ValidateOracleSourceAsync(
                environment,
                credential,
                useSysDba,
                cancellationToken);
            var dataPumpVersion = ResolveDataPumpVersion(
                dataPumpVersionOverride ?? options.Value.DataPumpExportVersion,
                sourceInfo.DatabaseVersion);
            if (sourceInfo.TimeZoneFileVersion.HasValue
                && targetTimeZoneVersion.HasValue
                && sourceInfo.TimeZoneFileVersion.Value > targetTimeZoneVersion.Value)
            {
                throw new InvalidOperationException(
                    $"Không thể tạo bản sao cho DB đích: TSTZ nguồn {sourceInfo.TimeZoneFileVersion.Value} > " +
                    $"đích {targetTimeZoneVersion.Value}. VERSION của Data Pump không thay đổi timezone file. " +
                    "DBA cần nâng timezone file của DB đích lên cùng hoặc cao hơn DB nguồn.");
            }

            var startedAt = DateTimeOffset.Now;
            var id = $"{startedAt:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..24];
            var safeEnvironmentKey = SanitizeFilePart(environment.Key);
            var safeSchema = SanitizeFilePart(environment.Oracle.Schema);
            var dumpFileName = $"THUCLUC_{safeEnvironmentKey}_{safeSchema}_{startedAt:yyyyMMdd_HHmmss}_{id[^6..]}.dmp".ToUpperInvariant();
            var serverLogFileName = Path.ChangeExtension(dumpFileName, ".log");
            backupDirectory = Path.Combine(
                backupStoragePathResolver.ResolveAndEnsure().Repository,
                safeEnvironmentKey,
                id);
            Directory.CreateDirectory(backupDirectory);

            manifest = new BackupManifest
            {
                Id = id,
                EnvironmentKey = environment.Key,
                EnvironmentName = environment.DisplayName,
                Schema = environment.Oracle.Schema.ToUpperInvariant(),
                DatabaseEndpoint = $"{environment.Oracle.Host}:{environment.Oracle.Port}/{environment.Oracle.ServiceName}",
                OracleDirectory = environment.Oracle.DataPumpDirectory.ToUpperInvariant(),
                OracleDirectoryPath = sourceInfo.DirectoryPath,
                SourceOracleVersion = sourceInfo.DatabaseVersion,
                DataPumpVersion = dataPumpVersion,
                SourceTimeZoneVersion = sourceInfo.TimeZoneFileVersion,
                DumpFileName = dumpFileName,
                StartedAt = startedAt,
                State = BackupState.Running,
                IsSafetyBackup = isSafetyBackup
            };
            await WriteManifestAsync(backupDirectory, manifest, cancellationToken);
            operation = journal.AddRunning("Sao lưu", environment.Key, $"Đang tạo {dumpFileName}");

            await RunDataPumpAsync(
                environment,
                credential,
                dumpFileName,
                serverLogFileName,
                dataPumpVersion,
                useSysDba,
                cancellationToken);
            var localLogPath = Path.Combine(backupDirectory, manifest.LogFileName);
            string? logDownloadWarning = null;
            try
            {
                await DownloadOracleFileAsync(
                    environment,
                    credential,
                    serverLogFileName,
                    localLogPath,
                    sourceInfo.DirectoryPath,
                    useSysDba,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                logDownloadWarning = $"Không tải được export log: {exception.Message}";
                logger.LogWarning(
                    exception,
                    "Data Pump đã export xong tại {LogPath} nhưng không tải được log",
                    CombineOraclePath(sourceInfo.DirectoryPath, serverLogFileName));
            }

            var localDumpPath = Path.Combine(backupDirectory, dumpFileName);
            try
            {
                await DownloadOracleFileAsync(
                    environment,
                    credential,
                    dumpFileName,
                    localDumpPath,
                    sourceInfo.DirectoryPath,
                    useSysDba,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Data Pump đã export xong nhưng không tải được file dump về máy: {exception.Message}",
                    exception);
            }
            var fileInfo = new FileInfo(localDumpPath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                throw new InvalidOperationException("Oracle đã chạy export nhưng file sao lưu tải về máy đang rỗng.");
            }

            var checksum = await CalculateSha256Async(localDumpPath, cancellationToken);
            var actualDumpVersion = await OracleDumpVersionReader.ReadAsync(localDumpPath, cancellationToken);
            manifest = manifest with
            {
                CompletedAt = DateTimeOffset.Now,
                State = BackupState.Succeeded,
                SizeBytes = fileInfo.Length,
                Sha256 = checksum,
                DataPumpVersion = actualDumpVersion ?? dataPumpVersion
            };
            await WriteManifestAsync(backupDirectory, manifest, cancellationToken);
            journal.Complete(operation.Id, OperationState.Succeeded, $"Đã tạo {dumpFileName} ({FormatBytes(fileInfo.Length)}).");
            logger.LogInformation(
                "Backup {BackupId} của {EnvironmentKey} hoàn tất, dung lượng {SizeBytes} byte",
                id,
                environment.Key,
                fileInfo.Length);

            var resultMessage = isSafetyBackup
                ? $"Đã tạo bản sao an toàn {FormatBytes(fileInfo.Length)} và kiểm tra SHA-256."
                : $"Đã tạo bản sao lưu {FormatBytes(fileInfo.Length)} và kiểm tra SHA-256.";
            if (logDownloadWarning is not null)
            {
                resultMessage += $" {logDownloadWarning}";
            }
            return new BackupRunResult(true, manifest, resultMessage);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Sao lưu môi trường {EnvironmentKey} thất bại", environment.Key);
            if (manifest is not null && backupDirectory is not null)
            {
                manifest = manifest with
                {
                    CompletedAt = DateTimeOffset.Now,
                    State = BackupState.Failed,
                    Error = ToSafeError(exception.Message)
                };
                await WriteManifestAsync(backupDirectory, manifest, CancellationToken.None);
            }

            if (operation is not null)
            {
                journal.Complete(operation.Id, OperationState.Failed, ToSafeError(exception.Message));
            }

            throw new InvalidOperationException(ToSafeError(exception.Message), exception);
        }
        finally
        {
            environmentLock.Release();
        }
    }

    public async Task<IReadOnlyList<BackupManifest>> GetRecentAsync(
        int count = 20,
        CancellationToken cancellationToken = default)
    {
        var storage = backupStoragePathResolver.ResolveAndEnsure();
        var searchRoots = new[] { storage.Repository, storage.LegacyRoot }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists);
        var manifests = new List<BackupManifest>();
        foreach (var manifestPath in searchRoots.SelectMany(root =>
                     Directory.EnumerateFiles(root, "manifest.json", SearchOption.AllDirectories)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var stream = File.OpenRead(manifestPath);
                var manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(
                    stream,
                    ManifestJsonOptions,
                    cancellationToken);
                if (manifest is not null)
                {
                    if (string.IsNullOrWhiteSpace(manifest.DataPumpVersion))
                    {
                        var dumpPath = Path.Combine(Path.GetDirectoryName(manifestPath)!, manifest.DumpFileName);
                        if (File.Exists(dumpPath))
                        {
                            manifest = manifest with
                            {
                                DataPumpVersion = await OracleDumpVersionReader.ReadAsync(dumpPath, cancellationToken)
                            };
                        }
                    }

                    manifests.Add(manifest);
                }
            }
            catch (Exception exception) when (exception is IOException or JsonException)
            {
                logger.LogWarning(exception, "Bỏ qua manifest không đọc được tại {ManifestPath}", manifestPath);
            }
        }

        return manifests
            .OrderByDescending(item => item.StartedAt)
            .Take(Math.Clamp(count, 1, 100))
            .ToList();
    }

    private static void ValidateEnvironment(
        ManagedEnvironmentOptions environment,
        bool requireBackupPermission)
    {
        if (!environment.Enabled)
        {
            throw new InvalidOperationException("Kết nối đang tắt. Hãy bật kết nối trước khi sao lưu.");
        }

        if (requireBackupPermission && !environment.Safety.AllowBackup)
        {
            throw new InvalidOperationException("Kết nối này chưa được cho phép sao lưu trong Tùy chọn nâng cao.");
        }

        if (string.IsNullOrWhiteSpace(environment.Key)
            || string.IsNullOrWhiteSpace(environment.Oracle.Host)
            || environment.Oracle.Port is <= 0 or > 65535
            || string.IsNullOrWhiteSpace(environment.Oracle.ServiceName)
            || string.IsNullOrWhiteSpace(environment.Oracle.Schema)
            || string.IsNullOrWhiteSpace(environment.Oracle.DataPumpDirectory))
        {
            throw new InvalidOperationException("Thông tin DB nguồn chưa đầy đủ. Kiểm tra Host, Port, Service/PDB, Schema và Oracle Directory.");
        }

        ValidateOracleIdentifier(environment.Oracle.Schema, "Schema");
        ValidateOracleIdentifier(environment.Oracle.DataPumpDirectory, "Oracle Directory");
    }

    private static async Task<(string DirectoryPath, string DatabaseVersion, int? TimeZoneFileVersion)> ValidateOracleSourceAsync(
        ManagedEnvironmentOptions environment,
        OracleCredential credential,
        bool useSysDba,
        CancellationToken cancellationToken)
    {
        await using var connection = new OracleConnection(BuildConnectionString(environment, credential, useSysDba));
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM ALL_USERS WHERE USERNAME = :schema_name) AS SCHEMA_COUNT,
                (SELECT MAX(DIRECTORY_PATH) FROM ALL_DIRECTORIES WHERE DIRECTORY_NAME = :directory_name) AS DIRECTORY_PATH,
                (SELECT MAX(VERSION) FROM PRODUCT_COMPONENT_VERSION WHERE PRODUCT LIKE 'Oracle Database%') AS DATABASE_VERSION
            FROM DUAL
            """;
        command.Parameters.Add("schema_name", OracleDbType.Varchar2).Value = environment.Oracle.Schema.ToUpperInvariant();
        command.Parameters.Add("directory_name", OracleDbType.Varchar2).Value = environment.Oracle.DataPumpDirectory.ToUpperInvariant();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        if (reader.GetInt32(0) == 0)
        {
            throw new InvalidOperationException($"Không tìm thấy schema {environment.Oracle.Schema} trên DB nguồn.");
        }

        if (reader.IsDBNull(1))
        {
            throw new InvalidOperationException(
                $"Không tìm thấy Oracle Directory {environment.Oracle.DataPumpDirectory} hoặc tài khoản chưa được cấp quyền truy cập.");
        }

        return (reader.GetString(1), reader.GetString(2), await ReadTimeZoneFileVersionAsync(connection, cancellationToken));
    }

    private static async Task<int?> ReadTimeZoneFileVersionAsync(
        OracleConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT VERSION FROM V$TIMEZONE_FILE";
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        }
        catch (OracleException exception) when (exception.Number is 942 or 1031)
        {
            return null;
        }
    }

    private static async Task RunDataPumpAsync(
        ManagedEnvironmentOptions environment,
        OracleCredential credential,
        string dumpFileName,
        string serverLogFileName,
        string dataPumpVersion,
        bool useSysDba,
        CancellationToken cancellationToken)
    {
        await using var connection = new OracleConnection(BuildConnectionString(environment, credential, useSysDba));
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = 60 * 60;
        command.CommandText = """
            DECLARE
                l_handle NUMBER := NULL;
                l_state  VARCHAR2(30);
            BEGIN
                l_handle := DBMS_DATAPUMP.OPEN(
                    operation => 'EXPORT',
                    job_mode  => 'SCHEMA',
                    job_name  => :job_name,
                    version   => :data_pump_version);

                DBMS_DATAPUMP.ADD_FILE(
                    handle    => l_handle,
                    filename  => :dump_file_name,
                    directory => :directory_name,
                    filetype  => DBMS_DATAPUMP.KU$_FILE_TYPE_DUMP_FILE,
                    reusefile => 0);

                DBMS_DATAPUMP.ADD_FILE(
                    handle    => l_handle,
                    filename  => :log_file_name,
                    directory => :directory_name,
                    filetype  => DBMS_DATAPUMP.KU$_FILE_TYPE_LOG_FILE,
                    reusefile => 0);

                DBMS_DATAPUMP.METADATA_FILTER(
                    handle => l_handle,
                    name   => 'SCHEMA_EXPR',
                    value  => :schema_expression);

                DBMS_DATAPUMP.START_JOB(l_handle);
                DBMS_DATAPUMP.WAIT_FOR_JOB(l_handle, l_state);
                :job_state := l_state;
            EXCEPTION
                WHEN OTHERS THEN
                    IF l_handle IS NOT NULL THEN
                        BEGIN
                            DBMS_DATAPUMP.DETACH(l_handle);
                        EXCEPTION
                            WHEN OTHERS THEN NULL;
                        END;
                    END IF;
                    RAISE;
            END;
            """;
        var jobName = $"TL_EXP_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}"[..28].ToUpperInvariant();
        var schema = environment.Oracle.Schema.ToUpperInvariant();
        command.Parameters.Add("job_name", OracleDbType.Varchar2).Value = jobName;
        command.Parameters.Add("data_pump_version", OracleDbType.Varchar2).Value = dataPumpVersion;
        command.Parameters.Add("dump_file_name", OracleDbType.Varchar2).Value = dumpFileName;
        command.Parameters.Add("directory_name", OracleDbType.Varchar2).Value = environment.Oracle.DataPumpDirectory.ToUpperInvariant();
        command.Parameters.Add("log_file_name", OracleDbType.Varchar2).Value = serverLogFileName;
        command.Parameters.Add("schema_expression", OracleDbType.Varchar2).Value = $"IN ('{schema}')";
        var stateParameter = command.Parameters.Add("job_state", OracleDbType.Varchar2, 30);
        stateParameter.Direction = ParameterDirection.Output;

        await command.ExecuteNonQueryAsync(cancellationToken);
        var state = Convert.ToString(stateParameter.Value)?.Trim();
        if (!string.Equals(state, "COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Oracle Data Pump kết thúc ở trạng thái {state ?? "không xác định"}.");
        }
    }

    private static string ResolveDataPumpVersion(string value, string sourceVersion)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.Equals(normalized, "COMPATIBLE", StringComparison.OrdinalIgnoreCase))
        {
            return "COMPATIBLE";
        }

        if (!Regex.IsMatch(normalized, @"^\d{1,2}(\.\d{1,2})?$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException("Ops:DataPumpExportVersion phải là COMPATIBLE hoặc có dạng 18.0, 19.0.");
        }

        var requestedRelease = OracleDumpVersionReader.GetRelease(normalized);
        var sourceRelease = OracleDumpVersionReader.GetRelease(sourceVersion);
        if (requestedRelease is not null && sourceRelease is not null && requestedRelease > sourceRelease)
        {
            return sourceRelease.ToString(2);
        }

        return normalized;
    }

    private async Task DownloadOracleFileAsync(
        ManagedEnvironmentOptions environment,
        OracleCredential credential,
        string oracleFileName,
        string localFilePath,
        string oracleDirectoryPath,
        bool useSysDba,
        CancellationToken cancellationToken)
    {
        var oracleFilePath = CombineOraclePath(oracleDirectoryPath, oracleFileName);
        if (IsLocalHost(environment.Oracle.Host) && File.Exists(oracleFilePath))
        {
            await CopyLocalOracleFileAsync(oracleFilePath, localFilePath, cancellationToken);
            return;
        }

        var dockerPath = toolLocator.Find("docker");
        if (dockerPath is not null && IsLocalHost(environment.Oracle.Host))
        {
            var container = await FindOracleContainerAsync(
                dockerPath,
                environment.Oracle.Port,
                cancellationToken);
            if (container is not null)
            {
                await CopyFromOracleContainerAsync(
                    dockerPath,
                    container,
                    oracleFilePath,
                    localFilePath,
                    cancellationToken);
                return;
            }
        }

        await using var connection = new OracleConnection(BuildConnectionString(environment, credential, useSysDba));
        await connection.OpenAsync(cancellationToken);
        using var oracleFile = new OracleBFile(
            connection,
            environment.Oracle.DataPumpDirectory.ToUpperInvariant(),
            oracleFileName);
        if (!oracleFile.FileExists)
        {
            throw new InvalidOperationException($"Oracle báo export thành công nhưng không tìm thấy file {oracleFileName} trong Oracle Directory.");
        }

        oracleFile.OpenFile();
        try
        {
            await using var destination = new FileStream(
                localFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[1024 * 1024];
            var remaining = oracleFile.Length;
            while (remaining > 0)
            {
                var requested = (int)Math.Min(buffer.LongLength, remaining);
                var bytesRead = oracleFile.Read(buffer, 0, requested);
                if (bytesRead <= 0)
                {
                    throw new EndOfStreamException(
                        $"File Oracle {oracleFileName} kết thúc sớm, còn thiếu {remaining} byte.");
                }
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                remaining -= bytesRead;
            }
            await destination.FlushAsync(cancellationToken);
        }
        finally
        {
            try
            {
                if (oracleFile.IsOpen)
                {
                    oracleFile.CloseFile();
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or OracleException)
            {
                // Không để lỗi đóng locator che mất kết quả đọc file hoặc lỗi gốc.
            }
        }
    }

    private static async Task CopyLocalOracleFileAsync(
        string oracleFilePath,
        string localFilePath,
        CancellationToken cancellationToken)
    {
        await using var source = new FileStream(
            oracleFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = new FileStream(
            localFilePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await source.CopyToAsync(destination, 1024 * 1024, cancellationToken);
        await destination.FlushAsync(cancellationToken);
    }

    private static async Task<string?> FindOracleContainerAsync(
        string dockerPath,
        int hostPort,
        CancellationToken cancellationToken)
    {
        var result = await RunProcessAsync(
            dockerPath,
            ["ps", "--format", "{{.ID}}|{{.Names}}|{{.Ports}}"],
            TimeSpan.FromSeconds(15),
            cancellationToken);
        if (result.ExitCode != 0)
        {
            return null;
        }

        var portMarker = $":{hostPort}->";
        foreach (var line in result.Output.Split(
                     new[] { "\r\n", "\n" },
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('|', 3);
            if (parts.Length == 3 && parts[2].Contains(portMarker, StringComparison.OrdinalIgnoreCase))
            {
                return parts[1];
            }
        }

        return null;
    }

    private static async Task CopyFromOracleContainerAsync(
        string dockerPath,
        string container,
        string oracleFilePath,
        string localFilePath,
        CancellationToken cancellationToken)
    {
        var result = await RunProcessAsync(
            dockerPath,
            ["cp", $"{container}:{oracleFilePath}", localFilePath],
            TimeSpan.FromMinutes(10),
            cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Không sao chép được file từ container Oracle {container}: {result.Output.Trim()}");
        }
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeoutValue,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutValue);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            throw;
        }

        var output = string.Join(
            Environment.NewLine,
            new[] { await outputTask, await errorTask }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return new ProcessResult(process.ExitCode, output);
    }

    private async Task WriteManifestAsync(
        string backupDirectory,
        BackupManifest manifest,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(backupDirectory, "manifest.json");
        var temporaryPath = manifestPath + ".tmp";
        await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, manifest, ManifestJsonOptions, cancellationToken);
        }

        File.Move(temporaryPath, manifestPath, overwrite: true);
    }

    private static string BuildConnectionString(
        ManagedEnvironmentOptions environment,
        OracleCredential credential,
        bool useSysDba)
    {
        var builder = new OracleConnectionStringBuilder
        {
            UserID = credential.UserName,
            Password = credential.Password,
            DataSource = $"{environment.Oracle.Host}:{environment.Oracle.Port}/{environment.Oracle.ServiceName}",
            Pooling = false,
            ConnectionTimeout = 10
        };
        if (useSysDba)
        {
            builder.DBAPrivilege = "SYSDBA";
        }

        return builder.ConnectionString;
    }

    private static async Task<string> CalculateSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static void ValidateOracleIdentifier(string value, string fieldName)
    {
        if (!OracleIdentifierRegex().IsMatch(value))
        {
            throw new InvalidOperationException($"{fieldName} không hợp lệ. Chỉ dùng chữ, số và các ký tự _, $, #; phải bắt đầu bằng chữ.");
        }
    }

    private static string SanitizeFilePart(string value)
    {
        var safe = FilePartRegex().Replace(value.Trim(), "_").Trim('_');
        return string.IsNullOrWhiteSpace(safe) ? "DB" : safe;
    }

    private static string ToSafeError(string message)
    {
        var firstLine = message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstLine) ? "Sao lưu không thành công." : firstLine.Trim();
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024d * 1024 * 1024):0.##} GB",
        >= 1024L * 1024 => $"{bytes / (1024d * 1024):0.##} MB",
        >= 1024L => $"{bytes / 1024d:0.##} KB",
        _ => $"{bytes} B"
    };

    private static string CombineOraclePath(string directoryPath, string fileName)
    {
        var separator = directoryPath.Contains('\\') ? "\\" : "/";
        return $"{directoryPath.TrimEnd('/', '\\')}{separator}{fileName}";
    }

    private static bool IsLocalHost(string host) => host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || host.Equals("::1", StringComparison.OrdinalIgnoreCase);

    private sealed record ProcessResult(int ExitCode, string Output);

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_$#]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex OracleIdentifierRegex();

    [GeneratedRegex(@"[^A-Za-z0-9_-]+", RegexOptions.CultureInvariant)]
    private static partial Regex FilePartRegex();

}
