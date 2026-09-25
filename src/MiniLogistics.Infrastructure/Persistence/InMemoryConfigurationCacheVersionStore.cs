using System.Collections.Concurrent;

namespace MiniLogistics.Infrastructure.Persistence;

public sealed class InMemoryConfigurationCacheVersionStore : IConfigurationCacheVersionStore
{
    private readonly ConcurrentDictionary<ConfigurationCacheScope, long> _versions = new();

    public long GetVersion(ConfigurationCacheScope scope) => _versions.GetOrAdd(scope, 0);

    public Task<long> GetVersionAsync(
        ConfigurationCacheScope scope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetVersion(scope));
    }

    public Task<long> IncrementAsync(
        ConfigurationCacheScope scope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var version = _versions.AddOrUpdate(scope, 1, static (_, current) => checked(current + 1));
        return Task.FromResult(version);
    }
}
