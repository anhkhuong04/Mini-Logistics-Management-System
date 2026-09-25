namespace MiniLogistics.Infrastructure.Persistence;

internal sealed record VersionedConfigurationCacheEntry<T>(long Version, T Value);

internal static class ConfigurationCacheStoreFailure
{
    public static bool IsUnavailable(Exception exception) =>
        exception is StackExchange.Redis.RedisException or TimeoutException;
}
