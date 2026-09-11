using Microsoft.Extensions.Logging.Abstractions;
using ThucLuc.DbManager.Checks;
using ThucLuc.DbManager.Configuration;
using ThucLuc.DbManager.Services;

var checks = new CheckSuite();

FoundationChecks.Run(checks);

checks.Run("PDB name is normalized", () =>
    CheckSuite.Equal("CAND_QLCNTT", OraclePdbService.NormalizePdbName(" cand_qlcntt ")));

foreach (var invalidName in new[]
         {
             "", "1CAND", "CAND-QL", "CAND QL", "PDBADMIN", "CDB$ROOT", "PDB$SEED",
             "C##CAND", "CAND_QLCNTT; DROP PLUGGABLE DATABASE X"
         })
{
    checks.Run($"PDB name rejects {invalidName}", () =>
        CheckSuite.Throws<InvalidOperationException>(() => OraclePdbService.NormalizePdbName(invalidName)));
}

checks.Run("Linux destination is accepted", () =>
    CheckSuite.Equal("/u02/oradata", OraclePdbService.NormalizeFileDestination(" /u02/oradata ")));
checks.Run("ASM destination is accepted", () =>
    CheckSuite.Equal("+DATA", OraclePdbService.NormalizeFileDestination("+DATA")));

foreach (var invalidPath in new[] { "/", "relative/path", "/u02/../root", "/u02/data'bad", "+DATA;DROP" })
{
    checks.Run($"Destination rejects {invalidPath}", () =>
        CheckSuite.Throws<InvalidOperationException>(() => OraclePdbService.NormalizeFileDestination(invalidPath)));
}

checks.Run("CREATE PDB SQL is scoped", () =>
{
    var sql = OraclePdbService.BuildCreatePdbSql("CAND_QLCNTT", "Dbm_Aa123456", "/u02/oradata");
    CheckSuite.Contains("CREATE PLUGGABLE DATABASE \"CAND_QLCNTT\"", sql);
    CheckSuite.Contains("CREATE_FILE_DEST = '/u02/oradata'", sql);
    CheckSuite.DoesNotContain("DROP", sql);
    CheckSuite.DoesNotContain("ALTER SYSTEM", sql);
    CheckSuite.DoesNotContain("FILE_NAME_CONVERT", sql);
});

checks.Run("USERS tablespace is bounded", () =>
{
    var sql = OraclePdbService.BuildUsersTablespaceSql(2);
    CheckSuite.Contains("SIZE 100M", sql);
    CheckSuite.Contains("MAXSIZE 2G", sql);
    CheckSuite.Throws<InvalidOperationException>(() => OraclePdbService.BuildUsersTablespaceSql(0));
    CheckSuite.Throws<InvalidOperationException>(() => OraclePdbService.BuildUsersTablespaceSql(1025));
});

checks.RunAsync("Disabled connection is rejected before network", async () =>
{
    var service = CreateService(new OpsOptions());
    var environment = TargetEnvironment();
    environment.Enabled = false;
    var error = await CheckSuite.ThrowsAsync<InvalidOperationException>(() =>
        service.InspectAsync(environment, "secret"));
    CheckSuite.Contains("đang tắt", error.Message);
}).GetAwaiter().GetResult();

checks.RunAsync("Production lock is enforced before network", async () =>
{
    var environment = TargetEnvironment();
    environment.Tier = "Production";
    var options = new OpsOptions { AllowProductionOperations = false, Environments = [environment] };
    var service = CreateService(options);
    var error = await CheckSuite.ThrowsAsync<InvalidOperationException>(() =>
        service.InspectAsync(environment, "secret"));
    CheckSuite.Contains("Production", error.Message);
}).GetAwaiter().GetResult();

checks.RunAsync("Blank SYS password is rejected before network", async () =>
{
    var service = CreateService(new OpsOptions());
    var error = await CheckSuite.ThrowsAsync<InvalidOperationException>(() =>
        service.InspectAsync(TargetEnvironment(), ""));
    CheckSuite.Contains("mật khẩu SYS", error.Message);
}).GetAwaiter().GetResult();

return checks.Finish();

static OraclePdbService CreateService(OpsOptions options) => new(
    new FixedOptionsMonitor<OpsOptions>(options),
    new InMemoryOperationJournal(),
    NullLogger<OraclePdbService>.Instance);

static ManagedEnvironmentOptions TargetEnvironment() => new()
{
    Key = "target",
    DisplayName = "Cơ sở dữ liệu đích",
    Tier = "Target",
    Enabled = true,
    Oracle = new OracleEnvironmentOptions
    {
        Host = "127.0.0.1",
        Port = 1521,
        ServiceName = "orcl"
    },
    Safety = new EnvironmentSafetyOptions { AllowRestore = true }
};
