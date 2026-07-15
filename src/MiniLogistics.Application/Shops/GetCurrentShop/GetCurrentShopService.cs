using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.GetCurrentShop;

public sealed class GetCurrentShopService : IGetCurrentShopService
{
    private readonly IShopAccessService _shopAccessService;

    public GetCurrentShopService(IShopAccessService shopAccessService)
    {
        _shopAccessService = shopAccessService;
    }

    public async Task<Result<GetCurrentShopResponse>> GetAsync(
        Guid ownerUserId,
        Guid? shopId = null,
        CancellationToken cancellationToken = default)
    {
        var shopResult = await _shopAccessService.GetShopForUserAsync(
            ownerUserId,
            shopId,
            requireActiveShop: false,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<GetCurrentShopResponse>.Failure(shopResult.Error);
        }

        var shop = shopResult.Value;
        return Result<GetCurrentShopResponse>.Success(new GetCurrentShopResponse(
            shop.Id,
            shop.Name,
            shop.PhoneNumber.Value,
            shop.Address.Street,
            shop.Address.Ward,
            shop.Address.Province,
            shop.Address.Country,
            shop.IsActive));
    }
}
