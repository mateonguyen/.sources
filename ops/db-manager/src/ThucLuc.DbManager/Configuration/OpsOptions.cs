namespace ThucLuc.DbManager.Configuration;

public sealed class OpsOptions
{
    public const string SectionName = "Ops";

    public string MigrationSqlPath { get; set; } = "../../../../backend/db/flyway/sql";

    public string BackupRootLinux { get; set; } = "/var/lib/thucluc-db-manager/backups";

    public string BackupRootWindows { get; set; } = @"D:\ThucLucData\DbBackups";

    // Tương thích với các file cấu hình cũ. Khi có giá trị, khóa này được ưu tiên.
    public string BackupRoot { get; set; } = string.Empty;

    // Khi chưa kiểm tra DB đích, giữ nguyên định dạng theo DB nguồn.
    // Luồng sao chép sẽ tự ghi đè bằng phiên bản DB đích đã phát hiện.
    public string DataPumpExportVersion { get; set; } = "COMPATIBLE";

    public bool UseHttpsRedirection { get; set; }

    public bool ProtectKeysWithDpapi { get; set; } = true;

    public bool RequireEncryptedDataProtectionKeys { get; set; }

    public string DataProtectionCertificatePath { get; set; } = string.Empty;

    public string DataProtectionCertificatePasswordFile { get; set; } = string.Empty;

    public bool AllowProductionOperations { get; set; }

    public string ExecutionMode { get; set; } = "Ssh";

    public LocalDeploymentOptions LocalDeployment { get; set; } = new();

    public OpsAuthenticationOptions Authentication { get; set; } = new();

    public string SshKnownHostsPath { get; set; } = string.Empty;

    public int SshConnectTimeoutSeconds { get; set; } = 10;

    public int RemoteCommandTimeoutSeconds { get; set; } = 120;

    public int LongRunningCommandTimeoutSeconds { get; set; } = 7_200;

    public int ArtifactUploadTimeoutSeconds { get; set; } = 1_800;

    public int DiskWarningPercent { get; set; } = 80;

    public List<ManagedEnvironmentOptions> Environments { get; set; } = [];

    public List<ManagedHostOptions> Hosts { get; set; } = [];
}

public sealed class LocalDeploymentOptions
{
    public string RootDirectory { get; set; } = "/opt/thucluc-test";

    public string ComposeFile { get; set; } = "compose.yml";

    public string EnvironmentFile { get; set; } = ".env";

    public string BackendService { get; set; } = "backend";

    public string FrontendService { get; set; } = "frontend";

    public string GotenbergService { get; set; } = "gotenberg";
}

public sealed class OpsAuthenticationOptions
{
    public bool Enabled { get; set; }

    public string AdminUser { get; set; } = "admin";

    public string PasswordHashFile { get; set; } = "/run/secrets/admin-password-hash";

    public int SessionHours { get; set; } = 8;
}

public sealed class ManagedHostOptions
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Tier { get; set; } = "Test";

    public string EnvironmentKey { get; set; } = "test";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 22;

    public string Username { get; set; } = "thucluc-ops";

    public string IdentityFile { get; set; } = string.Empty;

    public string ScriptDirectory { get; set; } = "/opt/thucluc-ops/bin";

    public bool Enabled { get; set; }

    public List<string> AllowedActions { get; set; } = [];
}

public sealed class ManagedEnvironmentOptions
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Tier { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public OracleEnvironmentOptions Oracle { get; set; } = new();

    public EnvironmentSafetyOptions Safety { get; set; } = new();
}

public sealed class OracleEnvironmentOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 1521;

    public string ServiceName { get; set; } = string.Empty;

    public string Schema { get; set; } = string.Empty;

    public string DataPumpDirectory { get; set; } = string.Empty;

    public string UserEnvironmentVariable { get; set; } = string.Empty;

    public string PasswordEnvironmentVariable { get; set; } = string.Empty;
}

public sealed class EnvironmentSafetyOptions
{
    public bool AllowBackup { get; set; }

    public bool AllowRestore { get; set; }

    public bool AllowMigrate { get; set; }
}
