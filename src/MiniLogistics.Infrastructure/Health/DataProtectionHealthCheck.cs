using Microsoft.Extensions.Diagnostics.HealthChecks;
using MiniLogistics.Application.PartnerApi;

namespace MiniLogistics.Infrastructure.Health;

public sealed class DataProtectionHealthCheck : IHealthCheck
{
    private readonly ISecretProtector _secretProtector;

    public DataProtectionHealthCheck(ISecretProtector secretProtector)
    {
        _secretProtector = secretProtector;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var probe = Guid.NewGuid().ToString("N");
            var protectedValue = _secretProtector.Protect(probe);
            return Task.FromResult(_secretProtector.Unprotect(protectedValue) == probe
                ? HealthCheckResult.Healthy("Data Protection can protect and unprotect data.")
                : HealthCheckResult.Unhealthy("Data Protection round-trip validation failed."));
        }
        catch (Exception exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Data Protection readiness check failed.",
                exception));
        }
    }
}
