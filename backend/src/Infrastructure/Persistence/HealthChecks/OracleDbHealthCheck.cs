using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ThucLuc.Infrastructure.Persistence.HealthChecks;

public sealed class OracleDbHealthCheck : IHealthCheck
{
    private readonly AppDbContext _dbContext;

    public OracleDbHealthCheck(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
        return canConnect ? HealthCheckResult.Healthy("Oracle database reachable.") : HealthCheckResult.Unhealthy("Oracle database is not reachable.");
    }
}