using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using ThucLuc.DbManager.Configuration;
using ThucLuc.DbManager.Models;

namespace ThucLuc.DbManager.Services;

public interface IOracleRestoreService
{
    Task<OracleRestoreTargetInspection> InspectExistingAsync(
        ManagedEnvironmentOptions target,
        string targetSchema,
        string schemaPassword,
        CancellationToken cancellationToken = default);

    Task<RestoreRunResult> RestoreAsync(RestoreRequest request, CancellationToken cancellationToken = default);
}

public sealed partial class OracleRestoreService(
    BackupStoragePathResolver storagePathResolver,
    IOracleBackupService backupService,
    IOracleSchemaProvisioningService schemaProvisioningService,
    IOracleCredentialResolver credentialResolver,
    IOracleCredentialStore credentialStore,
    IEnvironmentConfigurationService configurationService,
    IOperationJournal journal,
    ILogger<OracleRestoreService> logger) : IOracleRestoreService
{
    public async Task<OracleRestoreTargetInspection> InspectExistingAsync(
        ManagedEnvironmentOptions target,
        string targetSchema,
        string schemaPassword,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(targetSchema, "Schema đích");
        if (string.IsNullOrWhiteSpace(schemaPassword))
        {
            throw new InvalidOperationException("Hãy nhập mật khẩu schema đích.");
        }

        var environment = Clone(target);
        environment.Oracle.Schema = targetSchema.Trim().ToUpperInvariant();
        var credential = new OracleCredential(environment.Oracle.Schema, schemaPassword);
        try
        {
            var targetInfo = await InspectTargetAsync(environment, credential, cancellationToken);
            var versionInfo = await ReadTargetVersionAsync(environment, credential, cancellationToken);
            await using var connection = new OracleConnection(BuildConnectionString(environment, credential));
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT SYS_CONTEXT('USERENV', 'DB_NAME') FROM DUAL";
            var databaseName = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken)) ?? environment.Oracle.ServiceName;
            return new OracleRestoreTargetInspection(
                databaseName,
                versionInfo.Version,
                versionInfo.Compatibility,
                versionInfo.TimeZoneFileVersion,
                targetInfo.ObjectCount,
                targetInfo.Name);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException(GetSafeMessage(exception), exception);
        }
    }

    public async Task<RestoreRunResult> RestoreAsync(
        RestoreRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var target = Clone(request.TargetEnvironment);
        target.Oracle.Schema = request.TargetSchema.Trim().ToUpperInvariant();
        var operation = journal.AddRunning(
            "Phục hồi",
            target.Key,
            $"Đang phục hồi {request.Backup.Schema} vào {target.Oracle.Schema}.");
        var schemaWasCreated = false;
        BackupManifest? safetyBackup = null;
        int? targetTimeZoneFileVersion = null;

        try
        {
            var localDumpPath = FindDumpPath(request.Backup);
            await VerifyDumpAsync(localDumpPath, request.Backup, cancellationToken);
            var dumpVersion = request.Backup.DataPumpVersion
                ?? await OracleDumpVersionReader.ReadAsync(localDumpPath, cancellationToken);

            OracleCredential credential;
            if (request.TargetMode == RestoreTargetMode.CreateNew)
            {
                var targetInspection = await schemaProvisioningService.InspectAsync(
                    target,
                    request.SysPassword,
                    cancellationToken);
                EnsureCompatibleVersions(dumpVersion, targetInspection.DatabaseCompatibility);
                targetTimeZoneFileVersion = targetInspection.TimeZoneFileVersion;
                EnsureCompatibleTimeZone(request.Backup, targetTimeZoneFileVersion);
                var created = await schemaProvisioningService.CreateAsync(
                    target,
                    request.SysPassword,
                    request.SchemaPassword,
                    request.DefaultTablespace,
                    cancellationToken);
                credential = new OracleCredential(created.Schema, request.SchemaPassword);
                await credentialStore.SaveAsync(target.Key, credential, cancellationToken);
                schemaWasCreated = true;
                if (!string.IsNullOrWhiteSpace(created.DataPumpDirectoryName))
                {
                    target.Oracle.DataPumpDirectory = created.DataPumpDirectoryName;
                }
            }
            else if (request.TargetMode == RestoreTargetMode.ReplaceExisting)
            {
                if (!string.Equals(
                        request.ReplacementConfirmation.Trim(),
                        target.Oracle.Schema,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Hãy nhập chính xác tên schema {target.Oracle.Schema} để xác nhận thay thế.");
                }

                var inspection = await schemaProvisioningService.InspectAsync(
                    target,
                    request.SysPassword,
                    cancellationToken);
                EnsureCompatibleVersions(dumpVersion, inspection.DatabaseCompatibility);
                targetTimeZoneFileVersion = inspection.TimeZoneFileVersion;
                EnsureCompatibleTimeZone(request.Backup, targetTimeZoneFileVersion);
                if (!inspection.SchemaExists)
                {
                    throw new InvalidOperationException(
                        $"Schema {target.Oracle.Schema} không tồn tại. Hãy chọn chế độ tạo schema mới.");
                }

                if (string.IsNullOrWhiteSpace(inspection.DataPumpDirectoryName))
                {
                    throw new InvalidOperationException("DB đích chưa có Oracle Data Pump Directory phù hợp.");
                }

                target.Oracle.DataPumpDirectory = inspection.DataPumpDirectoryName;
                var backupResult = await backupService.CreateSafetyBackupAsync(
                    target,
                    request.SysPassword,
                    cancellationToken);
                if (!backupResult.Succeeded || backupResult.Manifest.State != BackupState.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Không tạo được bản sao an toàn của schema hiện tại nên hệ thống không thực hiện thay thế.");
                }

                safetyBackup = backupResult.Manifest;
                var replaced = await schemaProvisioningService.ReplaceAsync(
                    target,
                    request.SysPassword,
                    request.SchemaPassword,
                    request.DefaultTablespace,
                    cancellationToken);
                credential = new OracleCredential(replaced.Schema, request.SchemaPassword);
                schemaWasCreated = true;
                if (!string.IsNullOrWhiteSpace(replaced.DataPumpDirectoryName))
                {
                    target.Oracle.DataPumpDirectory = replaced.DataPumpDirectoryName;
                }
            }
            else
            {
                credential = !string.IsNullOrWhiteSpace(request.SchemaPassword)
                    ? new OracleCredential(target.Oracle.Schema, request.SchemaPassword)
                    : (await credentialResolver.ResolveAsync(target, includeCredential: true, cancellationToken)).Credential
                        ?? throw new InvalidOperationException("Hãy nhập mật khẩu của schema đích.");
                var targetVersion = await ReadTargetVersionAsync(target, credential, cancellationToken);
                EnsureCompatibleVersions(dumpVersion, targetVersion.Compatibility);
                targetTimeZoneFileVersion = targetVersion.TimeZoneFileVersion;
                EnsureCompatibleTimeZone(request.Backup, targetTimeZoneFileVersion);
            }

            var directory = await InspectTargetAsync(target, credential, cancellationToken);
            if (directory.ObjectCount > 0)
            {
                throw new TargetSchemaNotEmptyException(target.Oracle.Schema, directory.ObjectCount);
            }

            target.Oracle.DataPumpDirectory = directory.Name;
            var transferFileName = $"TL_RESTORE_{SanitizeFilePart(request.Backup.Id)}.DMP".ToUpperInvariant();
            await UploadDumpAsync(
                target,
                credential,
                localDumpPath,
                transferFileName,
                cancellationToken);
            await VerifyUploadedDumpAsync(
                target,
                credential,
                transferFileName,
                new FileInfo(localDumpPath).Length,
                cancellationToken);

            var logFileName = Path.ChangeExtension(transferFileName, ".LOG");
            var warnings = new List<string>();
            string? localLogPath = null;
            var workDirectory = Path.Combine(
                storagePathResolver.ResolveAndEnsure().Work,
                $"restore-{DateTime.Now:yyyyMMdd-HHmmss}-{SanitizeFilePart(target.Key)}");
            Directory.CreateDirectory(workDirectory);
            try
            {
                await RunImportAsync(
                    target,
                    credential,
                    request.Backup.Schema,
                    transferFileName,
                    logFileName,
                    cancellationToken);
            }
            catch (Exception importException) when (importException is not OperationCanceledException)
            {
                if (IsTimeZoneVersionMismatch(importException))
                {
                    var mismatch = ReadTimeZoneMismatch(importException);
                    var versions = mismatch is null
                        ? string.Empty
                        : $" TSTZ nguồn {mismatch.Value.Source} > đích {mismatch.Value.Target}.";
                    throw new InvalidOperationException(
                        $"Không thể phục hồi do timezone file không tương thích.{versions} " +
                        "VERSION của Data Pump không thay đổi timezone file. DBA cần nâng timezone file của DB đích " +
                        "lên cùng hoặc cao hơn DB nguồn rồi kiểm tra và phục hồi lại.",
                        importException);
                }

                var candidateLogPath = Path.Combine(workDirectory, logFileName);
                try
                {
                    await DownloadLogAsync(target, credential, logFileName, candidateLogPath, cancellationToken);
                    var oracleDetails = ReadOracleErrors(candidateLogPath);
                    if (!string.IsNullOrWhiteSpace(oracleDetails))
                    {
                        throw new InvalidOperationException(
                            $"Data Pump không thể khởi chạy. Chi tiết từ import log: {oracleDetails}",
                            importException);
                    }
                }
                catch (InvalidOperationException diagnosticException)
                    when (diagnosticException.InnerException == importException)
                {
                    throw;
                }
                catch (Exception diagnosticException)
                {
                    logger.LogWarning(diagnosticException, "Không đọc được import log sau khi Data Pump lỗi");
                }

                throw;
            }

            var completedTarget = await InspectTargetAsync(target, credential, cancellationToken);

            try
            {
                var candidateLogPath = Path.Combine(workDirectory, logFileName);
                await DownloadLogAsync(target, credential, logFileName, candidateLogPath, cancellationToken);
                if (File.Exists(candidateLogPath)) localLogPath = candidateLogPath;
            }
            catch (Exception exception)
            {
                warnings.Add(
                    "Không tải được file nhật ký import về máy. Việc này không ảnh hưởng dữ liệu đã phục hồi.");
                logger.LogWarning(exception, "Import đã hoàn tất nhưng không tải được log {LogFileName}", logFileName);
            }

            try
            {
                await configurationService.SaveAsync(target, cancellationToken);
                await credentialStore.SaveAsync(target.Key, credential, cancellationToken);
            }
            catch (Exception exception)
            {
                warnings.Add("Dữ liệu đã phục hồi nhưng chưa lưu được cấu hình/tài khoản cục bộ.");
                logger.LogWarning(exception, "Import đã hoàn tất nhưng không lưu được cấu hình đích");
            }

            var message =
                $"Data Pump đã hoàn tất. Đã phục hồi {request.Backup.Schema} vào {target.Oracle.Schema} " +
                $"trên {target.Oracle.ServiceName}; schema đích hiện có {completedTarget.ObjectCount} đối tượng.";
            journal.Complete(operation.Id, OperationState.Succeeded, message);
            return new RestoreRunResult(
                true,
                message,
                localLogPath,
                safetyBackup,
                warnings,
                completedTarget.ObjectCount);
        }
        catch (TargetSchemaNotEmptyException exception)
        {
            journal.Complete(operation.Id, OperationState.Failed, exception.Message);
            logger.LogWarning(
                exception,
                "Schema {Schema} không trống nên không thể phục hồi ở chế độ hiện tại",
                exception.Schema);
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var message = GetSafeMessage(exception);
            if (request.TargetMode == RestoreTargetMode.ReplaceExisting && safetyBackup is not null)
            {
                message += FindOracleException(exception)?.Number == 1940
                    ? $" Schema đích chưa bị xóa. Bản sao an toàn {safetyBackup.Id} vẫn được giữ trong kho."
                    : " Thao tác thay thế đã bắt đầu; schema đích có thể đã được thay đổi. " +
                      $"Bản sao an toàn {safetyBackup.Id} vẫn được giữ trong kho để phục hồi lại.";
            }
            else if (schemaWasCreated)
            {
                message += $" Schema {target.Oracle.Schema} đã được tạo thành công trước khi import lỗi; " +
                           "hãy chọn ‘Dùng schema trống đã có’ để thử lại bằng chính mật khẩu schema vừa nhập.";
            }
            journal.Complete(operation.Id, OperationState.Failed, message);
            logger.LogError(exception, "Phục hồi vào {EnvironmentKey} thất bại", target.Key);
            throw new InvalidOperationException(message, exception);
        }
    }

    private string FindDumpPath(BackupManifest backup)
    {
        var storage = storagePathResolver.ResolveAndEnsure();
        var path = new[] { storage.Repository, storage.LegacyRoot }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, backup.DumpFileName, SearchOption.AllDirectories))
            .FirstOrDefault(candidate => string.Equals(
                Path.GetFileName(Path.GetDirectoryName(candidate)),
                backup.Id,
                StringComparison.OrdinalIgnoreCase));
        return path ?? throw new InvalidOperationException("Không tìm thấy file .dmp cục bộ của bản sao đã chọn.");
    }

    private static async Task VerifyDumpAsync(
        string path,
        BackupManifest backup,
        CancellationToken cancellationToken)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length == 0)
        {
            throw new InvalidOperationException("File .dmp không tồn tại hoặc đang rỗng.");
        }

        if (backup.SizeBytes.HasValue && backup.SizeBytes.Value != file.Length)
        {
            throw new InvalidOperationException("Dung lượng file .dmp không khớp manifest.");
        }

        if (!string.IsNullOrWhiteSpace(backup.Sha256))
        {
            await using var stream = File.OpenRead(path);
            var checksum = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
            if (!string.Equals(checksum, backup.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("SHA-256 của file .dmp không khớp manifest; không thực hiện phục hồi.");
            }
        }
    }

    private static async Task<(string Name, int ObjectCount)> InspectTargetAsync(
        ManagedEnvironmentOptions target,
        OracleCredential credential,
        CancellationToken cancellationToken)
    {
        await using var connection = new OracleConnection(BuildConnectionString(target, credential));
        await connection.OpenAsync(cancellationToken);

        await using var countCommand = connection.CreateCommand();
        countCommand.BindByName = true;
        countCommand.CommandText = "SELECT COUNT(*) FROM USER_OBJECTS";
        var objectCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

        var candidates = new[] { target.Oracle.DataPumpDirectory, "DATA_PUMP_DIR" }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            await using var directoryCommand = connection.CreateCommand();
            directoryCommand.BindByName = true;
            directoryCommand.CommandText = "SELECT COUNT(*) FROM ALL_DIRECTORIES WHERE DIRECTORY_NAME = :name";
            directoryCommand.Parameters.Add("name", OracleDbType.Varchar2).Value = candidate;
            if (Convert.ToInt32(await directoryCommand.ExecuteScalarAsync(cancellationToken)) > 0)
            {
                return (candidate, objectCount);
            }
        }

        throw new InvalidOperationException("Schema đích chưa có quyền truy cập Oracle Data Pump Directory.");
    }

    private static async Task<(string Version, string Compatibility, int? TimeZoneFileVersion)> ReadTargetVersionAsync(
        ManagedEnvironmentOptions target,
        OracleCredential credential,
        CancellationToken cancellationToken)
    {
        await using var connection = new OracleConnection(BuildConnectionString(target, credential));
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = "BEGIN DBMS_UTILITY.DB_VERSION(:database_version, :compatibility); END;";
        var versionParameter = command.Parameters.Add("database_version", OracleDbType.Varchar2, 100);
        versionParameter.Direction = ParameterDirection.Output;
        var compatibilityParameter = command.Parameters.Add("compatibility", OracleDbType.Varchar2, 100);
        compatibilityParameter.Direction = ParameterDirection.Output;
        await command.ExecuteNonQueryAsync(cancellationToken);
        var version = Convert.ToString(versionParameter.Value)?.Trim();
        var compatibility = Convert.ToString(compatibilityParameter.Value)?.Trim();
        if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(compatibility))
        {
            throw new InvalidOperationException("Không xác định được phiên bản/COMPATIBLE của DB đích.");
        }

        int? timeZoneFileVersion = null;
        try
        {
            await using var timeZoneCommand = connection.CreateCommand();
            timeZoneCommand.CommandText = "SELECT VERSION FROM V$TIMEZONE_FILE";
            timeZoneFileVersion = Convert.ToInt32(await timeZoneCommand.ExecuteScalarAsync(cancellationToken));
        }
        catch (OracleException exception) when (exception.Number is 942 or 1031)
        {
            // Tài khoản schema có thể không có quyền xem fixed view; ORA-39405 vẫn được diễn giải khi import.
        }

        return (version, compatibility, timeZoneFileVersion);
    }

    private static void EnsureCompatibleVersions(string? dumpVersion, string targetVersion)
    {
        var dumpRelease = OracleDumpVersionReader.GetRelease(dumpVersion);
        var targetRelease = OracleDumpVersionReader.GetRelease(targetVersion);
        if (dumpRelease is null || targetRelease is null || dumpRelease <= targetRelease) return;

        throw new InvalidOperationException(
            $"Bản sao dùng định dạng Oracle {dumpVersion}, mới hơn DB đích Oracle {targetVersion}. " +
            "Không thể import trực tiếp. Hãy tạo lại bản sao bằng phiên bản Data Pump tương thích rồi phục hồi lại; " +
            "ứng dụng chưa thay đổi schema đích trong lần chạy này.");
    }

    private static void EnsureCompatibleTimeZone(BackupManifest backup, int? targetTimeZoneVersion)
    {
        if (!backup.SourceTimeZoneVersion.HasValue
            || !targetTimeZoneVersion.HasValue
            || backup.SourceTimeZoneVersion.Value <= targetTimeZoneVersion.Value)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Không thể phục hồi: TSTZ nguồn {backup.SourceTimeZoneVersion.Value} > đích {targetTimeZoneVersion.Value}. " +
            "VERSION của Data Pump không thay đổi timezone file. DBA cần nâng timezone file của DB đích " +
            "lên cùng hoặc cao hơn DB nguồn rồi kiểm tra và phục hồi lại.");
    }

    private static string? ReadOracleErrors(string path)
    {
        if (!File.Exists(path)) return null;
        var details = File.ReadLines(path)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("ORA-", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("UDE-", StringComparison.OrdinalIgnoreCase))
            .TakeLast(8)
            .ToArray();
        return details.Length == 0 ? null : string.Join(" · ", details);
    }

    private static async Task UploadDumpAsync(
        ManagedEnvironmentOptions target,
        OracleCredential credential,
        string localPath,
        string oracleFileName,
        CancellationToken cancellationToken)
    {
        await using var connection = new OracleConnection(BuildConnectionString(target, credential));
        await connection.OpenAsync(cancellationToken);
        await using var source = new FileStream(
            localPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            32767,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[32767];
        var firstChunk = true;
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await using var command = connection.CreateCommand();
            command.BindByName = true;
            command.CommandTimeout = 60;
            command.CommandText = """
                DECLARE
                    l_file UTL_FILE.FILE_TYPE;
                BEGIN
                    l_file := UTL_FILE.FOPEN(:directory_name, :file_name, :open_mode, 32767);
                    UTL_FILE.PUT_RAW(l_file, :file_data, TRUE);
                    UTL_FILE.FCLOSE(l_file);
                EXCEPTION
                    WHEN OTHERS THEN
                        IF UTL_FILE.IS_OPEN(l_file) THEN UTL_FILE.FCLOSE(l_file); END IF;
                        RAISE;
                END;
                """;
            command.Parameters.Add("directory_name", OracleDbType.Varchar2).Value = target.Oracle.DataPumpDirectory;
            command.Parameters.Add("file_name", OracleDbType.Varchar2).Value = oracleFileName;
            command.Parameters.Add("open_mode", OracleDbType.Varchar2).Value = firstChunk ? "wb" : "ab";
            command.Parameters.Add("file_data", OracleDbType.Raw).Value = buffer.AsSpan(0, bytesRead).ToArray();
            await command.ExecuteNonQueryAsync(cancellationToken);
            firstChunk = false;
        }
    }

    private static async Task VerifyUploadedDumpAsync(
        ManagedEnvironmentOptions target,
        OracleCredential credential,
        string oracleFileName,
        long expectedSize,
        CancellationToken cancellationToken)
    {
        await using var connection = new OracleConnection(BuildConnectionString(target, credential));
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = "SELECT DBMS_LOB.GETLENGTH(BFILENAME(:directory_name, :file_name)) FROM DUAL";
        command.Parameters.Add("directory_name", OracleDbType.Varchar2).Value = target.Oracle.DataPumpDirectory;
        command.Parameters.Add("file_name", OracleDbType.Varchar2).Value = oracleFileName;
        var actualSize = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        if (actualSize != expectedSize)
        {
            throw new InvalidOperationException(
                $"File dump chuyển vào DB đích chưa đầy đủ: nguồn {expectedSize:N0} byte, đích {actualSize:N0} byte. " +
                "Hệ thống đã dừng trước khi chạy Data Pump.");
        }
    }

    private static async Task RunImportAsync(
        ManagedEnvironmentOptions target,
        OracleCredential credential,
        string sourceSchema,
        string dumpFileName,
        string logFileName,
        CancellationToken cancellationToken)
    {
        ValidateIdentifier(sourceSchema, "Schema nguồn");
        await using var connection = new OracleConnection(BuildConnectionString(target, credential));
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = 60 * 60;
        command.CommandText = """
            DECLARE
                l_handle NUMBER := NULL;
                l_state  VARCHAR2(30);
                l_step   VARCHAR2(60) := 'mở job import';
                l_status KU$_STATUS;
                l_errors KU$_LOGENTRY;
                l_index  BINARY_INTEGER;
                l_error  VARCHAR2(4000);
            BEGIN
                l_handle := DBMS_DATAPUMP.OPEN(
                    operation => 'IMPORT',
                    job_mode  => 'SCHEMA',
                    job_name  => :job_name,
                    version   => 'COMPATIBLE');
                l_step := 'gắn file dump';
                DBMS_DATAPUMP.ADD_FILE(l_handle, :dump_file, :directory_name, NULL, DBMS_DATAPUMP.KU$_FILE_TYPE_DUMP_FILE);
                l_step := 'tạo file log';
                DBMS_DATAPUMP.ADD_FILE(l_handle, :log_file, :directory_name, NULL, DBMS_DATAPUMP.KU$_FILE_TYPE_LOG_FILE, 1);
                l_step := 'lọc schema nguồn';
                DBMS_DATAPUMP.METADATA_FILTER(l_handle, 'SCHEMA_EXPR', :schema_expression);
                IF :source_schema <> :target_schema THEN
                    l_step := 'ánh xạ schema nguồn sang đích';
                    DBMS_DATAPUMP.METADATA_REMAP(l_handle, 'REMAP_SCHEMA', :source_schema, :target_schema);
                END IF;
                l_step := 'chuẩn hóa thuộc tính lưu trữ';
                DBMS_DATAPUMP.METADATA_TRANSFORM(l_handle, 'SEGMENT_ATTRIBUTES', 0);
                l_step := 'đặt cách xử lý bảng đã có';
                DBMS_DATAPUMP.SET_PARAMETER(l_handle, 'TABLE_EXISTS_ACTION', 'SKIP');
                l_step := 'khởi chạy job import';
                DBMS_DATAPUMP.START_JOB(l_handle);
                l_step := 'chờ job import hoàn tất';
                DBMS_DATAPUMP.WAIT_FOR_JOB(l_handle, l_state);
                :job_state := l_state;
            EXCEPTION
                WHEN OTHERS THEN
                    l_error := SQLERRM;
                    IF l_handle IS NOT NULL THEN
                        BEGIN
                            DBMS_DATAPUMP.GET_STATUS(
                                l_handle,
                                DBMS_DATAPUMP.KU$_STATUS_JOB_ERROR,
                                0,
                                l_state,
                                l_status);
                            IF BITAND(l_status.mask, DBMS_DATAPUMP.KU$_STATUS_JOB_ERROR) <> 0 THEN
                                l_errors := l_status.error;
                                l_index := l_errors.FIRST;
                                WHILE l_index IS NOT NULL LOOP
                                    l_error := SUBSTR(l_error || ' | ' || l_errors(l_index).LogText, 1, 4000);
                                    l_index := l_errors.NEXT(l_index);
                                END LOOP;
                            END IF;
                        EXCEPTION WHEN OTHERS THEN NULL;
                        END;
                        BEGIN DBMS_DATAPUMP.DETACH(l_handle); EXCEPTION WHEN OTHERS THEN NULL; END;
                    END IF;
                    :job_state := 'FAILED';
                    :error_message := SUBSTR('Data Pump lỗi tại bước ' || l_step || ': ' || l_error, 1, 4000);
            END;
            """;
        var jobName = $"TL_IMP_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}"[..28].ToUpperInvariant();
        var source = sourceSchema.ToUpperInvariant();
        command.Parameters.Add("job_name", OracleDbType.Varchar2).Value = jobName;
        command.Parameters.Add("dump_file", OracleDbType.Varchar2).Value = dumpFileName;
        command.Parameters.Add("directory_name", OracleDbType.Varchar2).Value = target.Oracle.DataPumpDirectory;
        command.Parameters.Add("log_file", OracleDbType.Varchar2).Value = logFileName;
        command.Parameters.Add("schema_expression", OracleDbType.Varchar2).Value = $"IN ('{source}')";
        command.Parameters.Add("source_schema", OracleDbType.Varchar2).Value = source;
        command.Parameters.Add("target_schema", OracleDbType.Varchar2).Value = target.Oracle.Schema;
        var state = command.Parameters.Add("job_state", OracleDbType.Varchar2, 30);
        state.Direction = ParameterDirection.Output;
        var error = command.Parameters.Add("error_message", OracleDbType.Varchar2, 4000);
        error.Direction = ParameterDirection.Output;
        await command.ExecuteNonQueryAsync(cancellationToken);
        var value = Convert.ToString(state.Value)?.Trim();
        var errorMessage = Convert.ToString(error.Value)?.Trim();
        if (!string.IsNullOrWhiteSpace(errorMessage)
            && !string.Equals(errorMessage, "null", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(errorMessage);
        }
        if (!string.Equals(value, "COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Oracle Data Pump import kết thúc ở trạng thái {value ?? "không xác định"}.");
        }
    }

    private static bool IsTimeZoneVersionMismatch(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("ORA-39405", StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private static (int Source, int Target)? ReadTimeZoneMismatch(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            var match = Regex.Match(
                current.Message,
                @"source database with TSTZ version\s+(?<source>\d+)\s+into a target database with TSTZ version\s+(?<target>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success
                && int.TryParse(match.Groups["source"].Value, out var source)
                && int.TryParse(match.Groups["target"].Value, out var target))
            {
                return (source, target);
            }
        }

        return null;
    }

    private static async Task DownloadLogAsync(
        ManagedEnvironmentOptions target,
        OracleCredential credential,
        string fileName,
        string localPath,
        CancellationToken cancellationToken)
    {
        await using var connection = new OracleConnection(BuildConnectionString(target, credential));
        await connection.OpenAsync(cancellationToken);
        using var oracleFile = new OracleBFile(connection, target.Oracle.DataPumpDirectory, fileName);
        if (!oracleFile.FileExists) return;
        oracleFile.OpenFile();
        try
        {
            await using var destination = File.Create(localPath);
            var buffer = new byte[64 * 1024];
            var remaining = oracleFile.Length;
            while (remaining > 0)
            {
                var requested = (int)Math.Min(buffer.LongLength, remaining);
                var read = oracleFile.Read(buffer, 0, requested);
                if (read <= 0)
                {
                    throw new EndOfStreamException(
                        $"File Oracle {fileName} kết thúc sớm, còn thiếu {remaining} byte.");
                }
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                remaining -= read;
            }
        }
        finally
        {
            try
            {
                if (oracleFile.IsOpen) oracleFile.CloseFile();
            }
            catch (Exception exception) when (exception is InvalidOperationException or OracleException)
            {
                // ODP.NET có thể tự đóng locator sau khi đọc hết; lỗi đóng không làm hỏng file log đã tải.
            }
        }
    }

    private static void ValidateRequest(RestoreRequest request)
    {
        if (!request.TargetEnvironment.Enabled || !request.TargetEnvironment.Safety.AllowRestore)
            throw new InvalidOperationException("Kết nối đích chưa sẵn sàng hoặc chưa được phép phục hồi.");
        if (request.Backup.State != BackupState.Succeeded)
            throw new InvalidOperationException("Chỉ có thể phục hồi từ bản sao đã hoàn tất.");
        ValidateIdentifier(request.TargetSchema, "Schema đích");
        var targetSchema = request.TargetSchema.Trim().ToUpperInvariant();
        if (request.TargetMode == RestoreTargetMode.ReplaceExisting
            && targetSchema is "SYS" or "SYSTEM" or "PDBADMIN")
            throw new InvalidOperationException($"Không được phép thay thế tài khoản quản trị Oracle {targetSchema}.");
        if ((request.TargetMode is RestoreTargetMode.CreateNew or RestoreTargetMode.ReplaceExisting)
            && (string.IsNullOrWhiteSpace(request.SysPassword)
                || string.IsNullOrWhiteSpace(request.SchemaPassword)
                || string.IsNullOrWhiteSpace(request.DefaultTablespace)))
            throw new InvalidOperationException("Chưa nhập đủ SYS, mật khẩu schema và tablespace.");
        if (request.TargetMode == RestoreTargetMode.ReplaceExisting
            && !string.Equals(
                request.ReplacementConfirmation.Trim(),
                request.TargetSchema.Trim(),
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Tên schema xác nhận thay thế chưa chính xác.");
    }

    private static OracleConnectionStringBuilder CreateConnectionBuilder(ManagedEnvironmentOptions target, OracleCredential credential) => new()
    {
        UserID = credential.UserName,
        Password = credential.Password,
        DataSource = $"{target.Oracle.Host}:{target.Oracle.Port}/{target.Oracle.ServiceName}",
        Pooling = false,
        ConnectionTimeout = 10
    };

    private static string BuildConnectionString(ManagedEnvironmentOptions target, OracleCredential credential) =>
        CreateConnectionBuilder(target, credential).ConnectionString;

    private static ManagedEnvironmentOptions Clone(ManagedEnvironmentOptions source) => new()
    {
        Key = source.Key, DisplayName = source.DisplayName, Tier = source.Tier, Enabled = source.Enabled,
        Oracle = new OracleEnvironmentOptions
        {
            Host = source.Oracle.Host, Port = source.Oracle.Port, ServiceName = source.Oracle.ServiceName,
            Schema = source.Oracle.Schema, DataPumpDirectory = source.Oracle.DataPumpDirectory,
            UserEnvironmentVariable = source.Oracle.UserEnvironmentVariable,
            PasswordEnvironmentVariable = source.Oracle.PasswordEnvironmentVariable
        },
        Safety = new EnvironmentSafetyOptions
        {
            AllowBackup = source.Safety.AllowBackup, AllowRestore = source.Safety.AllowRestore,
            AllowMigrate = source.Safety.AllowMigrate
        }
    };

    private static string GetSafeMessage(Exception exception)
    {
        if (exception is InvalidOperationException
            && exception.Message.StartsWith("Data Pump không thể khởi chạy.", StringComparison.Ordinal))
        {
            return exception.Message;
        }

        var oracle = FindOracleException(exception);
        if (oracle is not null)
        {
            if (oracle.Number == 1940)
            {
                return "Schema vẫn còn phiên kết nối hoạt động. Hãy dừng ứng dụng đang dùng schema rồi thử lại.";
            }

            var details = string.Join(
                " · ",
                oracle.Message
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => line.StartsWith("ORA-", StringComparison.OrdinalIgnoreCase)
                        || line.StartsWith("PLS-", StringComparison.OrdinalIgnoreCase))
                    .Take(4));
            return string.IsNullOrWhiteSpace(details)
                ? $"Oracle không thể phục hồi (ORA-{oracle.Number:00000})."
                : details;
        }
        return exception.Message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            ?? "Phục hồi không thành công.";
    }

    private static OracleException? FindOracleException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is OracleException oracle) return oracle;
        }

        return null;
    }

    private static void ValidateIdentifier(string value, string fieldName)
    {
        if (!OracleIdentifierRegex().IsMatch(value))
            throw new InvalidOperationException($"{fieldName} không hợp lệ.");
    }

    private static string SanitizeFilePart(string value) => FilePartRegex().Replace(value, "_").Trim('_');

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_$#]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex OracleIdentifierRegex();

    [GeneratedRegex(@"[^A-Za-z0-9_-]+", RegexOptions.CultureInvariant)]
    private static partial Regex FilePartRegex();
}
