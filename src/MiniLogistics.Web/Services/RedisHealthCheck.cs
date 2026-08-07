using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace MiniLogistics.Web.Services;

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisHealthCheck(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await _connectionMultiplexer.GetDatabase()
                .PingAsync()
                .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            return HealthCheckResult.Healthy(
                "Redis is reachable.",
                new Dictionary<string, object> { ["latencyMs"] = latency.TotalMilliseconds });
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis readiness check failed.", exception);
        }
    }
}
