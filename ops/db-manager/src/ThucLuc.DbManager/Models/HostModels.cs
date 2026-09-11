namespace ThucLuc.DbManager.Models;

public enum HostAction
{
    StatusBackend,
    StageBackendRelease,
    DeployBackend,
    RollbackBackend,
    LogsBackend,
    StatusFrontend,
    StageFrontendRelease,
    DeployFrontend,
    RollbackFrontend,
    LogsFrontend,
    StatusInfra,
    BackupMinio,
    StageGotenbergRelease,
    StageMinioRelease,
    UpdateGotenberg,
    UpdateMinio
}

public enum HostArtifactKind
{
    Backend,
    Frontend,
    Gotenberg,
    Minio
}

public sealed record HostConnectionResult(
    bool Succeeded,
    string Message,
    string? Detail = null);

public sealed record HostCommandResult(
    bool Succeeded,
    int ExitCode,
    string Output,
    string Error);
