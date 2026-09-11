using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;
using ThucLuc.DbManager.Configuration;
using ThucLuc.DbManager.Models;

namespace ThucLuc.DbManager.Services;

public interface IOraclePdbService
{
    Task<OraclePdbInspection> InspectAsync(ManagedEnvironmentOptions environment, string sysPassword,
        CancellationToken cancellationToken = default);

    Task<OraclePdbSelection> SelectAsync(ManagedEnvironmentOptions environment, string sysPassword,
        string pdbName, string serviceName, CancellationToken cancellationToken = default);

    Task<OraclePdbSelection> CreateAsync(ManagedEnvironmentOptions environment, string sysPassword,
        string pdbName, string? createFileDestination, int maxDataSizeGb,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Explicit PDB provisioning, separate from schema restore. Never removes a PDB or changes an existing one's open mode.
/// Every SYS connection is unpooled and credentials/DDL are never logged or retained.
/// </summary>
public sealed partial class OraclePdbService(
    IOptionsMonitor<OpsOptions> options,
    IOperationJournal journal,
    ILogger<OraclePdbService> logger) : IOraclePdbService
{
    // Serialize provisioning within this application; Oracle enforces uniqueness across other processes.
    private readonly SemaphoreSlim _creationGate = new(1, 1);

    public async Task<OraclePdbInspection> InspectAsync(ManagedEnvironmentOptions environment,
        string sysPassword, CancellationToken cancellationToken = default)
    {
        ValidateRequest(environment, sysPassword);
        try
        {
            await using var connection = NewConnection(environment, sysPassword);
            await OpenWithRetryAsync(connection, cancellationToken);
            var context = await MoveToRootAsync(connection, cancellationToken);
            if (!context.IsCdb)
                return new(false, context.ContainerName, null, []);

            var destination = await ScalarStringAsync(connection,
                "SELECT VALUE FROM V$PARAMETER WHERE NAME = 'db_create_file_dest'", cancellationToken);
            var fileNameConvert = await ScalarStringAsync(connection,
                "SELECT VALUE FROM V$PARAMETER WHERE NAME = 'pdb_file_name_convert'", cancellationToken);
            var storageSummary = !string.IsNullOrWhiteSpace(destination)
                ? $"Oracle Managed Files: {destination}"
                : !string.IsNullOrWhiteSpace(fileNameConvert)
                    ? "Oracle đã có quy tắc PDB_FILE_NAME_CONVERT"
                    : null;
            return new(true, context.ContainerName, storageSummary,
                await ReadChoicesAsync(connection, cancellationToken));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException(SafeMessage(exception, environment));
        }
    }

    public async Task<OraclePdbSelection> SelectAsync(ManagedEnvironmentOptions environment,
        string sysPassword, string pdbName, string serviceName, CancellationToken cancellationToken = default)
    {
        ValidateRequest(environment, sysPassword);
        ValidateExistingPdbName(pdbName);
        ValidateServiceName(serviceName);
        try
        {
            await using var connection = NewConnection(environment, sysPassword);
            await OpenWithRetryAsync(connection, cancellationToken);
            var context = await MoveToRootAsync(connection, cancellationToken);
            if (!context.IsCdb) throw Failure("DB này không dùng PDB; giữ nguyên Service và tạo schema trực tiếp.");
            var choices = await ReadChoicesAsync(connection, cancellationToken);
            var choice = choices.FirstOrDefault(item =>
                string.Equals(item.Name, pdbName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase));
            if (choice is null)
                throw Failure("PDB hoặc service đã thay đổi. Hãy tải lại danh sách nơi lưu dữ liệu.");
            var identity = await ReadIdentityAsync(connection, choice.Name, cancellationToken);
            EnsureReady(identity);
            await VerifyServiceAsync(environment, sysPassword, choice.ServiceName, identity,
                retryRegistration: false, cancellationToken);
            return new(choice.Name, choice.ServiceName,
                $"Đã kiểm tra PDB {choice.Name}, service {choice.ServiceName}. Chưa tạo schema hoặc phục hồi dữ liệu.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException(SafeMessage(exception, environment));
        }
    }

    public async Task<OraclePdbSelection> CreateAsync(ManagedEnvironmentOptions environment,
        string sysPassword, string pdbName, string? createFileDestination, int maxDataSizeGb,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(environment, sysPassword);
        var name = NormalizePdbName(pdbName);
        var destination = NormalizeFileDestination(createFileDestination);
        var usersSql = BuildUsersTablespaceSql(maxDataSizeGb);
        await _creationGate.WaitAsync(cancellationToken);
        var operation = journal.AddRunning("Tạo nơi lưu dữ liệu", environment.Key, $"Đang chuẩn bị tạo PDB {name}.");
        var creationStarted = false;
        var created = false;
        var adminLocked = false;
        var stage = "kết nối lại với máy chủ";
        try
        {
            await using var connection = NewConnection(environment, sysPassword);
            await OpenWithRetryAsync(connection, cancellationToken);
            stage = "kiểm tra điều kiện";
            var context = await MoveToRootAsync(connection, cancellationToken);
            if (!context.IsCdb) throw Failure("DB này không dùng PDB; giữ nguyên Service và tạo schema trực tiếp.");
            var openMode = await ScalarStringAsync(connection, "SELECT OPEN_MODE FROM V$DATABASE", cancellationToken);
            if (openMode != "READ WRITE") throw Failure("CDB phải đang mở READ WRITE để tạo PDB mới.");
            await EnsureNameAvailableAsync(connection, name, cancellationToken);
            var inheritedDestination = await ScalarStringAsync(connection,
                "SELECT VALUE FROM V$PARAMETER WHERE NAME = 'db_create_file_dest'", cancellationToken);
            var inheritedFileNameConvert = await ScalarStringAsync(connection,
                "SELECT VALUE FROM V$PARAMETER WHERE NAME = 'pdb_file_name_convert'", cancellationToken);
            if (destination is null
                && string.IsNullOrWhiteSpace(inheritedDestination)
                && string.IsNullOrWhiteSpace(inheritedFileNameConvert))
                throw Failure("Oracle chưa có thư mục datafile mặc định. Hãy nhập một thư mục dữ liệu đã tồn tại trên máy chủ, có quyền ghi cho Oracle. Miniapp không tự tạo thư mục hệ điều hành.");

            // A dedicated, unprivileged bootstrap administrator is required by CREATE PDB syntax.
            // Its random password is not retained; SYS administers the PDB and the local account is locked below.
            var adminPassword = "Dbm_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "a9";
            stage = "tạo PDB từ mẫu Oracle";
            var createSql = BuildCreatePdbSql(name, adminPassword, destination);
            creationStarted = true;
            await ExecuteAsync(connection, createSql, cancellationToken, commandTimeout: 600);
            created = true;
            stage = "mở PDB mới";
            await ExecuteAsync(connection, $"ALTER PLUGGABLE DATABASE \"{name}\" OPEN READ WRITE", cancellationToken, 600);
            var identity = await ReadIdentityAsync(connection, name, cancellationToken);
            EnsureReady(identity);

            stage = "khóa tài khoản quản trị khởi tạo";
            await ExecuteAsync(connection, $"ALTER SESSION SET CONTAINER = \"{name}\"", cancellationToken);
            await ExecuteAsync(connection, "ALTER USER \"PDBADMIN\" ACCOUNT LOCK CONTAINER = CURRENT", cancellationToken);
            adminLocked = true;
            stage = "chuẩn bị tablespace USERS";
            var usersCount = await ScalarStringAsync(connection,
                "SELECT COUNT(*) FROM DBA_TABLESPACES WHERE TABLESPACE_NAME = 'USERS'", cancellationToken);
            var usersCreated = usersCount == "0";
            if (usersCreated) await ExecuteAsync(connection, usersSql, cancellationToken, 120);
            await ExecuteAsync(connection, "ALTER DATABASE DEFAULT TABLESPACE \"USERS\"", cancellationToken);

            stage = "lưu trạng thái mở PDB";
            await ExecuteAsync(connection, "ALTER SESSION SET CONTAINER = CDB$ROOT", cancellationToken);
            await ExecuteAsync(connection, $"ALTER PLUGGABLE DATABASE \"{name}\" SAVE STATE", cancellationToken);
            stage = "đăng ký và kiểm tra service mới";
            await ExecuteAsync(connection, "ALTER SYSTEM REGISTER", cancellationToken);
            var choices = await ReadChoicesAsync(connection, cancellationToken);
            var service = choices.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))?.ServiceName;
            if (string.IsNullOrWhiteSpace(service))
                throw Failure("PDB đã có nhưng chưa tìm được service. Chờ một lát rồi tải lại danh sách; không bấm tạo lại PDB.");
            await VerifyServiceAsync(environment, sysPassword, service, identity,
                retryRegistration: true, cancellationToken);

            var storageMessage = usersCreated
                ? $"USERS khởi tạo 100 MB, tự tăng tối đa {maxDataSizeGb} GB (không phải giới hạn toàn PDB)."
                : "USERS đã có sẵn trong mẫu Oracle; giữ nguyên cấu hình dung lượng của mẫu, không áp dụng giới hạn vừa nhập.";
            var message = $"Đã tạo PDB {name}, kiểm tra service {service} và lưu trạng thái tự mở trên instance hiện tại. {storageMessage} Tài khoản PDBADMIN đã khóa; dùng SYS để quản trị. Chưa tạo schema hoặc phục hồi dữ liệu.";
            journal.Complete(operation.Id, OperationState.Succeeded, message);
            logger.LogInformation("Đã tạo PDB {PdbName} cho kết nối {EnvironmentKey}", name, environment.Key);
            return new(name, service, message);
        }
        catch (Exception exception)
        {
            var message = $"Lỗi tại bước {stage}: {SafeMessage(exception, environment)}";
            if (creationStarted)
            {
                message += created
                    ? $" PDB {name} đã được tạo, nhưng chưa hoàn tất cấu hình."
                    : $" Yêu cầu tạo PDB {name} đã gửi; PDB có thể đã được tạo hoặc còn trạng thái lỗi.";
                if (!adminLocked) message += " Chưa xác nhận khóa PDBADMIN; cần SYS kiểm tra tài khoản trong PDB này.";
                message += " Miniapp không xóa PDB hay datafile. Hãy tải lại danh sách và kiểm tra trạng thái, không tạo lại cùng tên.";
            }
            journal.Complete(operation.Id, OperationState.Failed, message);
            // Do not attach provider exception/CommandText: failed DDL may contain generated credentials.
            logger.LogWarning("Tạo PDB {PdbName} chưa hoàn tất tại bước {Stage}, kết nối {EnvironmentKey}", name, stage, environment.Key);
            if (exception is OperationCanceledException && !creationStarted) throw;
            throw new InvalidOperationException(message);
        }
        finally
        {
            _creationGate.Release();
        }
    }

    public static string NormalizePdbName(string value)
    {
        var name = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!NewPdbNameRegex().IsMatch(name) || IsReservedName(name))
            throw Failure("Tên PDB dùng 1–30 ký tự chữ, số hoặc dấu gạch dưới, bắt đầu bằng chữ; không dùng tên quản trị Oracle.");
        return name;
    }

    public static string? NormalizeFileDestination(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var destination = value.Trim();
        var linuxDirectory = destination.StartsWith('/') && destination.TrimEnd('/').Length > 0
            && !destination.Split('/').Any(segment => segment is "." or "..");
        if (destination.Length > 1000 || destination.Any(char.IsControl)
            || destination.Contains('\'') || destination.Contains('"')
            || (!linuxDirectory && !AsmDiskGroupRegex().IsMatch(destination)))
            throw Failure("Thư mục datafile phải là đường dẫn tuyệt đối trên máy chủ Linux (không dùng /, dấu nháy hoặc ..), hoặc tên ASM như +DATA.");
        return destination;
    }

    public static string BuildCreatePdbSql(string pdbName, string adminPassword, string? createFileDestination)
    {
        var name = NormalizePdbName(pdbName);
        var destination = NormalizeFileDestination(createFileDestination);
        if (string.IsNullOrWhiteSpace(adminPassword) || adminPassword.Length > 128
            || adminPassword.Contains('"') || adminPassword.Any(char.IsControl))
            throw Failure("Mật khẩu quản trị khởi tạo không hợp lệ.");
        return $"CREATE PLUGGABLE DATABASE \"{name}\" ADMIN USER \"PDBADMIN\" IDENTIFIED BY \"{adminPassword}\""
            + (destination is null ? string.Empty : $" CREATE_FILE_DEST = '{destination}'");
    }

    public static string BuildUsersTablespaceSql(int maxDataSizeGb)
    {
        if (maxDataSizeGb is < 1 or > 1024)
            throw Failure("Dung lượng tối đa cho tablespace USERS phải từ 1 đến 1024 GB.");
        return $"CREATE BIGFILE TABLESPACE \"USERS\" DATAFILE SIZE 100M AUTOEXTEND ON NEXT 100M MAXSIZE {maxDataSizeGb.ToString(CultureInfo.InvariantCulture)}G";
    }

    private void ValidateRequest(ManagedEnvironmentOptions environment, string sysPassword)
    {
        if (!environment.Enabled || !environment.Safety.AllowRestore)
            throw Failure("Kết nối đích đang tắt hoặc chưa được phép phục hồi.");
        var configuredEnvironment = options.CurrentValue.Environments.FirstOrDefault(item =>
            string.Equals(item.Key, environment.Key, StringComparison.OrdinalIgnoreCase));
        if ((string.Equals(environment.Tier, "Production", StringComparison.OrdinalIgnoreCase)
                || string.Equals(configuredEnvironment?.Tier, "Production", StringComparison.OrdinalIgnoreCase))
            && !options.CurrentValue.AllowProductionOperations)
            throw Failure("Thao tác trên kết nối Production đang bị khóa theo cấu hình.");
        if (string.IsNullOrWhiteSpace(sysPassword)) throw Failure("Hãy nhập mật khẩu SYS để tìm hoặc tạo PDB.");
        if (string.IsNullOrWhiteSpace(environment.Oracle.Host) || string.IsNullOrWhiteSpace(environment.Oracle.ServiceName)
            || environment.Oracle.Port is < 1 or > 65535)
            throw Failure("Kết nối đích chưa có Host, cổng hoặc Service hợp lệ.");
    }

    private static async Task<(bool IsCdb, string ContainerName)> MoveToRootAsync(OracleConnection connection, CancellationToken ct)
    {
        var isCdb = await ScalarStringAsync(connection, "SELECT CDB FROM V$DATABASE", ct) == "YES";
        var container = await ScalarStringAsync(connection,
            "SELECT SYS_CONTEXT('USERENV', 'CON_NAME') FROM DUAL", ct) ?? string.Empty;
        if (isCdb && !string.Equals(container, "CDB$ROOT", StringComparison.OrdinalIgnoreCase))
            await ExecuteAsync(connection, "ALTER SESSION SET CONTAINER = CDB$ROOT", ct);
        return (isCdb, container);
    }

    private static async Task<List<OraclePdbChoice>> ReadChoicesAsync(OracleConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT P.NAME, NVL(S.NETWORK_NAME, S.NAME), P.OPEN_MODE
            FROM V$PDBS P
            LEFT JOIN V$SERVICES S ON S.CON_ID = P.CON_ID
            WHERE P.CON_ID > 2
            ORDER BY P.NAME, S.NAME
            """;
        command.CommandTimeout = 30;
        var choices = new List<OraclePdbChoice>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            choices.Add(new(reader.GetString(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1), reader.GetString(2)));
        return choices.Distinct().ToList();
    }

    private sealed record PdbIdentity(string Name, string Guid, string OpenMode, string Restricted, string Status);

    private static async Task<PdbIdentity> ReadIdentityAsync(OracleConnection connection, string name, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT P.NAME, RAWTOHEX(P.GUID), P.OPEN_MODE, NVL(P.RESTRICTED, 'NO'), D.STATUS
            FROM V$PDBS P JOIN DBA_PDBS D ON D.PDB_ID = P.CON_ID
            WHERE P.CON_ID > 2 AND UPPER(P.NAME) = :pdb_name
            """;
        command.BindByName = true;
        command.Parameters.Add("pdb_name", OracleDbType.Varchar2).Value = name.ToUpperInvariant();
        command.CommandTimeout = 30;
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw Failure("PDB không còn tồn tại. Hãy tải lại danh sách.");
        return new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4));
    }

    private static void EnsureReady(PdbIdentity identity)
    {
        if (identity.OpenMode != "READ WRITE" || identity.Restricted != "NO" || identity.Status != "NORMAL")
            throw Failure($"PDB {identity.Name} chưa sẵn sàng (mở: {identity.OpenMode}, trạng thái: {identity.Status}, hạn chế: {identity.Restricted}). Cần quản trị viên kiểm tra; miniapp không tự mở hoặc thay đổi PDB đang có.");
    }

    private static async Task EnsureNameAvailableAsync(OracleConnection connection, string name, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM (
                SELECT NAME FROM V$CONTAINERS WHERE UPPER(NAME) = :pdb_name
                UNION ALL
                SELECT PDB_NAME FROM DBA_PDBS WHERE UPPER(PDB_NAME) = :pdb_name
                UNION ALL
                SELECT NAME FROM V$SERVICES WHERE UPPER(NAME) = :pdb_name
                    OR UPPER(NETWORK_NAME) = :pdb_name
                    OR UPPER(REGEXP_SUBSTR(NETWORK_NAME, '^[^.]+')) = :pdb_name
            )
            """;
        command.BindByName = true;
        command.Parameters.Add("pdb_name", OracleDbType.Varchar2).Value = name;
        command.CommandTimeout = 30;
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture) != 0)
            throw Failure($"PDB hoặc service {name} đã tồn tại. Hãy chọn nơi lưu dữ liệu có sẵn hoặc đổi tên; miniapp không ghi đè.");
    }

    private static async Task VerifyServiceAsync(ManagedEnvironmentOptions environment, string sysPassword,
        string service, PdbIdentity expected, bool retryRegistration, CancellationToken ct)
    {
        ValidateServiceName(service);
        var attempts = retryRegistration ? 6 : 1;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                await using var connection = NewConnection(environment, sysPassword, service);
                await OpenWithRetryAsync(connection, ct);
                var container = await ScalarStringAsync(connection, "SELECT SYS_CONTEXT('USERENV', 'CON_NAME') FROM DUAL", ct);
                if (!string.Equals(container, expected.Name, StringComparison.OrdinalIgnoreCase))
                    throw Failure("Service không kết nối vào đúng PDB đã chọn. Có thể trùng service giữa các database; chưa thay đổi schema.");
                var actual = await ReadIdentityAsync(connection, expected.Name, ct);
                if (!string.Equals(actual.Guid, expected.Guid, StringComparison.OrdinalIgnoreCase))
                    throw Failure("Service dẫn tới PDB khác cùng tên. Cần quản trị viên xử lý trùng service trên listener.");
                EnsureReady(actual);
                return;
            }
            catch (OracleException exception) when (retryRegistration && exception.Number is 12514 or 12505
                && attempt < attempts - 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }

    private static OracleConnection NewConnection(ManagedEnvironmentOptions environment, string password, string? service = null) =>
        new(new OracleConnectionStringBuilder
        {
            UserID = "SYS", Password = password, DBAPrivilege = "SYSDBA", Pooling = false, ConnectionTimeout = 10,
            DataSource = $"{environment.Oracle.Host}:{environment.Oracle.Port}/{service ?? environment.Oracle.ServiceName}"
        }.ConnectionString);

    private static async Task ExecuteAsync(OracleConnection connection, string sql, CancellationToken ct, int commandTimeout = 30)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = commandTimeout;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task OpenWithRetryAsync(OracleConnection connection, CancellationToken ct)
    {
        const int maximumAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await connection.OpenAsync(ct);
                return;
            }
            catch (OracleException exception) when (attempt < maximumAttempts && IsTransientConnectionFailure(exception))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), ct);
            }
        }
    }

    private static bool IsTransientConnectionFailure(OracleException exception) =>
        exception.Number is 50201 or 12170 or 12514 or 12541 or 12543;

    private static async Task<string?> ScalarStringAsync(OracleConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 30;
        var value = await command.ExecuteScalarAsync(ct);
        return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static void ValidateExistingPdbName(string name)
    {
        if (!ExistingPdbNameRegex().IsMatch(name) || IsReservedName(name.ToUpperInvariant()))
            throw Failure("Hãy chọn một PDB ứng dụng hợp lệ trong danh sách.");
    }

    private static void ValidateServiceName(string name)
    {
        if (!ServiceNameRegex().IsMatch(name)) throw Failure("Service PDB không hợp lệ hoặc chưa đăng ký; hãy tải lại danh sách.");
    }

    private static bool IsReservedName(string name) => name is "SYS" or "SYSTEM" or "PDBADMIN" or "CDB" or "CDB$ROOT" or "PDB$SEED"
        || name.StartsWith("C##", StringComparison.Ordinal) || name.StartsWith("ORA$", StringComparison.Ordinal);

    // Only messages constructed here are safe to expose. Never retain a provider exception as an inner exception.
    private sealed class SafePdbException(string message) : InvalidOperationException(message);
    private static SafePdbException Failure(string message) => new(message);

    private static string SafeMessage(Exception exception, ManagedEnvironmentOptions? environment = null) => exception switch
    {
        SafePdbException => exception.Message,
        OperationCanceledException => "Thao tác bị hủy hoặc quá thời gian chờ.",
        OracleException oracle => oracle.Number switch
        {
            1017 => "Mật khẩu SYS không đúng hoặc SYS không đăng nhập được vào service đã chọn (ORA-01017).",
            1031 => "SYS chưa có quyền thực hiện thao tác (ORA-01031).",
            1521 => "Đã đạt giới hạn số PDB của database (ORA-01521).",
            65010 => "Đã đạt giới hạn số PDB của database (ORA-65010).",
            65012 => "Tên PDB đã tồn tại; không tạo hoặc ghi đè lần nữa (ORA-65012).",
            65016 => "Chưa cấu hình vị trí datafile hợp lệ cho PDB (ORA-65016).",
            65165 => "Thư mục datafile không hợp lệ hoặc Oracle không có quyền ghi (ORA-65165).",
            19504 or 27040 or 27041 or 27044 => $"Oracle không tạo được datafile. Kiểm tra thư mục, quyền ghi và dung lượng ổ trên máy chủ (ORA-{oracle.Number:00000}).",
            50201 => environment is null
                ? "Không thiết lập được kết nối Oracle (ORA-50201). Kiểm tra máy chủ, cổng, service và listener."
                : $"Không kết nối được {environment.Oracle.Host}:{environment.Oracle.Port}/{environment.Oracle.ServiceName} (ORA-50201). Kiểm tra mạng/VPN và listener; nếu lỗi ở bước kết nối thì lệnh tạo PDB chưa được gửi.",
            12170 => "Kết nối Oracle hết thời gian chờ (ORA-12170). Kiểm tra mạng hoặc listener.",
            12541 => "Không có listener Oracle tại máy chủ/cổng đã nhập (ORA-12541).",
            12543 => "Không tới được máy chủ Oracle qua mạng (ORA-12543).",
            12514 or 12505 => $"Listener chưa nhận service của PDB. Chờ một lát rồi tải lại danh sách; không tạo lại PDB (ORA-{oracle.Number:00000}).",
            _ => $"Oracle từ chối thao tác (ORA-{oracle.Number:00000}). Kiểm tra alert log trên máy chủ để biết chi tiết."
        },
        _ => "Không hoàn tất được thao tác PDB. Kiểm tra kết nối và trạng thái trên máy chủ rồi thử lại."
    };

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]{0,29}$", RegexOptions.CultureInvariant)]
    private static partial Regex NewPdbNameRegex();
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_$#]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex ExistingPdbNameRegex();
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._$#-]{0,254}$", RegexOptions.CultureInvariant)]
    private static partial Regex ServiceNameRegex();
    [GeneratedRegex(@"^\+[A-Za-z][A-Za-z0-9_]{0,29}$", RegexOptions.CultureInvariant)]
    private static partial Regex AsmDiskGroupRegex();
}
