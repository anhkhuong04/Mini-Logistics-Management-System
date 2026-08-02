using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shops.GetCurrentShop;

public sealed class GetCurrentShopService : IGetCurrentShopService
{
    private readonly IShopAccessService _shopAccessService;
    private readonly IPiiMaskingService _piiMaskingService;

    public GetCurrentShopService(
        IShopAccessService shopAccessService,
        IPiiMaskingService? piiMaskingService = null)
    {
        _shopAccessService = shopAccessService;
        _piiMaskingService = piiMaskingService ?? new PiiMaskingService();
    }

    public async Task<Result<GetCurrentShopResponse>> GetAsync(
        Guid ownerUserId,
        Guid? shopId = null,
        CancellationToken cancellationToken = default)
    {
        var shopResult = await _shopAccessService.GetShopAccessAsync(
            ownerUserId,
            shopId,
            requireActiveShop: false,
            ShopPermission.ViewShipments,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<GetCurrentShopResponse>.Failure(shopResult.Error);
        }

        var access = shopResult.Value;
        var shop = access.Shop;
        var canViewFullPii = access.HasPermission(ShopPermission.ViewFullPii);
        return Result<GetCurrentShopResponse>.Success(new GetCurrentShopResponse(
            shop.Id,
            shop.Name,
            canViewFullPii ? shop.PhoneNumber.Value : _piiMaskingService.MaskPhone(shop.PhoneNumber.Value),
            canViewFullPii ? shop.Address.Street : _piiMaskingService.MaskAddress(shop.Address.Street),
            shop.Address.Ward,
            shop.Address.Province,
            shop.Address.Country,
            shop.IsActive));
    }
}
