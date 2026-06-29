namespace MiniLogistics.Web.Services;

public interface IPartnerApiRateLimiter
{
    bool TryAcquire(
        Guid apiClientId,
        PartnerApiRateLimitKind kind,
        out TimeSpan retryAfter);
}
