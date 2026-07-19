using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.GetCurrentShop;

/// <summary>
/// Defines the application use case contract for Get Current Shop.
/// </summary>
public interface IGetCurrentShopService
{
    Task<Result<GetCurrentShopResponse>> GetAsync(
        Guid ownerUserId,
        Guid? shopId = null,
        CancellationToken cancellationToken = default);
}
