namespace MiniLogistics.Web.Services;

public interface IPartnerApiRateLimiter
{
    ValueTask<PartnerApiRateLimitDecision> AcquireAsync(
        Guid apiClientId,
        PartnerApiRateLimitKind kind,
        CancellationToken cancellationToken = default);
}

public sealed record PartnerApiRateLimitDecision(
    bool IsAllowed,
    TimeSpan RetryAfter,
    bool StoreUnavailable = false)
{
    public static PartnerApiRateLimitDecision Allowed { get; } = new(true, TimeSpan.Zero);
}
