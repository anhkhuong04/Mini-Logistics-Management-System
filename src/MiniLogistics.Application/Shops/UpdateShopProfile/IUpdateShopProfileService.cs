using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.UpdateShopProfile;

/// <summary>
/// Defines the application use case contract for Update Shop Profile.
/// </summary>
public interface IUpdateShopProfileService
{
    Task<Result<UpdateShopProfileResponse>> UpdateAsync(
        UpdateShopProfileCommand command,
        CancellationToken cancellationToken = default);
}
