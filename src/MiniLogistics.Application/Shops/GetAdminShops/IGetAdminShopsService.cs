using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.GetAdminShops;

public interface IGetAdminShopsService
{
    Task<Result<IReadOnlyList<GetAdminShopResponse>>> GetAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}
