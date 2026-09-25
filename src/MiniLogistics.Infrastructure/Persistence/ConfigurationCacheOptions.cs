namespace MiniLogistics.Infrastructure.Persistence;

public sealed class ConfigurationCacheOptions
{
    public const string SectionName = "ConfigurationCache";
    public const int DefaultConsistencyWindowSeconds = 15;

    public string KeyPrefix { get; set; } = "minilogistics:configuration";

    public int ConsistencyWindowSeconds { get; set; } = DefaultConsistencyWindowSeconds;
}
