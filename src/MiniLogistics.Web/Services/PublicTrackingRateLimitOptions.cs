namespace MiniLogistics.Web.Services;

public sealed class PublicTrackingRateLimitOptions
{
    public const string SectionName = "PublicTracking:RateLimiting";

    public int LimitPerMinute { get; set; } = 20;
}
