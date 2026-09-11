using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;
using ThucLuc.DbManager.Configuration;
using ThucLuc.DbManager.Models;

namespace ThucLuc.DbManager.Services;

public interface IPreflightService
{
    Task<PreflightReport> CheckAsync(ManagedEnvironmentOptions environment, CancellationToken cancellationToken = default);
}

public sealed partial class PreflightService(
    IOptionsMonitor<OpsOptions> options,
    IWebHostEnvironment hostEnvironment,
    IToolLocator toolLocator,
    IOracleCredentialResolver credentialResolver,
    ILogger<PreflightService> logger) : IPreflightService
{
    public async Task<PreflightReport> CheckAsync(
        ManagedEnvironmentOptions environment,
        CancellationToken cancellationToken = default)
    {
        var checks = new List<PreflightCheck>();

        CheckConfiguration(environment, checks);
        var endpointAvailable = await CheckOracleEndpointAsync(environment, checks, cancellationToken);
        var credential = await CheckCredentialAsync(environment, checks, cancellationToken);
        if (endpointAvailable && credential.IsConfigured)
        {
            await CheckOracleLoginAsync(environment, credential, checks, cancellationToken);
        }
        CheckDataPumpTools(checks);
        CheckFlywayRuntime(checks);
        CheckMigrationDirectory(checks);
        CheckSafetyPolicy(environment, checks);

        logger.LogInformation(
            "Preflight {EnvironmentKey}: {SuccessCount}/{TotalCount} checks succeeded",
            environment.Key,
            checks.Count(check => check.Status == CheckStatus.Success),
            checks.Count);

        return new PreflightReport(environment.Key, DateTimeOffset.Now, checks);
    }

    private static void CheckConfiguration(
        ManagedEnvironmentOptions environment,
        ICollection<PreflightCheck> checks)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(environment.Oracle.Host)) missing.Add("Host");
        if (environment.Oracle.Port is <= 0 or > 65535) missing.Add("Port");
        if (string.IsNullOrWhiteSpace(environment.Oracle.ServiceName)) missing.Add("ServiceName");

        checks.Add(missing.Count == 0
            ? new PreflightCheck("Thông tin kết nối", CheckStatus.Success, "Đã nhập đủ địa chỉ máy chủ và Service/PDB.")
            : new PreflightCheck(
                "Thông tin kết nối",
                CheckStatus.Error,
                "Còn thiếu thông tin bắt buộc.",
                string.Join(", ", missing)));
    }

    private static async Task<bool> CheckOracleEndpointAsync(
        ManagedEnvironmentOptions environment,
        ICollection<PreflightCheck> checks,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(environment.Oracle.Host) || environment.Oracle.Port is <= 0 or > 65535)
        {
            checks.Add(new PreflightCheck(
                "Máy chủ DB",
                CheckStatus.Error,
                "Không thể kiểm tra vì địa chỉ chưa hợp lệ."));
            return false;
        }

        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(4));
            await client.ConnectAsync(environment.Oracle.Host, environment.Oracle.Port, timeout.Token);

            checks.Add(new PreflightCheck(
                "Máy chủ DB",
                CheckStatus.Success,
                $"Đã liên lạc được {environment.Oracle.Host}:{environment.Oracle.Port}."));
            return true;
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            checks.Add(new PreflightCheck(
                "Máy chủ DB",
                CheckStatus.Error,
                $"Không kết nối được {environment.Oracle.Host}:{environment.Oracle.Port}.",
                exception.Message));
            return false;
        }
    }

    private async Task<CredentialResolution> CheckCredentialAsync(
        ManagedEnvironmentOptions environment,
        ICollection<PreflightCheck> checks,
        CancellationToken cancellationToken)
    {
        var resolution = await credentialResolver.ResolveAsync(
            environment,
            includeCredential: true,
            cancellationToken);
        checks.Add(resolution.IsConfigured
            ? new PreflightCheck("Tài khoản DB", CheckStatus.Success, $"Đã lưu qua {resolution.Source.ToLowerInvariant()}.")
            : new PreflightCheck(
                "Tài khoản DB",
                CheckStatus.Warning,
                "Chưa nhập tên đăng nhập và mật khẩu.",
                "Chỉ cần bổ sung khi sao lưu hoặc khi phục hồi vào schema đã có."));
        return resolution;
    }

    private static async Task CheckOracleLoginAsync(
        ManagedEnvironmentOptions environment,
        CredentialResolution resolution,
        ICollection<PreflightCheck> checks,
        CancellationToken cancellationToken)
    {
        if (resolution.Credential is null)
        {
            return;
        }

        var connectionString = new OracleConnectionStringBuilder
        {
            UserID = resolution.Credential.UserName,
            Password = resolution.Credential.Password,
            DataSource = $"{environment.Oracle.Host}:{environment.Oracle.Port}/{environment.Oracle.ServiceName}",
            Pooling = false,
            ConnectionTimeout = 5
        }.ConnectionString;

        try
        {
            await using var connection = new OracleConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            checks.Add(new PreflightCheck(
                "Đăng nhập DB",
                CheckStatus.Success,
                $"Đăng nhập Oracle thành công với tài khoản {resolution.Credential.UserName}."));
        }
        catch (OracleException exception)
        {
            checks.Add(new PreflightCheck(
                "Đăng nhập DB",
                CheckStatus.Error,
                "Không đăng nhập được Oracle. Kiểm tra tài khoản, mật khẩu và Service/PDB.",
                $"Oracle error {exception.Number}: {exception.Message}"));
        }
        catch (Exception exception) when (exception is InvalidOperationException or OperationCanceledException)
        {
            checks.Add(new PreflightCheck(
                "Đăng nhập DB",
                CheckStatus.Error,
                "Không hoàn tất kiểm tra đăng nhập Oracle.",
                exception.Message));
        }
    }

    private void CheckDataPumpTools(ICollection<PreflightCheck> checks)
    {
        var expdp = toolLocator.Find("expdp");
        var impdp = toolLocator.Find("impdp");

        checks.Add(expdp is not null && impdp is not null
            ? new PreflightCheck("Công cụ sao lưu", CheckStatus.Success, "Đã tìm thấy công cụ Oracle expdp và impdp.")
            : new PreflightCheck(
                "Công cụ sao lưu",
                CheckStatus.Warning,
                "Máy này chưa có công cụ Oracle expdp/impdp.",
                "Giao diện vẫn dùng được; cần cài Oracle Client trước khi tạo bản sao lưu."));
    }

    private void CheckFlywayRuntime(ICollection<PreflightCheck> checks)
    {
        var docker = toolLocator.Find("docker");
        checks.Add(docker is not null
            ? new PreflightCheck("Công cụ cập nhật cấu trúc", CheckStatus.Success, "Đã tìm thấy Docker để chạy Flyway.")
            : new PreflightCheck(
                "Công cụ cập nhật cấu trúc",
                CheckStatus.Warning,
                "Máy này chưa có Docker để chạy Flyway.",
                "Chỉ ảnh hưởng chức năng cập nhật cấu trúc DB."));
    }

    private void CheckMigrationDirectory(ICollection<PreflightCheck> checks)
    {
        var migrationPath = ResolvePath(options.CurrentValue.MigrationSqlPath);
        if (!Directory.Exists(migrationPath))
        {
            checks.Add(new PreflightCheck(
                "Bộ cập nhật cấu trúc",
                CheckStatus.Warning,
                "Không tìm thấy thư mục cập nhật cấu trúc.",
                migrationPath));
            return;
        }

        var versions = Directory
            .EnumerateFiles(migrationPath, "V*__*.sql", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Select(fileName => MigrationVersionRegex().Match(fileName ?? string.Empty))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups["version"].Value))
            .ToList();

        var latestVersion = versions.Count == 0 ? "chưa có" : $"V{versions.Max()}";
        checks.Add(new PreflightCheck(
            "Bộ cập nhật cấu trúc",
            CheckStatus.Success,
            $"Đã tìm thấy {versions.Count} migration; phiên bản local mới nhất: {latestVersion}.",
            migrationPath));
    }

    private void CheckSafetyPolicy(
        ManagedEnvironmentOptions environment,
        ICollection<PreflightCheck> checks)
    {
        var isProduction = string.Equals(environment.Tier, "Production", StringComparison.OrdinalIgnoreCase);
        if (isProduction && !options.CurrentValue.AllowProductionOperations)
        {
            checks.Add(new PreflightCheck(
                "Phạm vi được phép",
                CheckStatus.Warning,
                "Mọi thao tác thay đổi PROD đang bị khóa theo cấu hình."));
            return;
        }

        checks.Add(new PreflightCheck(
            "Phạm vi được phép",
            CheckStatus.Success,
            $"Backup: {FormatPermission(environment.Safety.AllowBackup)} · Restore: {FormatPermission(environment.Safety.AllowRestore)} · Migrate: {FormatPermission(environment.Safety.AllowMigrate)}"));
    }

    private string ResolvePath(string configuredPath) => Path.GetFullPath(
        Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(hostEnvironment.ContentRootPath, configuredPath));

    private static string FormatPermission(bool allowed) => allowed ? "cho phép" : "khóa";

    [GeneratedRegex(@"^V(?<version>\d+)__", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MigrationVersionRegex();
}
