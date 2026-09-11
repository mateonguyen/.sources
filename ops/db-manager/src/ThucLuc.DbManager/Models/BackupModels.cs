using ThucLuc.DbManager.Configuration;

namespace ThucLuc.DbManager.Models;

public enum BackupState
{
    Running,
    Succeeded,
    Failed
}

public enum RestoreTargetMode
{
    ExistingEmpty,
    CreateNew,
    ReplaceExisting
}

public sealed record BackupManifest
{
    public string Id { get; init; } = string.Empty;

    public string EnvironmentKey { get; init; } = string.Empty;

    public string EnvironmentName { get; init; } = string.Empty;

    public string Schema { get; init; } = string.Empty;

    public string DatabaseEndpoint { get; init; } = string.Empty;

    public string OracleDirectory { get; init; } = string.Empty;

    public string? OracleDirectoryPath { get; init; }

    public string? SourceOracleVersion { get; init; }

    public string? DataPumpVersion { get; init; }

    public int? SourceTimeZoneVersion { get; init; }

    public string DumpFileName { get; init; } = string.Empty;

    public string LogFileName { get; init; } = "export.log";

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public BackupState State { get; init; }

    public long? SizeBytes { get; init; }

    public string? Sha256 { get; init; }

    public string? Error { get; init; }

    public bool IsSafetyBackup { get; init; }
}

public sealed record BackupRunResult(bool Succeeded, BackupManifest Manifest, string Message);

public sealed record BackupUploadResult(BackupManifest Manifest, string RepositoryPath, string Message);

public sealed record RestoreRequest(
    BackupManifest Backup,
    ManagedEnvironmentOptions TargetEnvironment,
    string TargetSchema,
    RestoreTargetMode TargetMode,
    string SysPassword,
    string SchemaPassword,
    string DefaultTablespace,
    string ReplacementConfirmation);

public sealed record RestoreRunResult(
    bool Succeeded,
    string Message,
    string? LogPath = null,
    BackupManifest? SafetyBackup = null,
    IReadOnlyList<string>? Warnings = null,
    int? RestoredObjectCount = null);

public sealed class TargetSchemaNotEmptyException(string schema, int objectCount)
    : InvalidOperationException($"Schema {schema} đang có {objectCount} đối tượng.")
{
    public string Schema { get; } = schema;

    public int ObjectCount { get; } = objectCount;
}
