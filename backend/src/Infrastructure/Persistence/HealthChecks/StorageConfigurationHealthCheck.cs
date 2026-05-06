using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ThucLuc.Infrastructure.Options;

namespace ThucLuc.Infrastructure.Persistence.HealthChecks;

public sealed class StorageConfigurationHealthCheck : IHealthCheck
{
    private readonly MinioOptions _options;

    public StorageConfigurationHealthCheck(IOptions<MinioOptions> options)
    {
        _options = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var isValid = !string.IsNullOrWhiteSpace(_options.ServiceUrl)
                      && !string.IsNullOrWhiteSpace(_options.AccessKey)
                      && !string.IsNullOrWhiteSpace(_options.SecretKey)
                      && !string.IsNullOrWhiteSpace(_options.BucketName);

        return Task.FromResult(isValid
            ? HealthCheckResult.Healthy("Storage configuration is valid.")
            : HealthCheckResult.Unhealthy("Storage configuration is invalid."));
    }
}
