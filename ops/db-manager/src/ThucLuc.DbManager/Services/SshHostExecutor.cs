using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ThucLuc.DbManager.Configuration;
using ThucLuc.DbManager.Models;

namespace ThucLuc.DbManager.Services;

public interface IHostExecutor
{
    Task<HostConnectionResult> CheckAsync(
        ManagedHostOptions host,
        CancellationToken cancellationToken = default);

    Task<HostCommandResult> ExecuteAsync(
        ManagedHostOptions host,
        HostAction action,
        string? releaseVersion = null,
        CancellationToken cancellationToken = default);

    Task<HostCommandResult> UploadAsync(
        ManagedHostOptions host,
        HostArtifactKind artifactKind,
        string releaseVersion,
        string localFile,
        CancellationToken cancellationToken = default);
}

public sealed partial class SshHostExecutor(
    IOptionsMonitor<OpsOptions> options,
    ILogger<SshHostExecutor> logger) : IHostExecutor
{
    private const int MaxCapturedCharacters = 20_000;

    private static readonly IReadOnlyDictionary<HostAction, string> ScriptNames =
        new Dictionary<HostAction, string>
        {
            [HostAction.StatusBackend] = "status-backend",
            [HostAction.StageBackendRelease] = "stage-backend-release",
            [HostAction.DeployBackend] = "deploy-backend",
            [HostAction.RollbackBackend] = "rollback-backend",
            [HostAction.LogsBackend] = "logs-backend",
            [HostAction.StatusFrontend] = "status-frontend",
            [HostAction.StageFrontendRelease] = "stage-frontend-release",
            [HostAction.DeployFrontend] = "deploy-frontend",
            [HostAction.RollbackFrontend] = "rollback-frontend",
            [HostAction.LogsFrontend] = "logs-frontend",
            [HostAction.StatusInfra] = "status-infra",
            [HostAction.BackupMinio] = "backup-minio",
            [HostAction.StageGotenbergRelease] = "stage-gotenberg-release",
            [HostAction.StageMinioRelease] = "stage-minio-release",
            [HostAction.UpdateGotenberg] = "update-gotenberg",
            [HostAction.UpdateMinio] = "update-minio"
        };

    public async Task<HostConnectionResult> CheckAsync(
        ManagedHostOptions host,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateHost(host);
            var result = await RunSshAsync(host, ["/usr/bin/true"], cancellationToken);
            return result.Succeeded
                ? new HostConnectionResult(true, "Kết nối SSH thành công.")
                : new HostConnectionResult(false, "Không thể kết nối SSH.", CombineOutput(result));
        }
        catch (Exception exception)
        {
            return new HostConnectionResult(false, "Không thể kết nối SSH.", exception.GetBaseException().Message);
        }
    }

    public Task<HostCommandResult> ExecuteAsync(
        ManagedHostOptions host,
        HostAction action,
        string? releaseVersion = null,
        CancellationToken cancellationToken = default)
    {
        ValidateHost(host);

        var actionName = action.ToString();
        if (!host.AllowedActions.Contains(actionName, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Máy chủ {host.DisplayName} không cho phép thao tác {actionName}.");
        }

        var scriptName = ScriptNames[action];
        var scriptPath = BuildScriptPath(host.ScriptDirectory, scriptName);
        var arguments = new List<string> { "sudo", "--non-interactive", scriptPath };

        if (action is HostAction.StageBackendRelease
            or HostAction.StageFrontendRelease
            or HostAction.DeployBackend
            or HostAction.DeployFrontend
            or HostAction.StageGotenbergRelease
            or HostAction.StageMinioRelease
            or HostAction.UpdateGotenberg
            or HostAction.UpdateMinio)
        {
            if (string.IsNullOrWhiteSpace(releaseVersion) || !SafeTokenRegex().IsMatch(releaseVersion))
            {
                throw new InvalidOperationException(
                    "Phiên bản chỉ được chứa chữ, số, dấu chấm, gạch ngang và gạch dưới.");
            }

            arguments.Add(releaseVersion);
        }

        var timeoutSeconds = action is HostAction.BackupMinio or HostAction.UpdateMinio
            ? options.CurrentValue.LongRunningCommandTimeoutSeconds
            : (int?)null;
        return RunSshAsync(host, arguments, cancellationToken, timeoutSeconds);
    }

    public async Task<HostCommandResult> UploadAsync(
        ManagedHostOptions host,
        HostArtifactKind artifactKind,
        string releaseVersion,
        string localFile,
        CancellationToken cancellationToken = default)
    {
        ValidateHost(host);
        if (!SafeTokenRegex().IsMatch(releaseVersion))
        {
            throw new InvalidOperationException("Phiên bản release không hợp lệ.");
        }

        var requiredAction = artifactKind switch
        {
            HostArtifactKind.Backend => HostAction.StageBackendRelease,
            HostArtifactKind.Frontend => HostAction.StageFrontendRelease,
            HostArtifactKind.Gotenberg => HostAction.StageGotenbergRelease,
            HostArtifactKind.Minio => HostAction.StageMinioRelease,
            _ => throw new ArgumentOutOfRangeException(nameof(artifactKind), artifactKind, null)
        };
        if (!host.AllowedActions.Contains(requiredAction.ToString(), StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Máy chủ {host.DisplayName} không cho phép tiếp nhận image {artifactKind}.");
        }

        var resolvedFile = Path.GetFullPath(localFile);
        if (!File.Exists(resolvedFile))
        {
            throw new FileNotFoundException("Không tìm thấy Docker image cần upload.", resolvedFile);
        }

        var component = artifactKind switch
        {
            HostArtifactKind.Backend => "backend",
            HostArtifactKind.Frontend => "frontend",
            HostArtifactKind.Gotenberg => "gotenberg",
            HostArtifactKind.Minio => "minio",
            _ => throw new ArgumentOutOfRangeException(nameof(artifactKind), artifactKind, null)
        };
        var remoteFile = $"/tmp/thucluc-{component}-{releaseVersion}.tar.uploading";
        var settings = options.CurrentValue;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.ArtifactUploadTimeoutSeconds, 30, 7_200)));

        var startInfo = new ProcessStartInfo
        {
            FileName = "scp",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-q");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add("BatchMode=yes");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add($"ConnectTimeout={Math.Clamp(settings.SshConnectTimeoutSeconds, 3, 60)}");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add("StrictHostKeyChecking=yes");
        if (!string.IsNullOrWhiteSpace(settings.SshKnownHostsPath))
        {
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add($"UserKnownHostsFile={settings.SshKnownHostsPath}");
        }
        if (!string.IsNullOrWhiteSpace(host.IdentityFile))
        {
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(host.IdentityFile);
        }
        startInfo.ArgumentList.Add("-P");
        startInfo.ArgumentList.Add(host.Port.ToString());
        startInfo.ArgumentList.Add(resolvedFile);
        startInfo.ArgumentList.Add($"{host.Username}@{host.Host}:{remoteFile}");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Không thể khởi động SCP client.");
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return new HostCommandResult(
                process.ExitCode == 0,
                process.ExitCode,
                Truncate(await outputTask),
                Truncate(await errorTask));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException($"Upload image tới {host.DisplayName} quá thời gian cho phép.");
        }
    }

    private async Task<HostCommandResult> RunSshAsync(
        ManagedHostOptions host,
        IReadOnlyList<string> remoteArguments,
        CancellationToken cancellationToken,
        int? timeoutSeconds = null)
    {
        var settings = options.CurrentValue;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds.HasValue
            ? Math.Clamp(timeoutSeconds.Value, 60, 21_600)
            : Math.Clamp(settings.RemoteCommandTimeoutSeconds, 5, 3_600)));

        var startInfo = new ProcessStartInfo
        {
            FileName = "ssh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add("BatchMode=yes");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add($"ConnectTimeout={Math.Clamp(settings.SshConnectTimeoutSeconds, 3, 60)}");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add("StrictHostKeyChecking=yes");

        if (!string.IsNullOrWhiteSpace(settings.SshKnownHostsPath))
        {
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add($"UserKnownHostsFile={settings.SshKnownHostsPath}");
        }

        if (!string.IsNullOrWhiteSpace(host.IdentityFile))
        {
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(host.IdentityFile);
        }

        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add(host.Port.ToString());
        startInfo.ArgumentList.Add($"{host.Username}@{host.Host}");
        foreach (var argument in remoteArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Không thể khởi động OpenSSH client.");
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
            var output = Truncate(await outputTask);
            var error = Truncate(await errorTask);
            logger.LogInformation(
                "SSH action trên {HostKey} kết thúc với mã {ExitCode}",
                host.Key,
                process.ExitCode);
            return new HostCommandResult(process.ExitCode == 0, process.ExitCode, output, error);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException(
                $"Máy chủ {host.DisplayName} không hoàn tất thao tác trong thời gian cho phép.");
        }
    }

    private static void ValidateHost(ManagedHostOptions host)
    {
        if (!host.Enabled)
        {
            throw new InvalidOperationException($"Máy chủ {host.DisplayName} đang bị tắt trong cấu hình.");
        }

        if (!SafeHostRegex().IsMatch(host.Host)
            || !SafeUserRegex().IsMatch(host.Username)
            || host.Port is <= 0 or > 65_535)
        {
            throw new InvalidOperationException("Thông tin SSH của máy chủ không hợp lệ.");
        }

        if (!string.IsNullOrWhiteSpace(host.IdentityFile) && !File.Exists(host.IdentityFile))
        {
            throw new InvalidOperationException($"Không tìm thấy SSH identity file: {host.IdentityFile}");
        }
    }

    private static string BuildScriptPath(string directory, string scriptName)
    {
        var normalized = directory.TrimEnd('/');
        if (!SafeAbsolutePathRegex().IsMatch(normalized))
        {
            throw new InvalidOperationException("Thư mục script trên máy chủ không hợp lệ.");
        }

        return $"{normalized}/{scriptName}";
    }

    private static string CombineOutput(HostCommandResult result)
    {
        var detail = string.Join(Environment.NewLine, new[] { result.Error, result.Output }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(detail) ? $"SSH trả về mã {result.ExitCode}." : detail;
    }

    private static string Truncate(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= MaxCapturedCharacters
            ? trimmed
            : trimmed[..MaxCapturedCharacters] + Environment.NewLine + "[đã rút gọn]";
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Tiến trình có thể đã kết thúc giữa hai lần kiểm tra.
        }
    }

    [GeneratedRegex("^[A-Za-z0-9._:-]+$")]
    private static partial Regex SafeHostRegex();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_-]*$")]
    private static partial Regex SafeUserRegex();

    [GeneratedRegex("^/[A-Za-z0-9._/-]+$")]
    private static partial Regex SafeAbsolutePathRegex();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")]
    private static partial Regex SafeTokenRegex();
}
