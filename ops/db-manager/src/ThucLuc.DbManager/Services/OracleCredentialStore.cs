using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using ThucLuc.DbManager.Configuration;

namespace ThucLuc.DbManager.Services;

public sealed record OracleCredential(string UserName, string Password);

public sealed record CredentialResolution(
    bool IsConfigured,
    string Source,
    OracleCredential? Credential = null);

public interface IOracleCredentialStore
{
    Task<bool> ContainsAsync(string environmentKey, CancellationToken cancellationToken = default);

    Task<OracleCredential?> GetAsync(string environmentKey, CancellationToken cancellationToken = default);

    Task SaveAsync(string environmentKey, OracleCredential credential, CancellationToken cancellationToken = default);

    Task DeleteAsync(string environmentKey, CancellationToken cancellationToken = default);
}

public interface IOracleCredentialResolver
{
    Task<CredentialResolution> ResolveAsync(
        ManagedEnvironmentOptions environment,
        bool includeCredential = false,
        CancellationToken cancellationToken = default);
}

public sealed class ProtectedOracleCredentialStore : IOracleCredentialStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector _protector;
    private readonly string _credentialFile;
    private readonly ILogger<ProtectedOracleCredentialStore> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public ProtectedOracleCredentialStore(
        IDataProtectionProvider dataProtectionProvider,
        OpsRuntimePaths runtimePaths,
        ILogger<ProtectedOracleCredentialStore> logger)
    {
        _protector = dataProtectionProvider.CreateProtector("OracleCredentials.v1");
        _credentialFile = Path.Combine(runtimePaths.StateDirectory, "oracle-credentials.protected");
        _logger = logger;
    }

    public async Task<bool> ContainsAsync(string environmentKey, CancellationToken cancellationToken = default)
    {
        var credentials = await ReadAsync(cancellationToken);
        return credentials.ContainsKey(environmentKey);
    }

    public async Task<OracleCredential?> GetAsync(string environmentKey, CancellationToken cancellationToken = default)
    {
        var credentials = await ReadAsync(cancellationToken);
        return credentials.GetValueOrDefault(environmentKey);
    }

    public async Task SaveAsync(
        string environmentKey,
        OracleCredential credential,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(environmentKey)
            || string.IsNullOrWhiteSpace(credential.UserName)
            || string.IsNullOrWhiteSpace(credential.Password))
        {
            throw new InvalidOperationException("Username và password Oracle không được để trống.");
        }

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var credentials = await ReadUnsafeAsync(cancellationToken);
            credentials[environmentKey] = credential;
            await WriteUnsafeAsync(credentials, cancellationToken);
            _logger.LogInformation("Đã cập nhật credential mã hóa cho môi trường {EnvironmentKey}", environmentKey);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task DeleteAsync(string environmentKey, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var credentials = await ReadUnsafeAsync(cancellationToken);
            if (credentials.Remove(environmentKey))
            {
                await WriteUnsafeAsync(credentials, cancellationToken);
                _logger.LogInformation("Đã xóa credential của môi trường {EnvironmentKey}", environmentKey);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<Dictionary<string, OracleCredential>> ReadAsync(CancellationToken cancellationToken)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            return await ReadUnsafeAsync(cancellationToken);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<Dictionary<string, OracleCredential>> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_credentialFile))
        {
            return new Dictionary<string, OracleCredential>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var protectedPayload = await File.ReadAllTextAsync(_credentialFile, cancellationToken);
            var json = _protector.Unprotect(protectedPayload);
            var stored = JsonSerializer.Deserialize<Dictionary<string, OracleCredential>>(json, SerializerOptions)
                ?? new Dictionary<string, OracleCredential>();
            return new Dictionary<string, OracleCredential>(stored, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            _logger.LogError(exception, "Không thể giải mã kho credential Oracle");
            throw new InvalidOperationException(
                "Không thể đọc kho credential mã hóa. Không thao tác DB cho tới khi xử lý kho secret.",
                exception);
        }
    }

    private async Task WriteUnsafeAsync(
        Dictionary<string, OracleCredential> credentials,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_credentialFile)!);
        var json = JsonSerializer.Serialize(credentials, SerializerOptions);
        var protectedPayload = _protector.Protect(json);
        var temporaryFile = _credentialFile + ".tmp";
        await File.WriteAllTextAsync(temporaryFile, protectedPayload, cancellationToken);
        File.Move(temporaryFile, _credentialFile, overwrite: true);
    }
}

public sealed class OracleCredentialResolver(IOracleCredentialStore credentialStore) : IOracleCredentialResolver
{
    public async Task<CredentialResolution> ResolveAsync(
        ManagedEnvironmentOptions environment,
        bool includeCredential = false,
        CancellationToken cancellationToken = default)
    {
        var environmentUser = ReadEnvironmentVariable(environment.Oracle.UserEnvironmentVariable);
        var environmentPassword = ReadEnvironmentVariable(environment.Oracle.PasswordEnvironmentVariable);
        if (environmentUser is not null && environmentPassword is not null)
        {
            return new CredentialResolution(
                true,
                "Biến môi trường",
                includeCredential ? new OracleCredential(environmentUser, environmentPassword) : null);
        }

        var stored = await credentialStore.GetAsync(environment.Key, cancellationToken);
        return stored is null
            ? new CredentialResolution(false, "Chưa cấu hình")
            : new CredentialResolution(true, "Kho mã hóa cục bộ", includeCredential ? stored : null);
    }

    private static string? ReadEnvironmentVariable(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return null;
        }

        var value = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
