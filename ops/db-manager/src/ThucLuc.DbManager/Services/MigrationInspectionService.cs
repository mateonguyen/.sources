using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;
using ThucLuc.DbManager.Configuration;
using ThucLuc.DbManager.Models;

namespace ThucLuc.DbManager.Services;

public interface IMigrationInspectionService
{
    Task<MigrationInspection> InspectAsync(
        ManagedEnvironmentOptions environment,
        CancellationToken cancellationToken = default);
}

public sealed partial class MigrationInspectionService(
    IOptionsMonitor<OpsOptions> options,
    IWebHostEnvironment hostEnvironment,
    IOracleCredentialResolver credentials) : IMigrationInspectionService
{
    public async Task<MigrationInspection> InspectAsync(
        ManagedEnvironmentOptions environment,
        CancellationToken cancellationToken = default)
    {
        if (!environment.Enabled || !environment.Safety.AllowMigrate)
        {
            throw new InvalidOperationException("Môi trường này chưa được phép kiểm tra/chạy migration.");
        }

        var migrationDirectory = ResolveMigrationDirectory(options.CurrentValue.MigrationSqlPath);
        if (!Directory.Exists(migrationDirectory))
        {
            throw new InvalidOperationException($"Không tìm thấy thư mục Flyway SQL: {migrationDirectory}");
        }

        var resolution = await credentials.ResolveAsync(environment, includeCredential: true, cancellationToken);
        if (resolution.Credential is null)
        {
            throw new InvalidOperationException("Chưa lưu tài khoản schema để đọc lịch sử Flyway.");
        }

        var applied = await ReadAppliedAsync(environment, resolution.Credential, cancellationToken);
        var files = Directory.EnumerateFiles(migrationDirectory, "V*__*.sql", SearchOption.TopDirectoryOnly)
            .Select(path => ParseVersionedFile(path, applied))
            .OrderBy(file => file.Version)
            .ToList();
        var repeatable = Directory.EnumerateFiles(migrationDirectory, "R__*.sql", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Cast<string>()
            .OrderBy(name => name)
            .ToList();

        var current = files.Where(file => file.Applied && file.Succeeded).Select(file => (int?)file.Version).Max();
        var pending = files.Count(file => !file.Applied);
        var message = current is null
            ? $"Chưa có version migration thành công; có {pending} file đang chờ."
            : $"Database đang ở V{current}; có {pending} file đang chờ.";
        return new MigrationInspection(
            environment.Key,
            environment.Oracle.Schema,
            DateTimeOffset.Now,
            files,
            repeatable,
            message);
    }

    private static async Task<Dictionary<int, bool>> ReadAppliedAsync(
        ManagedEnvironmentOptions environment,
        OracleCredential credential,
        CancellationToken cancellationToken)
    {
        var connectionString = new OracleConnectionStringBuilder
        {
            UserID = credential.UserName,
            Password = credential.Password,
            DataSource = $"{environment.Oracle.Host}:{environment.Oracle.Port}/{environment.Oracle.ServiceName}",
            Pooling = false,
            ConnectionTimeout = 8
        }.ConnectionString;

        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT "version", "success"
            FROM "flyway_schema_history"
            WHERE "type" = 'SQL'
              AND "version" IS NOT NULL
            """;

        var applied = new Dictionary<int, bool>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var versionText = Convert.ToString(reader.GetValue(0));
                if (int.TryParse(versionText, out var version))
                {
                    applied[version] = Convert.ToBoolean(reader.GetValue(1));
                }
            }
        }
        catch (OracleException exception) when (exception.Number == 942)
        {
            // Schema chưa từng chạy Flyway.
        }

        return applied;
    }

    private static MigrationFile ParseVersionedFile(string path, IReadOnlyDictionary<int, bool> applied)
    {
        var fileName = Path.GetFileName(path);
        var match = VersionedFileRegex().Match(fileName);
        if (!match.Success || !int.TryParse(match.Groups["version"].Value, out var version))
        {
            throw new InvalidOperationException($"Tên migration không hợp lệ: {fileName}");
        }

        var description = match.Groups["description"].Value.Replace('_', ' ');
        var isApplied = applied.TryGetValue(version, out var succeeded);
        return new MigrationFile(version, description, fileName, isApplied, isApplied && succeeded);
    }

    private string ResolveMigrationDirectory(string configuredPath) => Path.GetFullPath(
        Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(hostEnvironment.ContentRootPath, configuredPath));

    [GeneratedRegex("^V(?<version>[0-9]+)__(?<description>.+)\\.sql$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionedFileRegex();
}
