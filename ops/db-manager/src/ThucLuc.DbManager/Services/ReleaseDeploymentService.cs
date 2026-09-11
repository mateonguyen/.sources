using Microsoft.Extensions.Options;
using ThucLuc.DbManager.Configuration;
using ThucLuc.DbManager.Models;

namespace ThucLuc.DbManager.Services;

public interface IReleaseDeploymentService
{
    OperationEntry Enqueue(ReleaseDeploymentSelection selection);

    OperationEntry EnqueueRollback(ReleaseRollbackSelection selection);
}

public sealed class ReleaseDeploymentService(
    IReleasePackageService packages,
    IReleaseMigrationService migrations,
    IHostExecutor hostExecutor,
    IOpsJobQueue jobs,
    IOptionsMonitor<OpsOptions> options) : IReleaseDeploymentService
{
    public OperationEntry Enqueue(ReleaseDeploymentSelection selection)
    {
        if (!selection.DeployBackend
            && !selection.DeployFrontend
            && !selection.UpdateGotenberg
            && !selection.UpdateMinio)
        {
            throw new InvalidOperationException("Hãy chọn ít nhất một thành phần cần triển khai.");
        }

        var bundle = packages.GetRequired(selection.Version);
        if (selection.DeployBackend && !bundle.HasBackend)
        {
            throw new InvalidOperationException("Release này không chứa image Backend.");
        }
        if (selection.DeployBackend && bundle.Manifest.Migrations.Count > 0 && !bundle.HasFlyway)
        {
            throw new InvalidOperationException("Release backend này thiếu Flyway image; không thể cập nhật database an toàn.");
        }
        if (selection.DeployFrontend && !bundle.HasFrontend)
        {
            throw new InvalidOperationException("Release này không chứa image Frontend.");
        }
        if (selection.UpdateGotenberg && !bundle.HasGotenberg)
        {
            throw new InvalidOperationException("Release này không chứa image Gotenberg.");
        }
        if (selection.UpdateMinio && !bundle.HasMinio)
        {
            throw new InvalidOperationException("Release này không chứa image MinIO.");
        }

        var hosts = options.CurrentValue.Hosts;
        var selectedHosts = new List<ManagedHostOptions>();
        if (selection.DeployBackend)
        {
            selectedHosts.Add(GetHost(hosts, "Backend"));
        }
        if (selection.DeployFrontend)
        {
            selectedHosts.Add(GetHost(hosts, "Frontend"));
        }
        if (selection.UpdateGotenberg || selection.UpdateMinio)
        {
            selectedHosts.Add(GetHost(hosts, "Infra"));
        }

        if (!options.CurrentValue.AllowProductionOperations
            && selectedHosts.Any(host => string.Equals(host.Tier, "Production", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Triển khai Production đang bị khóa trong cấu hình Ops Console.");
        }

        return jobs.Enqueue(
            "Triển khai release",
            BuildEnvironmentKey(selectedHosts),
            $"Đang chờ triển khai release {selection.Version}.",
            cancellationToken => DeployAsync(bundle, selection, hosts, cancellationToken));
    }

    public OperationEntry EnqueueRollback(ReleaseRollbackSelection selection)
    {
        if (!selection.RollbackBackend && !selection.RollbackFrontend)
        {
            throw new InvalidOperationException("Hãy chọn backend hoặc frontend cần rollback.");
        }

        var hosts = options.CurrentValue.Hosts;
        var selectedHosts = new List<ManagedHostOptions>();
        if (selection.RollbackBackend)
        {
            selectedHosts.Add(GetHost(hosts, "Backend"));
        }
        if (selection.RollbackFrontend)
        {
            selectedHosts.Add(GetHost(hosts, "Frontend"));
        }

        EnsureProductionAllowed(selectedHosts);
        return jobs.Enqueue(
            "Rollback release",
            BuildEnvironmentKey(selectedHosts),
            "Đang chờ rollback về image liền trước trên máy chủ.",
            cancellationToken => RollbackAsync(selection, hosts, cancellationToken));
    }

    private async Task<string> DeployAsync(
        ReleaseBundle bundle,
        ReleaseDeploymentSelection selection,
        IReadOnlyList<ManagedHostOptions> hosts,
        CancellationToken cancellationToken)
    {
        if (selection.DeployBackend)
        {
            var host = GetHost(hosts, "Backend");
            await EnsureConnectionAsync(host, cancellationToken);
            if (bundle.Manifest.Migrations.Count > 0)
            {
                await migrations.BackupAndMigrateAsync(bundle, host.EnvironmentKey, cancellationToken);
            }
            await EnsureSucceededAsync(
                await hostExecutor.UploadAsync(host, HostArtifactKind.Backend, bundle.Manifest.Version, bundle.BackendArchive, cancellationToken),
                "Upload backend image");
            await EnsureSucceededAsync(
                await hostExecutor.ExecuteAsync(host, HostAction.StageBackendRelease, bundle.Manifest.Version, cancellationToken),
                "Tiếp nhận backend image");
            await EnsureSucceededAsync(
                await hostExecutor.ExecuteAsync(host, HostAction.DeployBackend, bundle.Manifest.Version, cancellationToken),
                "Deploy backend");
        }

        if (selection.DeployFrontend)
        {
            var host = GetHost(hosts, "Frontend");
            await EnsureConnectionAsync(host, cancellationToken);
            await EnsureSucceededAsync(
                await hostExecutor.UploadAsync(host, HostArtifactKind.Frontend, bundle.Manifest.Version, bundle.FrontendArchive, cancellationToken),
                "Upload frontend image");
            await EnsureSucceededAsync(
                await hostExecutor.ExecuteAsync(host, HostAction.StageFrontendRelease, bundle.Manifest.Version, cancellationToken),
                "Tiếp nhận frontend image");
            await EnsureSucceededAsync(
                await hostExecutor.ExecuteAsync(host, HostAction.DeployFrontend, bundle.Manifest.Version, cancellationToken),
                "Deploy frontend");
        }

        if (selection.UpdateGotenberg)
        {
            var host = GetHost(hosts, "Infra");
            await EnsureConnectionAsync(host, cancellationToken);
            await EnsureSucceededAsync(
                await hostExecutor.UploadAsync(host, HostArtifactKind.Gotenberg, bundle.Manifest.Version, bundle.GotenbergArchive, cancellationToken),
                "Upload Gotenberg image");
            await EnsureSucceededAsync(
                await hostExecutor.ExecuteAsync(host, HostAction.StageGotenbergRelease, bundle.Manifest.Version, cancellationToken),
                "Tiếp nhận Gotenberg image");
            await EnsureSucceededAsync(
                await hostExecutor.ExecuteAsync(host, HostAction.UpdateGotenberg, bundle.Manifest.Version, cancellationToken),
                "Update Gotenberg");
        }

        if (selection.UpdateMinio)
        {
            var host = GetHost(hosts, "Infra");
            await EnsureConnectionAsync(host, cancellationToken);
            await EnsureSucceededAsync(
                await hostExecutor.ExecuteAsync(host, HostAction.BackupMinio, cancellationToken: cancellationToken),
                "Backup MinIO trước update");
            await EnsureSucceededAsync(
                await hostExecutor.UploadAsync(host, HostArtifactKind.Minio, bundle.Manifest.Version, bundle.MinioArchive, cancellationToken),
                "Upload MinIO image");
            await EnsureSucceededAsync(
                await hostExecutor.ExecuteAsync(host, HostAction.StageMinioRelease, bundle.Manifest.Version, cancellationToken),
                "Tiếp nhận MinIO image");
            await EnsureSucceededAsync(
                await hostExecutor.ExecuteAsync(host, HostAction.UpdateMinio, bundle.Manifest.Version, cancellationToken),
                "Update MinIO");
        }

        var components = string.Join(" và ", new[]
        {
            selection.DeployBackend && bundle.Manifest.Migrations.Count > 0 ? "database (Flyway)" : null,
            selection.DeployBackend ? "backend" : null,
            selection.DeployFrontend ? "frontend" : null,
            selection.UpdateGotenberg ? "Gotenberg" : null,
            selection.UpdateMinio ? "MinIO" : null
        }.Where(value => value is not null));
        return $"Đã triển khai {components} phiên bản {bundle.Manifest.Version}.";
    }

    private async Task<string> RollbackAsync(
        ReleaseRollbackSelection selection,
        IReadOnlyList<ManagedHostOptions> hosts,
        CancellationToken cancellationToken)
    {
        // Frontend được đưa về trước để giảm khả năng giao diện mới gọi backend cũ.
        if (selection.RollbackFrontend)
        {
            var host = GetHost(hosts, "Frontend");
            await EnsureConnectionAsync(host, cancellationToken);
            await EnsureSucceededAsync(
                await hostExecutor.ExecuteAsync(host, HostAction.RollbackFrontend, cancellationToken: cancellationToken),
                "Rollback frontend");
        }

        if (selection.RollbackBackend)
        {
            var host = GetHost(hosts, "Backend");
            await EnsureConnectionAsync(host, cancellationToken);
            await EnsureSucceededAsync(
                await hostExecutor.ExecuteAsync(host, HostAction.RollbackBackend, cancellationToken: cancellationToken),
                "Rollback backend");
        }

        var components = string.Join(" và ", new[]
        {
            selection.RollbackBackend ? "backend" : null,
            selection.RollbackFrontend ? "frontend" : null
        }.Where(value => value is not null));
        return $"Đã rollback {components} về image liền trước trên máy chủ.";
    }

    private void EnsureProductionAllowed(IEnumerable<ManagedHostOptions> hosts)
    {
        if (!options.CurrentValue.AllowProductionOperations
            && hosts.Any(host => string.Equals(host.Tier, "Production", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Thao tác Production đang bị khóa trong cấu hình Ops Console.");
        }
    }

    private async Task EnsureConnectionAsync(ManagedHostOptions host, CancellationToken cancellationToken)
    {
        var connection = await hostExecutor.CheckAsync(host, cancellationToken);
        if (!connection.Succeeded)
        {
            throw new InvalidOperationException($"{host.DisplayName}: {connection.Detail ?? connection.Message}");
        }
    }

    private static Task EnsureSucceededAsync(HostCommandResult result, string step)
    {
        if (!result.Succeeded)
        {
            var detail = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
            throw new InvalidOperationException($"{step} thất bại: {detail}");
        }
        return Task.CompletedTask;
    }

    private static ManagedHostOptions GetHost(IEnumerable<ManagedHostOptions> hosts, string role) =>
        hosts.SingleOrDefault(host => host.Enabled && string.Equals(host.Role, role, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Chưa cấu hình đúng một máy chủ {role} đang bật.");

    private static string BuildEnvironmentKey(IEnumerable<ManagedHostOptions> hosts)
    {
        var environmentKeys = hosts
            .Select(host => host.EnvironmentKey)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (environmentKeys.Count == 1)
        {
            return environmentKeys[0];
        }

        var tiers = hosts.Select(host => host.Tier)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return tiers.Count == 1 ? tiers[0] : "deployment";
    }
}
