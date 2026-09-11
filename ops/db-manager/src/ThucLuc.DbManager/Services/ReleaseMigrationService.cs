using System.Diagnostics;
using Microsoft.Extensions.Options;
using ThucLuc.DbManager.Configuration;
using ThucLuc.DbManager.Models;

namespace ThucLuc.DbManager.Services;

public interface IReleaseMigrationService
{
    Task<string> BackupAndMigrateAsync(
        ReleaseBundle bundle,
        string environmentKey,
        CancellationToken cancellationToken = default);
}

public sealed class ReleaseMigrationService(
    IOptionsMonitor<OpsOptions> options,
    IOracleCredentialResolver credentialResolver,
    IOracleBackupService backupService,
    ILogger<ReleaseMigrationService> logger) : IReleaseMigrationService
{
    private const int MaxCapturedCharacters = 30_000;
    private readonly SemaphoreSlim _migrationLock = new(1, 1);

    public async Task<string> BackupAndMigrateAsync(
        ReleaseBundle bundle,
        string environmentKey,
        CancellationToken cancellationToken = default)
    {
        var environment = options.CurrentValue.Environments.SingleOrDefault(item =>
            item.Enabled && string.Equals(item.Key, environmentKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Không tìm thấy môi trường database đang bật: {environmentKey}.");

        if (!environment.Safety.AllowMigrate)
        {
            throw new InvalidOperationException(
                $"Môi trường {environment.DisplayName} chưa cho phép chạy Flyway (AllowMigrate=false).");
        }
        if (!environment.Safety.AllowBackup)
        {
            throw new InvalidOperationException(
                $"Môi trường {environment.DisplayName} chưa cho phép sao lưu trước khi chạy Flyway.");
        }
        if (!bundle.HasFlyway || bundle.Manifest.Migrations.Count == 0)
        {
            throw new InvalidOperationException(
                "Release backend thiếu Flyway image hoặc thư mục migration SQL.");
        }

        var migrationFiles = Directory.EnumerateFiles(bundle.MigrationDirectory, "*.sql", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Cast<string>()
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var manifestFiles = bundle.Manifest.Migrations
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (migrationFiles.Count != manifestFiles.Count
            || !migrationFiles.SequenceEqual(manifestFiles, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Danh sách Flyway SQL không khớp release manifest.");
        }

        var credentialResolution = await credentialResolver.ResolveAsync(
            environment,
            includeCredential: true,
            cancellationToken);
        var credential = credentialResolution.Credential
            ?? throw new InvalidOperationException(
                "Chưa lưu tài khoản schema Oracle. Hãy cấu hình kết nối database trong Ops Console trước khi triển khai.");

        await _migrationLock.WaitAsync(cancellationToken);
        try
        {
            var backup = await backupService.CreateAsync(environment, cancellationToken);
            if (!backup.Succeeded)
            {
                throw new InvalidOperationException($"Sao lưu database thất bại: {backup.Message}");
            }

            var load = await RunDockerAsync(
                ["load", "--input", bundle.FlywayArchive],
                cancellationToken,
                longRunning: true);
            EnsureSucceeded(load, "Nạp Flyway image");

            var containerName = $"thucluc-flyway-{Sanitize(bundle.Manifest.Version)}-{Guid.NewGuid():N}";
            if (containerName.Length > 120) containerName = containerName[..120];
            var variables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["FLYWAY_URL"] = $"jdbc:oracle:thin:@//{environment.Oracle.Host}:{environment.Oracle.Port}/{environment.Oracle.ServiceName}",
                ["FLYWAY_USER"] = credential.UserName,
                ["FLYWAY_PASSWORD"] = credential.Password,
                ["FLYWAY_SCHEMAS"] = environment.Oracle.Schema,
                ["FLYWAY_DEFAULT_SCHEMA"] = environment.Oracle.Schema,
                ["FLYWAY_LOCATIONS"] = "filesystem:/flyway/sql"
            };

            try
            {
                var createArguments = new List<string>
                {
                    "create", "--name", containerName, "--network", "host"
                };
                foreach (var variable in variables.Keys)
                {
                    createArguments.Add("--env");
                    createArguments.Add(variable);
                }
                createArguments.Add(bundle.Manifest.FlywayImage);
                createArguments.Add("-connectRetries=10");
                createArguments.Add("-validateMigrationNaming=true");
                createArguments.Add("migrate");

                var create = await RunDockerAsync(createArguments, cancellationToken, environmentVariables: variables);
                EnsureSucceeded(create, "Tạo Flyway runner");

                var source = Path.Combine(bundle.MigrationDirectory, ".");
                var copy = await RunDockerAsync(
                    ["cp", source, $"{containerName}:/flyway/sql"],
                    cancellationToken,
                    longRunning: true);
                EnsureSucceeded(copy, "Đưa migration SQL vào Flyway runner");

                var migrate = await RunDockerAsync(
                    ["start", "--attach", containerName],
                    cancellationToken,
                    longRunning: true);
                EnsureSucceeded(migrate, "Chạy Flyway migrate");

                return $"{backup.Message} Flyway đã cập nhật database bằng {migrationFiles.Count} file trong release.";
            }
            finally
            {
                await RunDockerAsync(
                    ["rm", "--force", containerName],
                    CancellationToken.None,
                    longRunning: false,
                    ignoreCancellationTimeout: true);
            }
        }
        finally
        {
            _migrationLock.Release();
        }
    }

    private async Task<DockerResult> RunDockerAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        bool longRunning = false,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        bool ignoreCancellationTimeout = false)
    {
        var timeoutSeconds = longRunning
            ? options.CurrentValue.LongRunningCommandTimeoutSeconds
            : options.CurrentValue.RemoteCommandTimeoutSeconds;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 21_600)));

        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        if (environmentVariables is not null)
        {
            foreach (var variable in environmentVariables) startInfo.Environment[variable.Key] = variable.Value;
        }

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Không thể khởi động Docker CLI.");
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var result = new DockerResult(process.ExitCode, Truncate(await outputTask), Truncate(await errorTask));
            logger.LogInformation(
                "Flyway Docker action {Action} kết thúc với mã {ExitCode}",
                arguments.FirstOrDefault(),
                process.ExitCode);
            return result;
        }
        catch (OperationCanceledException) when (ignoreCancellationTimeout)
        {
            return new DockerResult(1, string.Empty, "Docker cleanup quá thời gian cho phép.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Docker/Flyway không hoàn tất trong thời gian cho phép.");
        }
    }

    private static void EnsureSucceeded(DockerResult result, string step)
    {
        if (result.ExitCode == 0) return;
        var detail = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
        throw new InvalidOperationException($"{step} thất bại: {detail}");
    }

    private static string Sanitize(string value) => new(
        value.ToLowerInvariant().Select(character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'
                ? character
                : '-').ToArray());

    private static string Truncate(string value) => value.Length <= MaxCapturedCharacters
        ? value.Trim()
        : value[^MaxCapturedCharacters..].Trim();

    private sealed record DockerResult(int ExitCode, string Output, string Error);
}
