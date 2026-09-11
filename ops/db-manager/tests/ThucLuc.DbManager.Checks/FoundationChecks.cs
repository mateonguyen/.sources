using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ThucLuc.DbManager.Configuration;
using ThucLuc.DbManager.Models;
using ThucLuc.DbManager.Services;

namespace ThucLuc.DbManager.Checks;

internal static class FoundationChecks
{
    public static void Run(CheckSuite checks)
    {
        checks.Run("SQLite journal persists operations", CheckSqliteJournal);
        checks.RunAsync("Release package verifies and persists", CheckReleasePackageAsync).GetAwaiter().GetResult();
        checks.RunAsync("Release package accepts backend and frontend only", CheckApplicationOnlyPackageAsync).GetAwaiter().GetResult();
        checks.RunAsync("Release package rejects incomplete infra image", CheckIncompleteInfraPackageAsync).GetAwaiter().GetResult();
        checks.RunAsync("Release package rejects path traversal", CheckReleaseTraversalAsync).GetAwaiter().GetResult();
        checks.RunAsync("Disabled SSH host is rejected before process start", CheckDisabledSshHostAsync).GetAwaiter().GetResult();
        checks.Run("Admin password hash verifies safely", CheckAdminPasswordHash);
        checks.Run("Admin credential validator reads configured hash", CheckAdminCredentialValidator);
    }

    private static void CheckSqliteJournal()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var paths = new OpsRuntimePaths(Path.Combine(root, "config.json"), root);
            var first = new SqliteOperationJournal(paths);
            var entry = first.AddWaiting("Deploy", "test", "Waiting");

            var second = new SqliteOperationJournal(paths);
            var persisted = second.GetRecent().Single(item => item.Id == entry.Id);
            CheckSuite.Equal(OperationState.Waiting, persisted.State);
            CheckSuite.Equal(1, second.RecoverInterrupted());
            CheckSuite.Equal(OperationState.Failed, second.GetRecent().Single(item => item.Id == entry.Id).State);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task CheckReleasePackageAsync()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            const string version = "1.2.3";
            await File.WriteAllTextAsync(Path.Combine(source, $"backend-{version}.tar"), "backend");
            await File.WriteAllTextAsync(Path.Combine(source, $"frontend-{version}.tar"), "frontend");
            await File.WriteAllTextAsync(Path.Combine(source, $"ops-console-{version}.tar"), "ops-console");
            var manifest = new ReleaseManifest
            {
                Version = version,
                BackendImage = $"thucluc-backend:{version}",
                FrontendImage = $"thucluc-frontend:{version}",
                OpsConsoleImage = $"thucluc-ops-console:{version}",
                MinimumOracleVersion = "19",
                CommitSha = "abc123",
                CreatedAt = DateTimeOffset.UtcNow,
                Migrations = []
            };
            await File.WriteAllTextAsync(
                Path.Combine(source, "release-manifest.json"),
                JsonSerializer.Serialize(manifest));
            await WriteChecksumsAsync(source);

            var archive = Path.Combine(root, "release.zip");
            ZipFile.CreateFromDirectory(source, archive);
            var paths = new OpsRuntimePaths(Path.Combine(root, "config.json"), Path.Combine(root, "state"));
            var service = new ReleasePackageService(paths, NullLogger<ReleasePackageService>.Instance);
            await using var stream = File.OpenRead(archive);
            var result = await service.ImportAsync("release.zip", stream);

            CheckSuite.Equal(true, result.Succeeded);
            CheckSuite.Equal(version, result.Bundle?.Manifest.Version);
            CheckSuite.Equal(1, service.GetAll().Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task CheckReleaseTraversalAsync()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var archivePath = Path.Combine(root, "bad.zip");
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("../escape.txt");
                await using var writer = new StreamWriter(entry.Open());
                await writer.WriteAsync("blocked");
            }

            var paths = new OpsRuntimePaths(Path.Combine(root, "config.json"), Path.Combine(root, "state"));
            var service = new ReleasePackageService(paths, NullLogger<ReleasePackageService>.Instance);
            await using var stream = File.OpenRead(archivePath);
            var result = await service.ImportAsync("bad.zip", stream);
            CheckSuite.Equal(false, result.Succeeded);
            CheckSuite.Equal(false, File.Exists(Path.Combine(root, "escape.txt")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task CheckApplicationOnlyPackageAsync()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            const string version = "app-1.0.0";
            await File.WriteAllTextAsync(Path.Combine(source, $"backend-{version}.tar"), "backend");
            await File.WriteAllTextAsync(Path.Combine(source, $"frontend-{version}.tar"), "frontend");
            var manifest = new ReleaseManifest
            {
                Version = version,
                BackendImage = $"thucluc-backend:{version}",
                FrontendImage = $"thucluc-frontend:{version}",
                CreatedAt = DateTimeOffset.UtcNow
            };
            await File.WriteAllTextAsync(
                Path.Combine(source, "release-manifest.json"),
                JsonSerializer.Serialize(manifest));
            await WriteChecksumsAsync(source);

            var archive = Path.Combine(root, "release.zip");
            ZipFile.CreateFromDirectory(source, archive);
            var paths = new OpsRuntimePaths(Path.Combine(root, "config.json"), Path.Combine(root, "state"));
            var service = new ReleasePackageService(paths, NullLogger<ReleasePackageService>.Instance);
            await using var stream = File.OpenRead(archive);
            var result = await service.ImportAsync("release.zip", stream);

            CheckSuite.Equal(true, result.Succeeded);
            CheckSuite.Equal(true, result.Bundle?.HasBackend);
            CheckSuite.Equal(true, result.Bundle?.HasFrontend);
            CheckSuite.Equal(false, result.Bundle?.HasOpsConsole);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void CheckAdminPasswordHash()
    {
        const string password = "Correct-Horse-2026";
        var hash = AdminPasswordHasher.Hash(password);
        CheckSuite.Equal(true, AdminPasswordHasher.Verify(password, hash));
        CheckSuite.Equal(false, AdminPasswordHasher.Verify("wrong-password", hash));
        CheckSuite.Equal(false, AdminPasswordHasher.Verify(password, "not-a-hash"));
    }

    private static void CheckAdminCredentialValidator()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var hashFile = Path.Combine(root, "admin-password-hash");
            File.WriteAllText(hashFile, AdminPasswordHasher.Hash("Correct-Horse-2026"));
            var options = new OpsOptions
            {
                Authentication = new OpsAuthenticationOptions
                {
                    Enabled = true,
                    AdminUser = "admin",
                    PasswordHashFile = hashFile
                }
            };
            var validator = new AdminCredentialValidator(new FixedOptionsMonitor<OpsOptions>(options));
            CheckSuite.Equal(true, validator.Validate("admin", "Correct-Horse-2026"));
            CheckSuite.Equal(false, validator.Validate("admin", "wrong-password"));
            CheckSuite.Equal(false, validator.Validate("other", "Correct-Horse-2026"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task CheckIncompleteInfraPackageAsync()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            const string version = "2.0.0";
            await File.WriteAllTextAsync(Path.Combine(source, $"backend-{version}.tar"), "backend");
            await File.WriteAllTextAsync(Path.Combine(source, $"frontend-{version}.tar"), "frontend");
            await File.WriteAllTextAsync(Path.Combine(source, $"ops-console-{version}.tar"), "ops-console");
            var manifest = new ReleaseManifest
            {
                Version = version,
                BackendImage = $"thucluc-backend:{version}",
                FrontendImage = $"thucluc-frontend:{version}",
                OpsConsoleImage = $"thucluc-ops-console:{version}",
                GotenbergImage = $"thucluc-gotenberg:{version}",
                CreatedAt = DateTimeOffset.UtcNow
            };
            await File.WriteAllTextAsync(
                Path.Combine(source, "release-manifest.json"),
                JsonSerializer.Serialize(manifest));
            await WriteChecksumsAsync(source);

            var archive = Path.Combine(root, "release.zip");
            ZipFile.CreateFromDirectory(source, archive);
            var paths = new OpsRuntimePaths(Path.Combine(root, "config.json"), Path.Combine(root, "state"));
            var service = new ReleasePackageService(paths, NullLogger<ReleasePackageService>.Instance);
            await using var stream = File.OpenRead(archive);
            var result = await service.ImportAsync("release.zip", stream);

            CheckSuite.Equal(false, result.Succeeded);
            CheckSuite.Contains("Gotenberg", result.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task CheckDisabledSshHostAsync()
    {
        var executor = new SshHostExecutor(
            new FixedOptionsMonitor<OpsOptions>(new OpsOptions()),
            NullLogger<SshHostExecutor>.Instance);
        var result = await executor.CheckAsync(new ManagedHostOptions
        {
            DisplayName = "Disabled",
            Host = "127.0.0.1",
            Username = "thucluc-ops",
            Enabled = false
        });
        CheckSuite.Equal(false, result.Succeeded);
        CheckSuite.Contains("đang bị tắt", result.Detail ?? string.Empty);
    }

    private static async Task WriteChecksumsAsync(string root)
    {
        var lines = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root).OrderBy(path => path))
        {
            if (Path.GetFileName(file) == "checksums.sha256") continue;
            await using var stream = File.OpenRead(file);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
            lines.Add($"{hash}  {Path.GetFileName(file)}");
        }
        await File.WriteAllLinesAsync(Path.Combine(root, "checksums.sha256"), lines);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "thucluc-ops-checks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
