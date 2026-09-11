namespace ThucLuc.DbManager.Models;

public sealed record ReleaseManifest
{
    public string Version { get; init; } = string.Empty;

    public string BackendImage { get; init; } = string.Empty;

    public string FrontendImage { get; init; } = string.Empty;

    public string OpsConsoleImage { get; init; } = string.Empty;

    public string FlywayImage { get; init; } = string.Empty;

    public string GotenbergImage { get; init; } = string.Empty;

    public string MinioImage { get; init; } = string.Empty;

    public string MinimumOracleVersion { get; init; } = string.Empty;

    public string CommitSha { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public IReadOnlyList<string> Migrations { get; init; } = [];
}

public sealed record ReleaseBundle(
    ReleaseManifest Manifest,
    string Directory,
    string PackageSha256)
{
    public string BackendArchive => Path.Combine(Directory, $"backend-{Manifest.Version}.tar");

    public string FrontendArchive => Path.Combine(Directory, $"frontend-{Manifest.Version}.tar");

    public string OpsConsoleArchive => Path.Combine(Directory, $"ops-console-{Manifest.Version}.tar");

    public string FlywayArchive => Path.Combine(Directory, $"flyway-{Manifest.Version}.tar");

    public string MigrationDirectory => Path.Combine(Directory, "flyway", "sql");

    public string GotenbergArchive => Path.Combine(Directory, $"gotenberg-{Manifest.Version}.tar");

    public string MinioArchive => Path.Combine(Directory, $"minio-{Manifest.Version}.tar");

    public bool HasBackend => !string.IsNullOrWhiteSpace(Manifest.BackendImage)
        && File.Exists(BackendArchive);

    public bool HasFrontend => !string.IsNullOrWhiteSpace(Manifest.FrontendImage)
        && File.Exists(FrontendArchive);

    public bool HasOpsConsole => !string.IsNullOrWhiteSpace(Manifest.OpsConsoleImage)
        && File.Exists(OpsConsoleArchive);

    public bool HasFlyway => !string.IsNullOrWhiteSpace(Manifest.FlywayImage)
        && File.Exists(FlywayArchive)
        && System.IO.Directory.Exists(MigrationDirectory);

    public bool HasGotenberg => !string.IsNullOrWhiteSpace(Manifest.GotenbergImage)
        && File.Exists(GotenbergArchive);

    public bool HasMinio => !string.IsNullOrWhiteSpace(Manifest.MinioImage)
        && File.Exists(MinioArchive);
}

public sealed record ReleaseImportResult(
    bool Succeeded,
    string Message,
    ReleaseBundle? Bundle = null);

public sealed record ReleaseDeploymentSelection(
    string Version,
    bool DeployBackend,
    bool DeployFrontend,
    bool UpdateGotenberg = false,
    bool UpdateMinio = false);

public sealed record ReleaseRollbackSelection(
    bool RollbackBackend,
    bool RollbackFrontend);
