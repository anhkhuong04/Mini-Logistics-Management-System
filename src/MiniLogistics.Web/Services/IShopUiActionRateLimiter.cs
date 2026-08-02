namespace MiniLogistics.Web.Services;

public interface IShopUiActionRateLimiter
{
    bool TryAcquire(
        Guid userId,
        ShopUiActionKind kind,
        out TimeSpan retryAfter);
}

