using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.GetShopContext;

public interface IGetShopContextService
{
    Task<Result<GetShopContextResponse>> GetAsync(
        Guid currentUserId,
        Guid? selectedShopId = null,
        CancellationToken cancellationToken = default);
}
