using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shops.GetShopProfile;

public sealed class GetShopProfileService : IGetShopProfileService
{
    private readonly IShopAccessService _shopAccessService;

    public GetShopProfileService(IShopAccessService shopAccessService)
    {
        _shopAccessService = shopAccessService;
    }

    public async Task<Result<GetShopProfileResponse>> GetAsync(
        Guid currentUserId,
        Guid? shopId = null,
        CancellationToken cancellationToken = default)
    {
        var shopResult = await _shopAccessService.GetShopForUserAsync(
            currentUserId,
            shopId,
            requireActiveShop: false,
            cancellationToken);

        if (shopResult.IsFailure)
        {
            return Result<GetShopProfileResponse>.Failure(shopResult.Error);
        }

        return Result<GetShopProfileResponse>.Success(ToResponse(shopResult.Value));
    }

    private static GetShopProfileResponse ToResponse(Shop shop)
    {
        return new GetShopProfileResponse(
            shop.Id,
            shop.Name,
            shop.PhoneNumber.Value,
            shop.Address.Street,
            shop.Address.Ward,
            shop.Address.Province,
            shop.Address.Country,
            shop.IsActive,
            shop.CreatedAtUtc,
            shop.UpdatedAtUtc);
    }
}
