using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MiniLogistics.Infrastructure.Persistence;

public sealed class RedisConfigurationCacheVersionStore : IConfigurationCacheVersionStore
{
    private readonly IDatabase _database;
    private readonly ConfigurationCacheOptions _options;

    public RedisConfigurationCacheVersionStore(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<ConfigurationCacheOptions> options)
    {
        _database = connectionMultiplexer.GetDatabase();
        _options = options.Value;
    }

    public long GetVersion(ConfigurationCacheScope scope)
    {
        var value = _database.StringGet(GetKey(scope));
        return ParseVersion(value);
    }

    public async Task<long> GetVersionAsync(
        ConfigurationCacheScope scope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _database.StringGetAsync(GetKey(scope));
        return ParseVersion(value);
    }

    public async Task<long> IncrementAsync(
        ConfigurationCacheScope scope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _database.StringIncrementAsync(GetKey(scope));
    }

    private RedisKey GetKey(ConfigurationCacheScope scope) =>
        $"{_options.KeyPrefix.TrimEnd(':')}:{scope.ToString().ToLowerInvariant()}:version";

    private static long ParseVersion(RedisValue value)
    {
        if (!value.HasValue)
        {
            return 0;
        }

        return long.TryParse(value.ToString(), out var version) && version >= 0
            ? version
            : throw new RedisException("Configuration cache version key contained an invalid value.");
    }
}
