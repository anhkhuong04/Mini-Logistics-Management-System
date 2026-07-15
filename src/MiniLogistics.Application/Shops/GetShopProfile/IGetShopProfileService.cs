using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.GetShopProfile;

public interface IGetShopProfileService
{
    Task<Result<GetShopProfileResponse>> GetAsync(
        Guid currentUserId,
        Guid? shopId = null,
        CancellationToken cancellationToken = default);
}
