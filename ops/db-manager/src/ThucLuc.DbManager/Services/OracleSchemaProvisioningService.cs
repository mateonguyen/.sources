using System.Text.RegularExpressions;
using Oracle.ManagedDataAccess.Client;
using ThucLuc.DbManager.Configuration;
using ThucLuc.DbManager.Models;

namespace ThucLuc.DbManager.Services;

public interface IOracleSchemaProvisioningService
{
    Task<OracleSchemaInspection> InspectAsync(
        ManagedEnvironmentOptions environment,
        string sysPassword,
        CancellationToken cancellationToken = default);

    Task<OracleSchemaProvisionResult> CreateAsync(
        ManagedEnvironmentOptions environment,
        string sysPassword,
        string schemaPassword,
        string defaultTablespace,
        CancellationToken cancellationToken = default);

    Task<OracleSchemaProvisionResult> ReplaceAsync(
        ManagedEnvironmentOptions environment,
        string sysPassword,
        string schemaPassword,
        string defaultTablespace,
        CancellationToken cancellationToken = default);
}

public sealed partial class OracleSchemaProvisioningService(
    IEnvironmentConfigurationService configurationService,
    IOperationJournal journal,
    ILogger<OracleSchemaProvisioningService> logger) : IOracleSchemaProvisioningService
{
    private const string DefaultDataPumpDirectory = "DATA_PUMP_DIR";

    private static readonly string[] SchemaPrivileges =
    [
        "CREATE SESSION",
        "CREATE TABLE",
        "CREATE VIEW",
        "CREATE SEQUENCE",
        "CREATE PROCEDURE",
        "CREATE TRIGGER",
        "CREATE TYPE"
    ];

    public async Task<OracleSchemaInspection> InspectAsync(
        ManagedEnvironmentOptions environment,
        string sysPassword,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(environment, sysPassword);

        try
        {
            await using var connection = new OracleConnection(BuildSysConnectionString(environment, sysPassword));
            await connection.OpenAsync(cancellationToken);
            return await InspectOpenConnectionAsync(connection, environment, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Không thể kiểm tra schema {Schema} tại môi trường {EnvironmentKey}",
                environment.Oracle.Schema,
                environment.Key);
            throw new InvalidOperationException(GetSafeMessage(exception, "Không thể kiểm tra DB đích bằng SYS."), exception);
        }
    }

    public async Task<OracleSchemaProvisionResult> CreateAsync(
        ManagedEnvironmentOptions environment,
        string sysPassword,
        string schemaPassword,
        string defaultTablespace,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(environment, sysPassword);
        ValidateSchemaPassword(schemaPassword);
        ValidateIdentifier(defaultTablespace, "Tablespace");

        var schema = environment.Oracle.Schema.ToUpperInvariant();
        var operation = journal.AddRunning(
            "Khởi tạo schema",
            environment.Key,
            $"Đang tạo schema {schema} trên DB đích.");

        try
        {
            await using var connection = new OracleConnection(BuildSysConnectionString(environment, sysPassword));
            await connection.OpenAsync(cancellationToken);

            var inspection = await InspectOpenConnectionAsync(connection, environment, cancellationToken);
            if (inspection.SchemaExists)
            {
                throw new InvalidOperationException(
                    $"Schema {schema} đã tồn tại. Miniapp không tự động xóa hoặc ghi đè schema đang có.");
            }

            if (!inspection.AvailableTablespaces.Contains(defaultTablespace, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Tablespace {defaultTablespace} không tồn tại hoặc không sẵn sàng.");
            }

            await CreateUserAsync(
                connection,
                schema,
                schemaPassword,
                defaultTablespace.ToUpperInvariant(),
                inspection.TemporaryTablespace,
                inspection.IsContainerDatabase,
                cancellationToken);
            await GrantSchemaPrivilegesAsync(connection, schema, cancellationToken);

            if (!string.IsNullOrWhiteSpace(inspection.DataPumpDirectoryName))
            {
                await GrantDirectoryAsync(
                    connection,
                    schema,
                    inspection.DataPumpDirectoryName,
                    cancellationToken);
            }

            await VerifySchemaLoginAsync(environment, schema, schemaPassword, cancellationToken);

            if (!string.IsNullOrWhiteSpace(inspection.DataPumpDirectoryName)
                && !string.Equals(
                    environment.Oracle.DataPumpDirectory,
                    inspection.DataPumpDirectoryName,
                    StringComparison.OrdinalIgnoreCase))
            {
                var updatedEnvironment = Clone(environment);
                updatedEnvironment.Oracle.DataPumpDirectory = inspection.DataPumpDirectoryName;
                await configurationService.SaveAsync(updatedEnvironment, cancellationToken);
            }

            var result = new OracleSchemaProvisionResult(
                schema,
                inspection.ContainerName,
                defaultTablespace.ToUpperInvariant(),
                inspection.TemporaryTablespace,
                inspection.DataPumpDirectoryName,
                LoginVerified: true);

            journal.Complete(
                operation.Id,
                OperationState.Succeeded,
                $"Đã tạo và kiểm tra đăng nhập schema {schema} trên {inspection.ContainerName}.");
            logger.LogInformation(
                "Đã khởi tạo schema {Schema} trên container {ContainerName} cho môi trường {EnvironmentKey}",
                schema,
                inspection.ContainerName,
                environment.Key);
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var message = GetSafeMessage(exception, "Không thể khởi tạo schema đích.");
            journal.Complete(operation.Id, OperationState.Failed, message);
            logger.LogError(
                exception,
                "Khởi tạo schema {Schema} tại môi trường {EnvironmentKey} thất bại",
                schema,
                environment.Key);
            throw new InvalidOperationException(message, exception);
        }
    }

    public async Task<OracleSchemaProvisionResult> ReplaceAsync(
        ManagedEnvironmentOptions environment,
        string sysPassword,
        string schemaPassword,
        string defaultTablespace,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(environment, sysPassword);
        ValidateSchemaPassword(schemaPassword);
        ValidateIdentifier(defaultTablespace, "Tablespace");

        var schema = environment.Oracle.Schema.ToUpperInvariant();
        if (schema is "SYS" or "SYSTEM" or "PDBADMIN")
        {
            throw new InvalidOperationException($"Không được phép thay thế tài khoản quản trị Oracle {schema}.");
        }

        var operation = journal.AddRunning(
            "Thay thế schema",
            environment.Key,
            $"Đang xóa và tạo lại schema {schema} sau khi đã có bản sao an toàn.");
        var schemaLocked = false;
        var schemaDropped = false;

        try
        {
            await using var connection = new OracleConnection(BuildSysConnectionString(environment, sysPassword));
            await connection.OpenAsync(cancellationToken);

            var inspection = await InspectOpenConnectionAsync(connection, environment, cancellationToken);
            if (!inspection.SchemaExists)
            {
                throw new InvalidOperationException(
                    $"Schema {schema} không còn tồn tại. Hãy chọn chế độ tạo schema mới.");
            }

            if (!inspection.AvailableTablespaces.Contains(defaultTablespace, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Tablespace {defaultTablespace} không tồn tại hoặc không sẵn sàng.");
            }

            await EnsureSchemaIsNotOracleMaintainedAsync(connection, schema, cancellationToken);

            await using (var lockCommand = connection.CreateCommand())
            {
                lockCommand.CommandText = $"ALTER USER \"{schema}\" ACCOUNT LOCK";
                await lockCommand.ExecuteNonQueryAsync(cancellationToken);
                schemaLocked = true;
            }

            var disconnectedSessions = await DisconnectSchemaSessionsAsync(
                connection,
                schema,
                cancellationToken);
            if (disconnectedSessions > 0)
            {
                logger.LogInformation(
                    "Đã ngắt {SessionCount} phiên đang dùng schema {Schema} trước khi thay thế",
                    disconnectedSessions,
                    schema);
            }

            await DropSchemaWithRetryAsync(connection, schema, cancellationToken);
            schemaDropped = true;
            schemaLocked = false;

            await CreateUserAsync(
                connection,
                schema,
                schemaPassword,
                defaultTablespace.ToUpperInvariant(),
                inspection.TemporaryTablespace,
                inspection.IsContainerDatabase,
                cancellationToken);
            await GrantSchemaPrivilegesAsync(connection, schema, cancellationToken);

            if (!string.IsNullOrWhiteSpace(inspection.DataPumpDirectoryName))
            {
                await GrantDirectoryAsync(
                    connection,
                    schema,
                    inspection.DataPumpDirectoryName,
                    cancellationToken);
            }

            await VerifySchemaLoginAsync(environment, schema, schemaPassword, cancellationToken);

            var result = new OracleSchemaProvisionResult(
                schema,
                inspection.ContainerName,
                defaultTablespace.ToUpperInvariant(),
                inspection.TemporaryTablespace,
                inspection.DataPumpDirectoryName,
                LoginVerified: true);
            journal.Complete(
                operation.Id,
                OperationState.Succeeded,
                $"Đã xóa và tạo lại schema {schema} trên {inspection.ContainerName}.");
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var message = GetSafeMessage(exception, "Không thể thay thế schema đích.");
            journal.Complete(operation.Id, OperationState.Failed, message);
            logger.LogError(
                exception,
                "Thay thế schema {Schema} tại môi trường {EnvironmentKey} thất bại",
                schema,
                environment.Key);
            throw new InvalidOperationException(message, exception);
        }
        finally
        {
            if (schemaLocked && !schemaDropped)
            {
                await TryUnlockSchemaAsync(environment, sysPassword, schema);
            }
        }
    }

    private static async Task<int> DisconnectSchemaSessionsAsync(
        OracleConnection connection,
        string schema,
        CancellationToken cancellationToken)
    {
        var sessions = new List<(int InstanceId, int SessionId, int SerialNumber)>();
        await using (var query = connection.CreateCommand())
        {
            query.BindByName = true;
            query.CommandText = """
                SELECT INST_ID, SID, SERIAL#
                FROM GV$SESSION
                WHERE USERNAME = :schema
                  AND TYPE = 'USER'
                """;
            query.Parameters.Add("schema", OracleDbType.Varchar2).Value = schema;
            await using var reader = await query.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                sessions.Add((
                    Convert.ToInt32(reader.GetValue(0)),
                    Convert.ToInt32(reader.GetValue(1)),
                    Convert.ToInt32(reader.GetValue(2))));
            }
        }

        foreach (var session in sessions)
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"ALTER SYSTEM KILL SESSION '{session.SessionId},{session.SerialNumber},@{session.InstanceId}' IMMEDIATE";
            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (OracleException exception) when (exception.Number is 30 or 31)
            {
                // Session đã biến mất hoặc đã được đánh dấu kết thúc.
            }
        }

        return sessions.Count;
    }

    private static async Task DropSchemaWithRetryAsync(
        OracleConnection connection,
        string schema,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 20; attempt++)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP USER \"{schema}\" CASCADE";
            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
                return;
            }
            catch (OracleException exception) when (exception.Number == 1940 && attempt < 20)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
        }
    }

    private static async Task TryUnlockSchemaAsync(
        ManagedEnvironmentOptions environment,
        string sysPassword,
        string schema)
    {
        try
        {
            await using var connection = new OracleConnection(BuildSysConnectionString(environment, sysPassword));
            await connection.OpenAsync(CancellationToken.None);
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER USER \"{schema}\" ACCOUNT UNLOCK";
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }
        catch
        {
            // Best effort: giữ nguyên lỗi gốc, không để lỗi mở khóa che mất nguyên nhân thay thế thất bại.
        }
    }

    private static async Task EnsureSchemaIsNotOracleMaintainedAsync(
        OracleConnection connection,
        string schema,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = "SELECT ORACLE_MAINTAINED FROM DBA_USERS WHERE USERNAME = :schema";
        command.Parameters.Add("schema", OracleDbType.Varchar2).Value = schema;
        var value = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
        if (string.Equals(value, "Y", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Không được phép thay thế schema do Oracle quản lý: {schema}.");
        }
    }

    private static async Task<OracleSchemaInspection> InspectOpenConnectionAsync(
        OracleConnection connection,
        ManagedEnvironmentOptions environment,
        CancellationToken cancellationToken)
    {
        var databaseName = await QueryRequiredStringAsync(
            connection,
            "SELECT SYS_CONTEXT('USERENV', 'DB_NAME') FROM DUAL",
            cancellationToken);
        var databaseVersion = await QueryRequiredStringAsync(
            connection,
            "SELECT VERSION FROM V$INSTANCE",
            cancellationToken);
        var databaseCompatibility = await QueryRequiredStringAsync(
            connection,
            "SELECT VALUE FROM V$PARAMETER WHERE NAME = 'compatible'",
            cancellationToken);
        var timeZoneFileVersion = await QueryOptionalIntAsync(
            connection,
            "SELECT VERSION FROM V$TIMEZONE_FILE",
            cancellationToken);
        var databaseMode = await QueryRequiredStringAsync(
            connection,
            "SELECT CDB FROM V$DATABASE",
            cancellationToken);
        var isContainerDatabase = string.Equals(databaseMode, "YES", StringComparison.OrdinalIgnoreCase);
        var containerName = await QueryRequiredStringAsync(
            connection,
            "SELECT SYS_CONTEXT('USERENV', 'CON_NAME') FROM DUAL",
            cancellationToken);
        var containerIdText = await QueryRequiredStringAsync(
            connection,
            "SELECT SYS_CONTEXT('USERENV', 'CON_ID') FROM DUAL",
            cancellationToken);
        _ = int.TryParse(containerIdText, out var containerId);

        if (isContainerDatabase
            && (string.Equals(containerName, "CDB$ROOT", StringComparison.OrdinalIgnoreCase)
                || containerId <= 1))
        {
            throw new InvalidOperationException(
                $"Kết nối hiện đang vào {containerName}, nơi quản trị chung, không phải nơi tạo schema ứng dụng. " +
                "Ở phần Kết nối đích phía trên, mở ‘Chọn / tạo database con (PDB)’, " +
                "chọn database con của ứng dụng hoặc tạo mới, rồi kiểm tra DB đích lại.");
        }

        var schema = environment.Oracle.Schema.ToUpperInvariant();
        string? accountStatus = null;
        string? currentDefaultTablespace = null;
        await using (var userCommand = connection.CreateCommand())
        {
            userCommand.BindByName = true;
            userCommand.CommandText = "SELECT ACCOUNT_STATUS, DEFAULT_TABLESPACE FROM DBA_USERS WHERE USERNAME = :schema";
            userCommand.Parameters.Add("schema", OracleDbType.Varchar2).Value = schema;
            await using var reader = await userCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                accountStatus = reader.GetString(0);
                currentDefaultTablespace = reader.GetString(1);
            }
        }

        var objectCount = 0;
        if (accountStatus is not null)
        {
            await using var objectCommand = connection.CreateCommand();
            objectCommand.BindByName = true;
            objectCommand.CommandText = "SELECT COUNT(*) FROM DBA_OBJECTS WHERE OWNER = :schema";
            objectCommand.Parameters.Add("schema", OracleDbType.Varchar2).Value = schema;
            objectCount = Convert.ToInt32(await objectCommand.ExecuteScalarAsync(cancellationToken));
        }

        var tablespaces = new List<string>();
        await using (var tablespaceCommand = connection.CreateCommand())
        {
            tablespaceCommand.CommandText = """
                SELECT TABLESPACE_NAME
                FROM DBA_TABLESPACES
                WHERE CONTENTS = 'PERMANENT'
                  AND STATUS = 'ONLINE'
                  AND TABLESPACE_NAME NOT IN ('SYSTEM', 'SYSAUX')
                ORDER BY CASE WHEN TABLESPACE_NAME = 'USERS' THEN 0 ELSE 1 END, TABLESPACE_NAME
                """;
            await using var reader = await tablespaceCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tablespaces.Add(reader.GetString(0));
            }
        }

        if (tablespaces.Count == 0)
        {
            throw new InvalidOperationException("DB đích chưa có tablespace dữ liệu phù hợp để tạo schema.");
        }

        var recommendedTablespace = await QueryOptionalStringAsync(
            connection,
            "SELECT PROPERTY_VALUE FROM DATABASE_PROPERTIES WHERE PROPERTY_NAME = 'DEFAULT_PERMANENT_TABLESPACE'",
            cancellationToken);
        if (string.IsNullOrWhiteSpace(recommendedTablespace)
            || !tablespaces.Contains(recommendedTablespace, StringComparer.OrdinalIgnoreCase))
        {
            recommendedTablespace = tablespaces[0];
        }

        var temporaryTablespace = await QueryOptionalStringAsync(
            connection,
            "SELECT PROPERTY_VALUE FROM DATABASE_PROPERTIES WHERE PROPERTY_NAME = 'DEFAULT_TEMP_TABLESPACE'",
            cancellationToken) ?? "TEMP";

        var (directoryName, directoryPath) = await FindDataPumpDirectoryAsync(
            connection,
            environment.Oracle.DataPumpDirectory,
            cancellationToken);

        return new OracleSchemaInspection(
            databaseName,
            databaseVersion,
            databaseCompatibility,
            timeZoneFileVersion,
            containerName,
            containerId,
            isContainerDatabase,
            accountStatus is not null,
            accountStatus,
            currentDefaultTablespace,
            objectCount,
            recommendedTablespace,
            temporaryTablespace,
            tablespaces,
            directoryName,
            directoryPath);
    }

    private static async Task<(string? Name, string? Path)> FindDataPumpDirectoryAsync(
        OracleConnection connection,
        string configuredDirectory,
        CancellationToken cancellationToken)
    {
        var candidates = new[] { configuredDirectory, DefaultDataPumpDirectory }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            await using var command = connection.CreateCommand();
            command.BindByName = true;
            command.CommandText = "SELECT DIRECTORY_PATH FROM DBA_DIRECTORIES WHERE DIRECTORY_NAME = :directory_name";
            command.Parameters.Add("directory_name", OracleDbType.Varchar2).Value = candidate;
            var path = await command.ExecuteScalarAsync(cancellationToken);
            if (path is not null and not DBNull)
            {
                return (candidate, Convert.ToString(path));
            }
        }

        return (null, null);
    }

    private static async Task CreateUserAsync(
        OracleConnection connection,
        string schema,
        string schemaPassword,
        string defaultTablespace,
        string temporaryTablespace,
        bool isContainerDatabase,
        CancellationToken cancellationToken)
    {
        ValidateIdentifier(temporaryTablespace, "Temporary tablespace");
        await using var command = connection.CreateCommand();
        var containerClause = isContainerDatabase ? "CONTAINER = CURRENT" : string.Empty;
        command.CommandText = $"""
            CREATE USER "{schema}"
            IDENTIFIED BY "{schemaPassword}"
            DEFAULT TABLESPACE "{defaultTablespace}"
            TEMPORARY TABLESPACE "{temporaryTablespace}"
            QUOTA UNLIMITED ON "{defaultTablespace}"
            {containerClause}
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task GrantSchemaPrivilegesAsync(
        OracleConnection connection,
        string schema,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"GRANT {string.Join(", ", SchemaPrivileges)} TO \"{schema}\"";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task GrantDirectoryAsync(
        OracleConnection connection,
        string schema,
        string directoryName,
        CancellationToken cancellationToken)
    {
        ValidateIdentifier(directoryName, "Oracle Directory");
        await using var command = connection.CreateCommand();
        command.CommandText = $"GRANT READ, WRITE ON DIRECTORY \"{directoryName}\" TO \"{schema}\"";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task VerifySchemaLoginAsync(
        ManagedEnvironmentOptions environment,
        string schema,
        string schemaPassword,
        CancellationToken cancellationToken)
    {
        var connectionString = new OracleConnectionStringBuilder
        {
            UserID = schema,
            Password = schemaPassword,
            DataSource = $"{environment.Oracle.Host}:{environment.Oracle.Port}/{environment.Oracle.ServiceName}",
            Pooling = false,
            ConnectionTimeout = 10
        }.ConnectionString;
        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM DUAL";
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task<string> QueryRequiredStringAsync(
        OracleConnection connection,
        string sql,
        CancellationToken cancellationToken) =>
        await QueryOptionalStringAsync(connection, sql, cancellationToken)
        ?? throw new InvalidOperationException("Oracle không trả về thông tin DB cần thiết.");

    private static async Task<string?> QueryOptionalStringAsync(
        OracleConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToString(result);
    }

    private static async Task<int?> QueryOptionalIntAsync(
        OracleConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToInt32(result);
    }

    private static string BuildSysConnectionString(
        ManagedEnvironmentOptions environment,
        string sysPassword) => new OracleConnectionStringBuilder
    {
        UserID = "SYS",
        Password = sysPassword,
        DataSource = $"{environment.Oracle.Host}:{environment.Oracle.Port}/{environment.Oracle.ServiceName}",
        DBAPrivilege = "SYSDBA",
        Pooling = false,
        ConnectionTimeout = 10
    }.ConnectionString;

    private static void ValidateRequest(ManagedEnvironmentOptions environment, string sysPassword)
    {
        if (!environment.Safety.AllowRestore)
        {
            throw new InvalidOperationException("Kết nối này không được cấu hình làm DB đích phục hồi.");
        }

        if (!environment.Enabled)
        {
            throw new InvalidOperationException("Kết nối đích đang tắt.");
        }

        if (string.IsNullOrWhiteSpace(sysPassword))
        {
            throw new InvalidOperationException("Hãy nhập mật khẩu SYS.");
        }

        if (string.IsNullOrWhiteSpace(environment.Oracle.Host)
            || string.IsNullOrWhiteSpace(environment.Oracle.ServiceName))
        {
            throw new InvalidOperationException("Kết nối đích chưa có Host hoặc Service/PDB.");
        }

        ValidateIdentifier(environment.Oracle.Schema, "Schema");
    }

    private static void ValidateSchemaPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length > 128)
        {
            throw new InvalidOperationException("Hãy nhập mật khẩu schema (tối đa 128 ký tự). Chính sách độ mạnh do Oracle kiểm tra.");
        }

        if (password.Contains('"') || password.Any(char.IsControl))
        {
            throw new InvalidOperationException("Mật khẩu schema không được chứa dấu ngoặc kép hoặc ký tự điều khiển.");
        }
    }

    private static void ValidateIdentifier(string value, string fieldName)
    {
        if (!OracleIdentifierRegex().IsMatch(value))
        {
            throw new InvalidOperationException($"{fieldName} không phải định danh Oracle hợp lệ.");
        }
    }

    private static ManagedEnvironmentOptions Clone(ManagedEnvironmentOptions source) => new()
    {
        Key = source.Key,
        DisplayName = source.DisplayName,
        Tier = source.Tier,
        Enabled = source.Enabled,
        Oracle = new OracleEnvironmentOptions
        {
            Host = source.Oracle.Host,
            Port = source.Oracle.Port,
            ServiceName = source.Oracle.ServiceName,
            Schema = source.Oracle.Schema,
            DataPumpDirectory = source.Oracle.DataPumpDirectory,
            UserEnvironmentVariable = source.Oracle.UserEnvironmentVariable,
            PasswordEnvironmentVariable = source.Oracle.PasswordEnvironmentVariable
        },
        Safety = new EnvironmentSafetyOptions
        {
            AllowBackup = source.Safety.AllowBackup,
            AllowRestore = source.Safety.AllowRestore,
            AllowMigrate = source.Safety.AllowMigrate
        }
    };

    private static string GetSafeMessage(Exception exception, string fallback)
    {
        if (exception is OracleException oracleException)
        {
            return oracleException.Number switch
            {
                1017 => "Mật khẩu SYS không đúng hoặc SYS không đăng nhập được vào Service/PDB đã chọn.",
                1031 => "Tài khoản không có quyền SYSDBA.",
                1918 => "Schema không tồn tại.",
                1920 => "Tên schema đang xung đột với user hoặc role đã có.",
                1940 => "Schema đang có phiên đăng nhập hoạt động. Hãy dừng ứng dụng đang dùng schema này rồi thử lại.",
                65040 => "Thao tác không được phép trong container hiện tại. Kiểm tra lại Service/PDB.",
                65096 => "Tên schema không hợp lệ cho container hiện tại. Có thể kết nối đang vào CDB$ROOT.",
                _ => $"Oracle từ chối thao tác (ORA-{oracleException.Number:00000})."
            };
        }

        var firstLine = exception.Message
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstLine) ? fallback : firstLine.Trim();
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_$#]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex OracleIdentifierRegex();
}
