using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ThucLuc.DbManager.Configuration;
using ThucLuc.DbManager.Models;

namespace ThucLuc.DbManager.Services;

public sealed partial class LocalHostExecutor(
    IOptionsMonitor<OpsOptions> options,
    OpsRuntimePaths runtimePaths,
    ILogger<LocalHostExecutor> logger) : IHostExecutor
{
    private const int MaxCapturedCharacters = 20_000;
    private readonly SemaphoreSlim _deploymentLock = new(1, 1);

    public async Task<HostConnectionResult> CheckAsync(
        ManagedHostOptions host,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateHost(host);
            ValidateDeploymentFiles();
            var docker = await RunDockerAsync(["info", "--format", "{{.ServerVersion}}"], cancellationToken);
            if (!docker.Succeeded)
            {
                return new HostConnectionResult(false, "Docker trên máy hiện tại chưa sẵn sàng.", CombineOutput(docker));
            }

            var compose = await RunDockerAsync(["compose", "version", "--short"], cancellationToken);
            return compose.Succeeded
                ? new HostConnectionResult(true, "Máy hiện tại và Docker đã sẵn sàng.", $"Docker {docker.Output}; Compose {compose.Output}")
                : new HostConnectionResult(false, "Docker Compose chưa sẵn sàng.", CombineOutput(compose));
        }
        catch (Exception exception)
        {
            return new HostConnectionResult(false, "Không thể kiểm tra máy hiện tại.", exception.GetBaseException().Message);
        }
    }

    public async Task<HostCommandResult> ExecuteAsync(
        ManagedHostOptions host,
        HostAction action,
        string? releaseVersion = null,
        CancellationToken cancellationToken = default)
    {
        ValidateHost(host);
        EnsureAllowed(host, action);

        if (RequiresVersion(action))
        {
            ValidateVersion(releaseVersion);
        }

        return action switch
        {
            HostAction.StatusBackend => await RunComposeAsync(["ps", Local.BackendService], cancellationToken),
            HostAction.StatusFrontend => await RunComposeAsync(["ps", Local.FrontendService], cancellationToken),
            HostAction.LogsBackend => await RunComposeAsync(["logs", "--tail", "150", Local.BackendService], cancellationToken),
            HostAction.LogsFrontend => await RunComposeAsync(["logs", "--tail", "150", Local.FrontendService], cancellationToken),
            HostAction.StatusInfra => await StatusInfraAsync(cancellationToken),
            HostAction.StageBackendRelease => await LoadImageAsync(HostArtifactKind.Backend, releaseVersion!, cancellationToken),
            HostAction.StageFrontendRelease => await LoadImageAsync(HostArtifactKind.Frontend, releaseVersion!, cancellationToken),
            HostAction.StageGotenbergRelease => await LoadImageAsync(HostArtifactKind.Gotenberg, releaseVersion!, cancellationToken),
            HostAction.StageMinioRelease => await LoadImageAsync(HostArtifactKind.Minio, releaseVersion!, cancellationToken),
            HostAction.DeployBackend => await DeployAsync("backend", Local.BackendService, "THUCLUC_BACKEND_IMAGE", $"thucluc-backend:{releaseVersion}", releaseVersion!, cancellationToken),
            HostAction.DeployFrontend => await DeployAsync("frontend", Local.FrontendService, "THUCLUC_FRONTEND_IMAGE", $"thucluc-frontend:{releaseVersion}", releaseVersion!, cancellationToken),
            HostAction.UpdateGotenberg => await DeployAsync("gotenberg", Local.GotenbergService, "THUCLUC_GOTENBERG_IMAGE", $"thucluc-gotenberg:{releaseVersion}", releaseVersion!, cancellationToken),
            HostAction.RollbackBackend => await RollbackAsync("backend", Local.BackendService, "THUCLUC_BACKEND_IMAGE", cancellationToken),
            HostAction.RollbackFrontend => await RollbackAsync("frontend", Local.FrontendService, "THUCLUC_FRONTEND_IMAGE", cancellationToken),
            HostAction.BackupMinio or HostAction.UpdateMinio => Unsupported("MinIO hiện đang chạy độc lập; chức năng này sẽ được bật sau khi khai báo phương án backup an toàn."),
            _ => Unsupported($"Local mode chưa hỗ trợ thao tác {action}.")
        };
    }

    public Task<HostCommandResult> UploadAsync(
        ManagedHostOptions host,
        HostArtifactKind artifactKind,
        string releaseVersion,
        string localFile,
        CancellationToken cancellationToken = default)
    {
        ValidateHost(host);
        ValidateVersion(releaseVersion);
        var resolvedFile = Path.GetFullPath(localFile);
        if (!File.Exists(resolvedFile))
        {
            throw new FileNotFoundException("Không tìm thấy Docker image trong release.", resolvedFile);
        }

        return Task.FromResult(new HostCommandResult(
            true,
            0,
            "Release đã nằm trên máy hiện tại, không cần upload qua SSH.",
            string.Empty));
    }

    private LocalDeploymentOptions Local => options.CurrentValue.LocalDeployment;

    private async Task<HostCommandResult> LoadImageAsync(
        HostArtifactKind kind,
        string version,
        CancellationToken cancellationToken)
    {
        var component = kind.ToString().ToLowerInvariant();
        var archive = Path.Combine(runtimePaths.ReleaseDirectory, version, $"{component}-{version}.tar");
        if (!File.Exists(archive))
        {
            return new HostCommandResult(false, 3, string.Empty, $"Không tìm thấy image: {archive}");
        }

        return await RunDockerAsync(["load", "--input", archive], cancellationToken, longRunning: true);
    }

    private async Task<HostCommandResult> DeployAsync(
        string component,
        string service,
        string imageVariable,
        string image,
        string version,
        CancellationToken cancellationToken)
    {
        await _deploymentLock.WaitAsync(cancellationToken);
        try
        {
            var state = await ReadStateAsync(cancellationToken);
            var componentState = state.Components.GetValueOrDefault(component) ?? new ComponentDeploymentState();
            var currentImage = componentState.CurrentImage;
            if (string.IsNullOrWhiteSpace(currentImage))
            {
                currentImage = await InspectCurrentImageAsync(service, cancellationToken);
            }

            var environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [imageVariable] = image,
                ["RELEASE_VERSION"] = version
            };
            var result = await RunComposeAsync(["up", "--detach", "--no-deps", "--wait", service], cancellationToken, environment, longRunning: true);
            if (!result.Succeeded)
            {
                return result;
            }

            if (!string.IsNullOrWhiteSpace(currentImage) && !string.Equals(currentImage, image, StringComparison.Ordinal))
            {
                componentState.PreviousImage = currentImage;
            }
            componentState.CurrentImage = image;
            state.Components[component] = componentState;
            await WriteStateAsync(state, cancellationToken);
            await SetEnvironmentValuesAsync(environment, cancellationToken);
            return result with { Output = $"{component} đã chạy image {image}.{Environment.NewLine}{result.Output}".Trim() };
        }
        finally
        {
            _deploymentLock.Release();
        }
    }

    private async Task<HostCommandResult> RollbackAsync(
        string component,
        string service,
        string imageVariable,
        CancellationToken cancellationToken)
    {
        await _deploymentLock.WaitAsync(cancellationToken);
        try
        {
            var state = await ReadStateAsync(cancellationToken);
            if (!state.Components.TryGetValue(component, out var componentState)
                || string.IsNullOrWhiteSpace(componentState.PreviousImage))
            {
                return new HostCommandResult(false, 4, string.Empty, $"Chưa có image {component} trước đó để rollback.");
            }

            var previous = componentState.PreviousImage;
            var version = previous.Contains(':')
                ? previous[(previous.LastIndexOf(':') + 1)..]
                : "rollback";
            var environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [imageVariable] = previous,
                ["RELEASE_VERSION"] = version
            };
            var result = await RunComposeAsync(["up", "--detach", "--no-deps", "--wait", service], cancellationToken, environment, longRunning: true);
            if (!result.Succeeded)
            {
                return result;
            }

            (componentState.CurrentImage, componentState.PreviousImage) = (componentState.PreviousImage, componentState.CurrentImage);
            await WriteStateAsync(state, cancellationToken);
            await SetEnvironmentValuesAsync(environment, cancellationToken);
            return result with { Output = $"Đã rollback {component} về {previous}.{Environment.NewLine}{result.Output}".Trim() };
        }
        finally
        {
            _deploymentLock.Release();
        }
    }

    private async Task<string> InspectCurrentImageAsync(string service, CancellationToken cancellationToken)
    {
        var container = await RunComposeAsync(["ps", "--quiet", service], cancellationToken);
        if (!container.Succeeded || string.IsNullOrWhiteSpace(container.Output))
        {
            return string.Empty;
        }

        var inspect = await RunDockerAsync(["inspect", "--format", "{{.Config.Image}}", container.Output.Trim()], cancellationToken);
        return inspect.Succeeded ? inspect.Output.Trim() : string.Empty;
    }

    private async Task<HostCommandResult> StatusInfraAsync(CancellationToken cancellationToken)
    {
        var status = await RunDockerAsync(
            ["ps", "--filter", "name=minio", "--filter", "name=gotenberg", "--format", "table {{.Names}}\t{{.Image}}\t{{.Status}}\t{{.Ports}}"],
            cancellationToken);
        if (!status.Succeeded)
        {
            return status;
        }

        var lines = new List<string> { status.Output };
        AddDiskStatus(Local.RootDirectory, lines);
        AddDiskStatus(options.CurrentValue.BackupRoot, lines);
        return status with { Output = string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line))) };
    }

    private static void AddDiskStatus(string path, ICollection<string> lines)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        var root = Path.GetPathRoot(Path.GetFullPath(path));
        if (string.IsNullOrWhiteSpace(root)) return;
        var drive = new DriveInfo(root);
        if (!drive.IsReady || drive.TotalSize <= 0) return;
        var usedPercent = (int)Math.Round(100d * (drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize);
        lines.Add($"DISK|{path}|{usedPercent}|{drive.AvailableFreeSpace / 1024}");
    }

    private Task<HostCommandResult> RunComposeAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? environment = null,
        bool longRunning = false)
    {
        ValidateDeploymentFiles();
        var fullArguments = new List<string>
        {
            "compose",
            "--project-directory", Local.RootDirectory,
            "--env-file", ResolveLocalPath(Local.EnvironmentFile),
            "--file", ResolveLocalPath(Local.ComposeFile)
        };
        fullArguments.AddRange(arguments);
        return RunDockerAsync(fullArguments, cancellationToken, longRunning, environment);
    }

    private async Task<HostCommandResult> RunDockerAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        bool longRunning = false,
        IReadOnlyDictionary<string, string>? environment = null)
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
        if (environment is not null)
        {
            foreach (var pair in environment) startInfo.Environment[pair.Key] = pair.Value;
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Không thể khởi động Docker CLI.");
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            var result = new HostCommandResult(
                process.ExitCode == 0,
                process.ExitCode,
                Truncate(await outputTask),
                Truncate(await errorTask));
            logger.LogInformation("Local Docker action {Arguments} kết thúc với mã {ExitCode}", string.Join(' ', arguments), process.ExitCode);
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException("Docker không hoàn tất thao tác trong thời gian cho phép.");
        }
    }

    private async Task<LocalDeploymentState> ReadStateAsync(CancellationToken cancellationToken)
    {
        var path = StateFile;
        if (!File.Exists(path)) return new LocalDeploymentState();
        try
        {
            return JsonSerializer.Deserialize<LocalDeploymentState>(await File.ReadAllTextAsync(path, cancellationToken))
                ?? new LocalDeploymentState();
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Trạng thái deploy local không hợp lệ.", exception);
        }
    }

    private async Task WriteStateAsync(LocalDeploymentState state, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(runtimePaths.StateDirectory);
        var temporary = StateFile + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(state), cancellationToken);
        File.Move(temporary, StateFile, overwrite: true);
    }

    private async Task SetEnvironmentValuesAsync(
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        var path = ResolveLocalPath(Local.EnvironmentFile);
        var lines = (await File.ReadAllLinesAsync(path, cancellationToken)).ToList();
        foreach (var pair in values)
        {
            if (!SafeEnvironmentValueRegex().IsMatch(pair.Value))
            {
                throw new InvalidOperationException($"Giá trị {pair.Key} không hợp lệ.");
            }

            var prefix = pair.Key + "=";
            var index = lines.FindIndex(line => line.StartsWith(prefix, StringComparison.Ordinal));
            if (index >= 0) lines[index] = prefix + pair.Value;
            else lines.Add(prefix + pair.Value);
        }

        var temporary = path + ".ops.tmp";
        await File.WriteAllLinesAsync(temporary, lines, cancellationToken);
        File.Move(temporary, path, overwrite: true);
    }

    private string StateFile => Path.Combine(runtimePaths.StateDirectory, "local-deployment.json");

    private string ResolveLocalPath(string path) => Path.IsPathRooted(path)
        ? Path.GetFullPath(path)
        : Path.GetFullPath(Path.Combine(Local.RootDirectory, path));

    private void ValidateDeploymentFiles()
    {
        var root = Path.GetFullPath(Local.RootDirectory);
        if (!Directory.Exists(root))
        {
            throw new InvalidOperationException($"Không tìm thấy thư mục ứng dụng: {root}");
        }
        if (!File.Exists(ResolveLocalPath(Local.ComposeFile)))
        {
            throw new InvalidOperationException("Không tìm thấy compose.yml của ứng dụng.");
        }
        if (!File.Exists(ResolveLocalPath(Local.EnvironmentFile)))
        {
            throw new InvalidOperationException("Không tìm thấy file .env của ứng dụng.");
        }
    }

    private static void ValidateHost(ManagedHostOptions host)
    {
        if (!host.Enabled) throw new InvalidOperationException($"Máy chủ {host.DisplayName} đang bị tắt.");
    }

    private static void EnsureAllowed(ManagedHostOptions host, HostAction action)
    {
        if (!host.AllowedActions.Contains(action.ToString(), StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{host.DisplayName} không cho phép thao tác {action}.");
        }
    }

    private static bool RequiresVersion(HostAction action) => action is
        HostAction.StageBackendRelease or HostAction.StageFrontendRelease or
        HostAction.StageGotenbergRelease or HostAction.StageMinioRelease or
        HostAction.DeployBackend or HostAction.DeployFrontend or
        HostAction.UpdateGotenberg or HostAction.UpdateMinio;

    private static void ValidateVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version) || !SafeVersionRegex().IsMatch(version))
        {
            throw new InvalidOperationException("Phiên bản release không hợp lệ.");
        }
    }

    private static HostCommandResult Unsupported(string message) => new(false, 5, string.Empty, message);

    private static string CombineOutput(HostCommandResult result) =>
        string.Join(Environment.NewLine, new[] { result.Error, result.Output }.Where(value => !string.IsNullOrWhiteSpace(value)));

    private static string Truncate(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= MaxCapturedCharacters ? trimmed : trimmed[..MaxCapturedCharacters] + Environment.NewLine + "[đã rút gọn]";
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")]
    private static partial Regex SafeVersionRegex();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:/-]{0,255}$")]
    private static partial Regex SafeEnvironmentValueRegex();

    private sealed class LocalDeploymentState
    {
        public Dictionary<string, ComponentDeploymentState> Components { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ComponentDeploymentState
    {
        public string CurrentImage { get; set; } = string.Empty;
        public string PreviousImage { get; set; } = string.Empty;
    }
}
