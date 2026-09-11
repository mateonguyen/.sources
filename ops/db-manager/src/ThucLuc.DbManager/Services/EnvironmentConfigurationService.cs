using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ThucLuc.DbManager.Configuration;

namespace ThucLuc.DbManager.Services;

public interface IEnvironmentConfigurationService
{
    Task SaveAsync(ManagedEnvironmentOptions environment, CancellationToken cancellationToken = default);

    Task DeleteAsync(string environmentKey, CancellationToken cancellationToken = default);
}

public sealed class EnvironmentConfigurationService(
    IOptionsMonitor<OpsOptions> options,
    OpsRuntimePaths runtimePaths,
    ILogger<EnvironmentConfigurationService> logger) : IEnvironmentConfigurationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task SaveAsync(
        ManagedEnvironmentOptions environment,
        CancellationToken cancellationToken = default)
    {
        Validate(environment);
        if (string.Equals(environment.Tier, "Production", StringComparison.OrdinalIgnoreCase)
            && !options.CurrentValue.AllowProductionOperations
            && (environment.Safety.AllowBackup
                || environment.Safety.AllowRestore
                || environment.Safety.AllowMigrate))
        {
            throw new InvalidOperationException(
                "Thao tác Production đang khóa toàn cục. Hãy bỏ chọn Backup, Migration và Restore.");
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var updated = await ReadCurrentAsync(cancellationToken);
            var existingIndex = updated.Environments.FindIndex(item =>
                string.Equals(item.Key, environment.Key, StringComparison.OrdinalIgnoreCase));
            var safeEnvironment = Clone(environment);

            if (existingIndex >= 0)
            {
                updated.Environments[existingIndex] = safeEnvironment;
            }
            else
            {
                updated.Environments.Add(safeEnvironment);
            }

            await WriteAsync(updated, cancellationToken);
            logger.LogInformation("Đã cập nhật cấu hình môi trường {EnvironmentKey}", environment.Key);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task DeleteAsync(string environmentKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(environmentKey))
        {
            throw new InvalidOperationException("Chưa xác định môi trường cần xóa.");
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var updated = await ReadCurrentAsync(cancellationToken);
            updated.Environments.RemoveAll(item =>
                string.Equals(item.Key, environmentKey, StringComparison.OrdinalIgnoreCase));
            await WriteAsync(updated, cancellationToken);
            logger.LogInformation("Đã xóa cấu hình môi trường {EnvironmentKey}", environmentKey);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task WriteAsync(OpsOptions updated, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(runtimePaths.ConfigurationFile)
            ?? throw new InvalidOperationException("Đường dẫn cấu hình môi trường không hợp lệ.");
        Directory.CreateDirectory(directory);

        var document = new OpsConfigurationDocument(updated);
        var json = JsonSerializer.Serialize(document, SerializerOptions);
        var temporaryFile = runtimePaths.ConfigurationFile + ".tmp";
        await File.WriteAllTextAsync(temporaryFile, json, cancellationToken);
        File.Move(temporaryFile, runtimePaths.ConfigurationFile, overwrite: true);
    }

    private async Task<OpsOptions> ReadCurrentAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(runtimePaths.ConfigurationFile))
        {
            return Clone(options.CurrentValue);
        }

        try
        {
            var json = await File.ReadAllTextAsync(runtimePaths.ConfigurationFile, cancellationToken);
            var document = JsonSerializer.Deserialize<OpsConfigurationDocument>(json, SerializerOptions);
            return document?.Ops is null ? Clone(options.CurrentValue) : Clone(document.Ops);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "File cấu hình môi trường không hợp lệ. Không ghi đè cho tới khi JSON được xử lý.",
                exception);
        }
    }

    private static void Validate(ManagedEnvironmentOptions environment)
    {
        if (string.IsNullOrWhiteSpace(environment.Key)
            || environment.Key.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new InvalidOperationException("Mã môi trường chỉ được chứa chữ, số, dấu gạch ngang hoặc gạch dưới.");
        }

        if (string.IsNullOrWhiteSpace(environment.DisplayName))
        {
            throw new InvalidOperationException("Tên hiển thị môi trường không được để trống.");
        }

        if (environment.Oracle.Port is <= 0 or > 65535)
        {
            throw new InvalidOperationException("Port Oracle phải nằm trong khoảng 1–65535.");
        }
    }

    private static OpsOptions Clone(OpsOptions source) => new()
    {
        MigrationSqlPath = source.MigrationSqlPath,
        BackupRootLinux = source.BackupRootLinux,
        BackupRootWindows = source.BackupRootWindows,
        BackupRoot = source.BackupRoot,
        DataPumpExportVersion = source.DataPumpExportVersion,
        UseHttpsRedirection = source.UseHttpsRedirection,
        ProtectKeysWithDpapi = source.ProtectKeysWithDpapi,
        RequireEncryptedDataProtectionKeys = source.RequireEncryptedDataProtectionKeys,
        DataProtectionCertificatePath = source.DataProtectionCertificatePath,
        DataProtectionCertificatePasswordFile = source.DataProtectionCertificatePasswordFile,
        AllowProductionOperations = source.AllowProductionOperations,
        ExecutionMode = source.ExecutionMode,
        LocalDeployment = new LocalDeploymentOptions
        {
            RootDirectory = source.LocalDeployment.RootDirectory,
            ComposeFile = source.LocalDeployment.ComposeFile,
            EnvironmentFile = source.LocalDeployment.EnvironmentFile,
            BackendService = source.LocalDeployment.BackendService,
            FrontendService = source.LocalDeployment.FrontendService,
            GotenbergService = source.LocalDeployment.GotenbergService
        },
        Authentication = new OpsAuthenticationOptions
        {
            Enabled = source.Authentication.Enabled,
            AdminUser = source.Authentication.AdminUser,
            PasswordHashFile = source.Authentication.PasswordHashFile,
            SessionHours = source.Authentication.SessionHours
        },
        SshKnownHostsPath = source.SshKnownHostsPath,
        SshConnectTimeoutSeconds = source.SshConnectTimeoutSeconds,
        RemoteCommandTimeoutSeconds = source.RemoteCommandTimeoutSeconds,
        LongRunningCommandTimeoutSeconds = source.LongRunningCommandTimeoutSeconds,
        ArtifactUploadTimeoutSeconds = source.ArtifactUploadTimeoutSeconds,
        DiskWarningPercent = source.DiskWarningPercent,
        Environments = source.Environments.Select(Clone).ToList(),
        Hosts = source.Hosts.Select(Clone).ToList()
    };

    private static ManagedHostOptions Clone(ManagedHostOptions source) => new()
    {
        Key = source.Key.Trim(),
        DisplayName = source.DisplayName.Trim(),
        Role = source.Role.Trim(),
        Tier = source.Tier.Trim(),
        EnvironmentKey = source.EnvironmentKey.Trim(),
        Host = source.Host.Trim(),
        Port = source.Port,
        Username = source.Username.Trim(),
        IdentityFile = source.IdentityFile.Trim(),
        ScriptDirectory = source.ScriptDirectory.Trim(),
        Enabled = source.Enabled,
        AllowedActions = source.AllowedActions.Select(action => action.Trim()).ToList()
    };

    private static ManagedEnvironmentOptions Clone(ManagedEnvironmentOptions source) => new()
    {
        Key = source.Key.Trim(),
        DisplayName = source.DisplayName.Trim(),
        Tier = source.Tier.Trim(),
        Enabled = source.Enabled,
        Oracle = new OracleEnvironmentOptions
        {
            Host = source.Oracle.Host.Trim(),
            Port = source.Oracle.Port,
            ServiceName = source.Oracle.ServiceName.Trim(),
            Schema = source.Oracle.Schema.Trim(),
            DataPumpDirectory = source.Oracle.DataPumpDirectory.Trim(),
            UserEnvironmentVariable = source.Oracle.UserEnvironmentVariable.Trim(),
            PasswordEnvironmentVariable = source.Oracle.PasswordEnvironmentVariable.Trim()
        },
        Safety = new EnvironmentSafetyOptions
        {
            AllowBackup = source.Safety.AllowBackup,
            AllowRestore = source.Safety.AllowRestore,
            AllowMigrate = source.Safety.AllowMigrate
        }
    };

    private sealed class OpsConfigurationDocument
    {
        public OpsConfigurationDocument()
        {
        }

        public OpsConfigurationDocument(OpsOptions ops)
        {
            Ops = ops;
        }

        public OpsOptions? Ops { get; set; }
    }
}
