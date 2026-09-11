using Microsoft.Extensions.Options;

namespace ThucLuc.DbManager.Configuration;

public sealed record BackupStoragePaths(
    string Root,
    string Repository,
    string Inbox,
    string Work,
    string Quarantine,
    string LegacyRoot);

public sealed class BackupStoragePathResolver(
    IOptionsMonitor<OpsOptions> options,
    IWebHostEnvironment hostEnvironment)
{
    public BackupStoragePaths ResolveAndEnsure()
    {
        var current = options.CurrentValue;
        var configuredPath = !string.IsNullOrWhiteSpace(current.BackupRoot)
            ? current.BackupRoot
            : OperatingSystem.IsWindows()
                ? current.BackupRootWindows
                : current.BackupRootLinux;

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException(
                OperatingSystem.IsWindows()
                    ? "Chưa cấu hình Ops:BackupRootWindows."
                    : "Chưa cấu hình Ops:BackupRootLinux.");
        }

        var root = Path.GetFullPath(Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(hostEnvironment.ContentRootPath, configuredPath));
        var paths = new BackupStoragePaths(
            root,
            Path.Combine(root, "repository"),
            Path.Combine(root, "inbox"),
            Path.Combine(root, "work"),
            Path.Combine(root, "quarantine"),
            Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, "../../backups")));

        Directory.CreateDirectory(paths.Repository);
        Directory.CreateDirectory(paths.Inbox);
        Directory.CreateDirectory(paths.Work);
        Directory.CreateDirectory(paths.Quarantine);
        return paths;
    }
}
