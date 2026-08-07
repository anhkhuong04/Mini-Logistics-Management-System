namespace MiniLogistics.Web.Services;

public sealed class PartnerApiRateLimitOptions
{
    public const string SectionName = "PartnerApi:RateLimiting";

    public string Mode { get; set; } = "Memory";

    public int StoreTimeoutMilliseconds { get; set; } = 500;

    public int StoreFailureThreshold { get; set; } = 3;

    public int CircuitBreakSeconds { get; set; } = 10;

    public string KeyPrefix { get; set; } = "mini-logistics";

    public int QuoteLimitPerMinute { get; set; } = 60;

    public int CreateShipmentLimitPerMinute { get; set; } = 30;

    public int TrackingLimitPerMinute { get; set; } = 120;

    public int CancelShipmentLimitPerMinute { get; set; } = 30;

    public int GetLimit(PartnerApiRateLimitKind kind)
    {
        return kind switch
        {
            PartnerApiRateLimitKind.Quote => QuoteLimitPerMinute,
            PartnerApiRateLimitKind.CreateShipment => CreateShipmentLimitPerMinute,
            PartnerApiRateLimitKind.Tracking => TrackingLimitPerMinute,
            PartnerApiRateLimitKind.CancelShipment => CancelShipmentLimitPerMinute,
            _ => QuoteLimitPerMinute
        };
    }

    public bool FailClosed(PartnerApiRateLimitKind kind)
    {
        return kind is PartnerApiRateLimitKind.CreateShipment or PartnerApiRateLimitKind.CancelShipment;
    }
}
