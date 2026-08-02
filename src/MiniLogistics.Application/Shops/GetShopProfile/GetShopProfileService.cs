using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shops.GetShopProfile;

public sealed class GetShopProfileService : IGetShopProfileService
{
    private readonly IShopAccessService _shopAccessService;
    private readonly IPiiMaskingService _piiMaskingService;

    public GetShopProfileService(
        IShopAccessService shopAccessService,
        IPiiMaskingService? piiMaskingService = null)
    {
        _shopAccessService = shopAccessService;
        _piiMaskingService = piiMaskingService ?? new PiiMaskingService();
    }

    public async Task<Result<GetShopProfileResponse>> GetAsync(
        Guid currentUserId,
        Guid? shopId = null,
        CancellationToken cancellationToken = default)
    {
        var shopResult = await _shopAccessService.GetShopAccessAsync(
            currentUserId,
            shopId,
            requireActiveShop: false,
            ShopPermission.ViewShipments,
            cancellationToken);

        if (shopResult.IsFailure)
        {
            return Result<GetShopProfileResponse>.Failure(shopResult.Error);
        }

        return Result<GetShopProfileResponse>.Success(ToResponse(shopResult.Value));
    }

    private GetShopProfileResponse ToResponse(ShopAccessContext access)
    {
        var shop = access.Shop;
        var canViewFullPii = access.HasPermission(ShopPermission.ViewFullPii);
        return new GetShopProfileResponse(
            shop.Id,
            shop.Name,
            canViewFullPii ? shop.PhoneNumber.Value : _piiMaskingService.MaskPhone(shop.PhoneNumber.Value),
            canViewFullPii ? shop.Address.Street : _piiMaskingService.MaskAddress(shop.Address.Street),
            shop.Address.Ward,
            shop.Address.Province,
            shop.Address.Country,
            shop.IsActive,
            shop.CreatedAtUtc,
            shop.UpdatedAtUtc);
    }
}
