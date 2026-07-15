namespace MiniLogistics.Web.Services;

public sealed class PartnerApiRateLimitOptions
{
    public const string SectionName = "PartnerApi:RateLimiting";

    public string Mode { get; set; } = "Memory";

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
}
