using System.Reflection;
using Microsoft.Extensions.Options;
using ThucLuc.DbManager.Configuration;
using ThucLuc.DbManager.Services;

namespace ThucLuc.DbManager.Checks;

internal sealed class FixedOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue => value;
    public T Get(string? name) => value;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

internal sealed class NoWriteConfigurationService : IEnvironmentConfigurationService
{
    public int WriteCount { get; private set; }

    public Task SaveAsync(ManagedEnvironmentOptions environment, CancellationToken cancellationToken = default)
    {
        WriteCount++;
        throw new InvalidOperationException("Rendering must not save connection settings.");
    }

    public Task DeleteAsync(string environmentKey, CancellationToken cancellationToken = default)
    {
        WriteCount++;
        throw new InvalidOperationException("Rendering must not delete connection settings.");
    }
}

internal sealed class NoSecretsCredentialStore : IOracleCredentialStore
{
    public int SecretAccessCount { get; private set; }

    public Task<bool> ContainsAsync(string environmentKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<OracleCredential?> GetAsync(string environmentKey, CancellationToken cancellationToken = default)
    {
        SecretAccessCount++;
        throw new InvalidOperationException("Rendering must not load saved passwords.");
    }

    public Task SaveAsync(string environmentKey, OracleCredential credential, CancellationToken cancellationToken = default)
    {
        SecretAccessCount++;
        throw new InvalidOperationException("Rendering must not write saved passwords.");
    }

    public Task DeleteAsync(string environmentKey, CancellationToken cancellationToken = default)
    {
        SecretAccessCount++;
        throw new InvalidOperationException("Rendering must not delete saved passwords.");
    }
}

// Render-time tests deliberately reject every backend operation. This proxy keeps
// the no-network guarantee when methods are added to the PDB service interface.
public class NoNetworkServiceProxy : DispatchProxy
{
    public int CallCount { get; private set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        CallCount++;
        throw new InvalidOperationException($"Rendering must not call {targetMethod?.Name}.");
    }
}
