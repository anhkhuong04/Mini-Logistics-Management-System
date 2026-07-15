using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.UpdateShopProfile;

public interface IUpdateShopProfileService
{
    Task<Result<UpdateShopProfileResponse>> UpdateAsync(
        UpdateShopProfileCommand command,
        CancellationToken cancellationToken = default);
}
