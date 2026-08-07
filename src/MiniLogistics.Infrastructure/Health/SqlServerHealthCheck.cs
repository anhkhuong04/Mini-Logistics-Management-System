using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MiniLogistics.Infrastructure.Persistence;

namespace MiniLogistics.Infrastructure.Health;

public sealed class SqlServerHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SqlServerHealthCheck(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("SQL Server is reachable.")
                : HealthCheckResult.Unhealthy("SQL Server rejected the readiness probe.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("SQL Server readiness check failed.", exception);
        }
    }
}
