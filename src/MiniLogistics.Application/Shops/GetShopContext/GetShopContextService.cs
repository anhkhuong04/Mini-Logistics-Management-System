using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shops.GetShopContext;

public sealed class GetShopContextService : IGetShopContextService
{
    private readonly IShopAccessService _shopAccessService;

    public GetShopContextService(IShopAccessService shopAccessService)
    {
        _shopAccessService = shopAccessService;
    }

    public async Task<Result<GetShopContextResponse>> GetAsync(
        Guid currentUserId,
        Guid? selectedShopId = null,
        CancellationToken cancellationToken = default)
    {
        var shopsResult = await _shopAccessService.GetAccessibleShopsAsync(
            currentUserId,
            cancellationToken);
        if (shopsResult.IsFailure)
        {
            return Result<GetShopContextResponse>.Failure(shopsResult.Error);
        }

        var shops = shopsResult.Value;
        var selectedShop = ResolveSelectedShop(shops, selectedShopId);
        if (selectedShop is null)
        {
            return Result<GetShopContextResponse>.Failure(
                ApplicationErrors.Forbidden("Current user cannot access this shop."));
        }

        return Result<GetShopContextResponse>.Success(new GetShopContextResponse(
            selectedShop.Id,
            shops.Select(ToResponse).ToList()));
    }

    private static Shop? ResolveSelectedShop(
        IReadOnlyList<Shop> shops,
        Guid? selectedShopId)
    {
        if (selectedShopId.HasValue)
        {
            return shops.FirstOrDefault(shop => shop.Id == selectedShopId.Value);
        }

        return shops
            .OrderByDescending(shop => shop.IsActive)
            .ThenBy(shop => shop.Name)
            .FirstOrDefault();
    }

    private static ShopContextItemResponse ToResponse(Shop shop)
    {
        return new ShopContextItemResponse(
            shop.Id,
            shop.Name,
            shop.PhoneNumber.Value,
            shop.Address.Street,
            shop.Address.Ward,
            shop.Address.Province,
            shop.Address.Country,
            shop.IsActive);
    }
}
