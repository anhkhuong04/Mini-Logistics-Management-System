using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.GetAdminShops;

/// <summary>
/// Defines the application use case contract for Get Admin Shops.
/// </summary>
public interface IGetAdminShopsService
{
    Task<Result<IReadOnlyList<GetAdminShopResponse>>> GetAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}
