using System.Text.RegularExpressions;
using Oracle.ManagedDataAccess.Client;
using ThucLuc.DbManager.Configuration;

namespace ThucLuc.DbManager.Services;

public sealed record OracleDirectorySetupResult(string DirectoryName, string DirectoryPath);

public interface IOracleDirectorySetupService
{
    Task<OracleDirectorySetupResult> GrantDefaultDirectoryAsync(
        ManagedEnvironmentOptions environment,
        string sysPassword,
        CancellationToken cancellationToken = default);
}

public sealed partial class OracleDirectorySetupService(
    IEnvironmentConfigurationService configurationService,
    IOperationJournal journal,
    ILogger<OracleDirectorySetupService> logger) : IOracleDirectorySetupService
{
    private const string DefaultDirectoryName = "DATA_PUMP_DIR";

    public async Task<OracleDirectorySetupResult> GrantDefaultDirectoryAsync(
        ManagedEnvironmentOptions environment,
        string sysPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sysPassword))
        {
            throw new InvalidOperationException("Hãy nhập mật khẩu SYS.");
        }

        ValidateIdentifier(environment.Oracle.Schema, "Schema");
        var operation = journal.AddRunning(
            "Thiết lập quyền sao lưu",
            environment.Key,
            $"Đang cấp quyền {DefaultDirectoryName} cho {environment.Oracle.Schema}.");

        try
        {
            var connectionString = new OracleConnectionStringBuilder
            {
                UserID = "SYS",
                Password = sysPassword,
                DataSource = $"{environment.Oracle.Host}:{environment.Oracle.Port}/{environment.Oracle.ServiceName}",
                DBAPrivilege = "SYSDBA",
                Pooling = false,
                ConnectionTimeout = 10
            }.ConnectionString;

            await using var connection = new OracleConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var directoryPath = await FindDefaultDirectoryAsync(connection, cancellationToken);
            if (directoryPath is null)
            {
                throw new InvalidOperationException(
                    $"Oracle chưa có {DefaultDirectoryName}. DBA cần tạo vùng Data Pump trên máy chủ trước.");
            }

            await GrantDirectoryAsync(connection, environment.Oracle.Schema, cancellationToken);

            var updatedEnvironment = Clone(environment);
            updatedEnvironment.Oracle.DataPumpDirectory = DefaultDirectoryName;
            await configurationService.SaveAsync(updatedEnvironment, cancellationToken);

            journal.Complete(
                operation.Id,
                Models.OperationState.Succeeded,
                $"Đã cấp READ, WRITE trên {DefaultDirectoryName} cho {environment.Oracle.Schema}.");
            logger.LogInformation(
                "Đã cấp quyền Oracle Directory {DirectoryName} cho schema {Schema} tại môi trường {EnvironmentKey}",
                DefaultDirectoryName,
                environment.Oracle.Schema,
                environment.Key);

            return new OracleDirectorySetupResult(DefaultDirectoryName, directoryPath);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var message = GetSafeMessage(exception);
            journal.Complete(operation.Id, Models.OperationState.Failed, message);
            logger.LogError(
                exception,
                "Không thể thiết lập Oracle Directory cho môi trường {EnvironmentKey}",
                environment.Key);
            throw new InvalidOperationException(message, exception);
        }
    }

    private static async Task<string?> FindDefaultDirectoryAsync(
        OracleConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = "SELECT DIRECTORY_PATH FROM DBA_DIRECTORIES WHERE DIRECTORY_NAME = :directory_name";
        command.Parameters.Add("directory_name", OracleDbType.Varchar2).Value = DefaultDirectoryName;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToString(result);
    }

    private static async Task GrantDirectoryAsync(
        OracleConnection connection,
        string schema,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"GRANT READ, WRITE ON DIRECTORY \"{DefaultDirectoryName}\" TO \"{schema.ToUpperInvariant()}\"";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ValidateIdentifier(string value, string fieldName)
    {
        if (!OracleIdentifierRegex().IsMatch(value))
        {
            throw new InvalidOperationException($"{fieldName} không hợp lệ để cấp quyền.");
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

    private static string GetSafeMessage(Exception exception)
    {
        if (exception is OracleException oracleException)
        {
            return oracleException.Number switch
            {
                1017 => "Mật khẩu SYS không đúng hoặc tài khoản không đăng nhập được.",
                1031 => "Tài khoản không có quyền SYSDBA.",
                _ => $"Oracle không thể thiết lập quyền (ORA-{oracleException.Number:00000})."
            };
        }

        var firstLine = exception.Message
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstLine) ? "Không thể thiết lập quyền sao lưu." : firstLine.Trim();
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_$#]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex OracleIdentifierRegex();
}
