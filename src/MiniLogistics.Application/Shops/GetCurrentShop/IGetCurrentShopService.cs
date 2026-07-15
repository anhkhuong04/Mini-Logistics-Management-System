using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.GetCurrentShop;

public interface IGetCurrentShopService
{
    Task<Result<GetCurrentShopResponse>> GetAsync(
        Guid ownerUserId,
        Guid? shopId = null,
        CancellationToken cancellationToken = default);
}
