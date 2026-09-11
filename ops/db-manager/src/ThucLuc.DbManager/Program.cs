using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using ThucLuc.DbManager.Components;
using ThucLuc.DbManager.Configuration;
using ThucLuc.DbManager.Services;

if (args.Contains("--hash-password", StringComparer.Ordinal))
{
    var password = Console.In.ReadLine() ?? string.Empty;
    Console.Out.WriteLine(AdminPasswordHasher.Hash(password));
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

var externalConfigPath = Environment.GetEnvironmentVariable("THUCLUC_OPS_CONFIG_PATH")
    ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../config/environments.json"));
var statePath = Environment.GetEnvironmentVariable("THUCLUC_OPS_STATE_PATH")
    ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../state"));
var releasePath = Environment.GetEnvironmentVariable("THUCLUC_OPS_RELEASE_PATH");
var runtimePaths = new OpsRuntimePaths(externalConfigPath, statePath, releasePath);
builder.Configuration.AddJsonFile(
    externalConfigPath,
    optional: true,
    reloadOnChange: true);
builder.Configuration.AddJsonFile(
    "appsettings.Local.json",
    optional: true,
    reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

builder.Services
    .AddOptions<OpsOptions>()
    .Bind(builder.Configuration.GetSection(OpsOptions.SectionName))
    .Validate(options => options.Environments
        .Select(environment => environment.Key)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count() == options.Environments.Count,
        "Mỗi môi trường phải có Key duy nhất.")
    .Validate(options => options.Hosts
        .Select(host => host.Key)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count() == options.Hosts.Count,
        "Mỗi máy chủ phải có Key duy nhất.")
    .Validate(options => options.Hosts.All(host =>
            !host.Enabled
            || (!string.IsNullOrWhiteSpace(host.Key)
                && !string.IsNullOrWhiteSpace(host.DisplayName)
                && !string.IsNullOrWhiteSpace(host.Role)
                && !string.IsNullOrWhiteSpace(host.EnvironmentKey)
                && (string.Equals(options.ExecutionMode, "Local", StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(host.Host)
                        && !string.IsNullOrWhiteSpace(host.Username)
                        && host.Port is > 0 and <= 65_535)))),
        "Máy chủ đang bật phải có đủ Key, tên, role, EnvironmentKey, host, username và port hợp lệ.")
    .Validate(options => options.Hosts
        .SelectMany(host => host.AllowedActions)
        .All(action => Enum.TryParse<ThucLuc.DbManager.Models.HostAction>(action, ignoreCase: true, out _)),
        "AllowedActions chứa thao tác không được Ops Console hỗ trợ.")
    .Validate(options => options.DiskWarningPercent is >= 50 and <= 95,
        "DiskWarningPercent phải nằm trong khoảng 50-95.")
    .Validate(options => string.Equals(options.ExecutionMode, "Local", StringComparison.OrdinalIgnoreCase)
            || string.Equals(options.ExecutionMode, "Ssh", StringComparison.OrdinalIgnoreCase),
        "ExecutionMode chỉ nhận Local hoặc Ssh.")
    .Validate(options => !options.Authentication.Enabled
            || (!string.IsNullOrWhiteSpace(options.Authentication.AdminUser)
                && options.Authentication.SessionHours is >= 1 and <= 24),
        "Cấu hình đăng nhập Ops Console không hợp lệ.")
    .Validate(options => options.SshConnectTimeoutSeconds is >= 3 and <= 60
            && options.RemoteCommandTimeoutSeconds is >= 5 and <= 3_600
            && options.LongRunningCommandTimeoutSeconds is >= 60 and <= 21_600
            && options.ArtifactUploadTimeoutSeconds is >= 30 and <= 7_200,
        "Timeout SSH/upload nằm ngoài giới hạn an toàn.")
    .ValidateOnStart();

builder.Services.AddSingleton<IToolLocator, ToolLocator>();
builder.Services.AddSingleton(runtimePaths);
builder.Services.AddSingleton<BackupStoragePathResolver>();
builder.Services.AddSingleton<IEnvironmentConfigurationService, EnvironmentConfigurationService>();
builder.Services.AddSingleton<IOracleCredentialStore, ProtectedOracleCredentialStore>();
builder.Services.AddSingleton<IOracleCredentialResolver, OracleCredentialResolver>();
builder.Services.AddScoped<IPreflightService, PreflightService>();
builder.Services.AddSingleton<IOracleBackupService, OracleBackupService>();
builder.Services.AddSingleton<IBackupUploadService, BackupUploadService>();
builder.Services.AddSingleton<IOracleDirectorySetupService, OracleDirectorySetupService>();
builder.Services.AddSingleton<IOracleSchemaProvisioningService, OracleSchemaProvisioningService>();
builder.Services.AddSingleton<IOraclePdbService, OraclePdbService>();
builder.Services.AddSingleton<IOracleRestoreService, OracleRestoreService>();
builder.Services.AddSingleton<IOperationJournal, SqliteOperationJournal>();
if (string.Equals(
        builder.Configuration.GetValue<string>($"{OpsOptions.SectionName}:ExecutionMode"),
        "Local",
        StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IHostExecutor, LocalHostExecutor>();
}
else
{
    builder.Services.AddSingleton<IHostExecutor, SshHostExecutor>();
}
builder.Services.AddSingleton<IAdminCredentialValidator, AdminCredentialValidator>();
builder.Services.AddSingleton<IReleasePackageService, ReleasePackageService>();
builder.Services.AddSingleton<IReleaseMigrationService, ReleaseMigrationService>();
builder.Services.AddSingleton<IReleaseDeploymentService, ReleaseDeploymentService>();
builder.Services.AddSingleton<IMigrationInspectionService, MigrationInspectionService>();
builder.Services.AddSingleton<OpsJobQueue>();
builder.Services.AddSingleton<IOpsJobQueue>(services => services.GetRequiredService<OpsJobQueue>());
builder.Services.AddHostedService(services => services.GetRequiredService<OpsJobQueue>());
var dataProtectionPath = Path.Combine(statePath, "data-protection");
Directory.CreateDirectory(dataProtectionPath);
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("ThucLuc.DbManager")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
var protectionCertificatePath = builder.Configuration.GetValue<string>(
    $"{OpsOptions.SectionName}:DataProtectionCertificatePath");
if (!string.IsNullOrWhiteSpace(protectionCertificatePath))
{
    var resolvedCertificatePath = Path.GetFullPath(protectionCertificatePath);
    if (!File.Exists(resolvedCertificatePath))
    {
        throw new InvalidOperationException(
            $"Không tìm thấy certificate bảo vệ credential: {resolvedCertificatePath}");
    }

    var passwordFile = builder.Configuration.GetValue<string>(
        $"{OpsOptions.SectionName}:DataProtectionCertificatePasswordFile");
    if (string.IsNullOrWhiteSpace(passwordFile) || !File.Exists(passwordFile))
    {
        throw new InvalidOperationException("Không tìm thấy file mật khẩu certificate bảo vệ credential.");
    }

    var certificatePassword = File.ReadAllText(passwordFile).TrimEnd('\r', '\n');
    if (string.IsNullOrEmpty(certificatePassword))
    {
        throw new InvalidOperationException("File mật khẩu certificate bảo vệ credential đang trống.");
    }

    var protectionCertificate = new X509Certificate2(
        resolvedCertificatePath,
        certificatePassword,
        X509KeyStorageFlags.EphemeralKeySet);
    if (!protectionCertificate.HasPrivateKey)
    {
        throw new InvalidOperationException("Certificate bảo vệ credential không có private key.");
    }
    dataProtection.ProtectKeysWithCertificate(protectionCertificate);
}
else if (OperatingSystem.IsWindows()
    && builder.Configuration.GetValue($"{OpsOptions.SectionName}:ProtectKeysWithDpapi", true))
{
    dataProtection.ProtectKeysWithDpapi();
}
else if (builder.Configuration.GetValue<bool>(
             $"{OpsOptions.SectionName}:RequireEncryptedDataProtectionKeys"))
{
    throw new InvalidOperationException(
        "Cấu hình yêu cầu mã hóa Data Protection key nhưng chưa khai báo certificate.");
}
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.Cookie.Name = "ThucLuc.OpsConsole.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(Math.Clamp(
            builder.Configuration.GetValue<int>($"{OpsOptions.SectionName}:Authentication:SessionHours", 8),
            1,
            24));
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Tạo sẵn kho backup và các vùng chức năng; lỗi quyền sẽ xuất hiện ngay khi khởi động.
app.Services.GetRequiredService<BackupStoragePathResolver>().ResolveAndEnsure();
Directory.CreateDirectory(runtimePaths.ReleaseDirectory);
var recoveredOperations = app.Services.GetRequiredService<IOperationJournal>().RecoverInterrupted();
if (recoveredOperations > 0)
{
    app.Logger.LogWarning(
        "Đã đánh dấu {OperationCount} thao tác dở dang là thất bại sau khi ứng dụng khởi động lại",
        recoveredOperations);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

if (builder.Configuration.GetValue<bool>($"{OpsOptions.SectionName}:UseHttpsRedirection"))
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/health/live", () => Results.Ok(new
{
    status = "Healthy",
    application = "ThucLuc.OpsConsole",
    timestamp = DateTimeOffset.Now
})).AllowAnonymous();

app.MapPost("/auth/login", async (HttpContext context, IAdminCredentialValidator credentials) =>
{
    var form = await context.Request.ReadFormAsync(context.RequestAborted);
    var userName = form["userName"].ToString();
    var password = form["password"].ToString();
    if (!credentials.Validate(userName, password))
    {
        return Results.LocalRedirect("/login?error=1");
    }

    var identity = new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, userName), new Claim(ClaimTypes.Role, "Administrator")],
        CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = false });
    var returnUrl = form["returnUrl"].ToString();
    if (string.IsNullOrWhiteSpace(returnUrl)
        || !returnUrl.StartsWith('/')
        || returnUrl.StartsWith("//", StringComparison.Ordinal))
    {
        returnUrl = "/";
    }
    return Results.LocalRedirect(returnUrl);
}).DisableAntiforgery().AllowAnonymous();

app.MapPost("/auth/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/login");
}).DisableAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
