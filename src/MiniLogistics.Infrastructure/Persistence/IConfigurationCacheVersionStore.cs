namespace MiniLogistics.Infrastructure.Persistence;

public enum ConfigurationCacheScope
{
    RouteRegions,
    FeeRules,
    Hubs
}

/// <summary>
/// Provides shared generations for process-local configuration caches.
/// </summary>
public interface IConfigurationCacheVersionStore
{
    long GetVersion(ConfigurationCacheScope scope);

    Task<long> GetVersionAsync(
        ConfigurationCacheScope scope,
        CancellationToken cancellationToken = default);

    Task<long> IncrementAsync(
        ConfigurationCacheScope scope,
        CancellationToken cancellationToken = default);
}
