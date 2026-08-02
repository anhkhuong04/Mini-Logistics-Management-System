using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shops.GetShopContext;

public sealed class GetShopContextService : IGetShopContextService
{
    private readonly IShopAccessService _shopAccessService;
    private readonly IPiiMaskingService _piiMaskingService;

    public GetShopContextService(
        IShopAccessService shopAccessService,
        IPiiMaskingService? piiMaskingService = null)
    {
        _shopAccessService = shopAccessService;
        _piiMaskingService = piiMaskingService ?? new PiiMaskingService();
    }

    public async Task<Result<GetShopContextResponse>> GetAsync(
        Guid currentUserId,
        Guid? selectedShopId = null,
        CancellationToken cancellationToken = default)
    {
        var shopsResult = await _shopAccessService.GetAccessibleShopAccessesAsync(
            currentUserId,
            ShopPermission.ViewShipments,
            cancellationToken);
        if (shopsResult.IsFailure)
        {
            return Result<GetShopContextResponse>.Failure(shopsResult.Error);
        }

        var accesses = shopsResult.Value;
        var selectedAccess = ResolveSelectedShop(accesses, selectedShopId);
        if (selectedAccess is null)
        {
            return Result<GetShopContextResponse>.Failure(
                ApplicationErrors.Forbidden("Current user cannot access this shop."));
        }

        return Result<GetShopContextResponse>.Success(new GetShopContextResponse(
            selectedAccess.Shop.Id,
            accesses.Select(ToResponse).ToList()));
    }

    private static ShopAccessContext? ResolveSelectedShop(
        IReadOnlyList<ShopAccessContext> accesses,
        Guid? selectedShopId)
    {
        if (selectedShopId.HasValue)
        {
            return accesses.FirstOrDefault(access => access.Shop.Id == selectedShopId.Value);
        }

        return accesses
            .OrderByDescending(access => access.Shop.IsActive)
            .ThenBy(access => access.Shop.Name)
            .FirstOrDefault();
    }

    private ShopContextItemResponse ToResponse(ShopAccessContext access)
    {
        var shop = access.Shop;
        var canViewFullPii = access.HasPermission(ShopPermission.ViewFullPii);
        return new ShopContextItemResponse(
            shop.Id,
            shop.Name,
            canViewFullPii ? shop.PhoneNumber.Value : _piiMaskingService.MaskPhone(shop.PhoneNumber.Value),
            canViewFullPii ? shop.Address.Street : _piiMaskingService.MaskAddress(shop.Address.Street),
            shop.Address.Ward,
            shop.Address.Province,
            shop.Address.Country,
            shop.IsActive,
            access.IsOwner,
            access.StaffRole,
            access.Permissions);
    }
}
