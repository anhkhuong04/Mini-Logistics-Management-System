using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.GetShopProfile;

/// <summary>
/// Defines the application use case contract for Get Shop Profile.
/// </summary>
public interface IGetShopProfileService
{
    Task<Result<GetShopProfileResponse>> GetAsync(
        Guid currentUserId,
        Guid? shopId = null,
        CancellationToken cancellationToken = default);
}
