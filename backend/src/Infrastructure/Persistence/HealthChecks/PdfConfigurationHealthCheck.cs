using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ThucLuc.Infrastructure.Options;

namespace ThucLuc.Infrastructure.Persistence.HealthChecks;

public sealed class PdfConfigurationHealthCheck : IHealthCheck
{
    private readonly PdfOptions _options;

    public PdfConfigurationHealthCheck(IOptions<PdfOptions> options)
    {
        _options = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_options.UseGotenberg)
        {
            return Task.FromResult(HealthCheckResult.Healthy("PDF rendering does not require Gotenberg."));
        }

        var isValid = Uri.TryCreate(_options.GotenbergUrl, UriKind.Absolute, out _)
                      && _options.TimeoutSeconds > 0;

        return Task.FromResult(isValid
            ? HealthCheckResult.Healthy("PDF configuration is valid.")
            : HealthCheckResult.Unhealthy("PDF configuration is invalid."));
    }
}
