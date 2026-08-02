using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.Staff;

public interface IShopStaffManagementService
{
    Task<Result<IReadOnlyList<ShopStaffResponse>>> GetAsync(
        Guid currentUserId,
        Guid shopId,
        CancellationToken cancellationToken = default);

    Task<Result<ShopStaffResponse>> CreateAsync(
        CreateShopStaffCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<ShopStaffResponse>> UpdateAsync(
        UpdateShopStaffAccessCommand command,
        CancellationToken cancellationToken = default);
}
