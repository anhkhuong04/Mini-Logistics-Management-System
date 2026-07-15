namespace MiniLogistics.Web.Services;

public interface IPublicTrackingRateLimiter
{
    bool TryAcquire(
        string clientKey,
        string trackingCode,
        out TimeSpan retryAfter);
}
