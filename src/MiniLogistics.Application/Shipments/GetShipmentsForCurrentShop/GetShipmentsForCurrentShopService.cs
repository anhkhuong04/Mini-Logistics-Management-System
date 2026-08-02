using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shipments.GetShipmentsForCurrentShop;

public sealed class GetShipmentsForCurrentShopService : IGetShipmentsForCurrentShopService
{
    private readonly IShopAccessService _shopAccessService;
    private readonly IShipmentReadRepository _shipmentRepository;
    private readonly IPiiMaskingService _piiMaskingService;

    public GetShipmentsForCurrentShopService(
        IShopAccessService shopAccessService,
        IShipmentReadRepository shipmentRepository,
        IPiiMaskingService? piiMaskingService = null)
    {
        _shopAccessService = shopAccessService;
        _shipmentRepository = shipmentRepository;
        _piiMaskingService = piiMaskingService ?? new PiiMaskingService();
    }

    public async Task<Result<IReadOnlyList<ShipmentListItemResponse>>> GetAsync(
        Guid ownerUserId,
        Guid? shopId = null,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await _shopAccessService.GetShopAccessAsync(
            ownerUserId,
            shopId,
            requireActiveShop: false,
            ShopPermission.ViewShipments,
            cancellationToken);
        if (accessResult.IsFailure)
        {
            return Result<IReadOnlyList<ShipmentListItemResponse>>.Failure(accessResult.Error);
        }

        var shop = accessResult.Value.Shop;
        var canViewFullPii = accessResult.Value.HasPermission(ShopPermission.ViewFullPii);
        var canViewCod = accessResult.Value.HasPermission(ShopPermission.ViewCod);
        var shipments = await _shipmentRepository.GetByShopIdAsync(shop.Id, cancellationToken);
        var response = shipments
            .Select(shipment => ToResponse(shipment, canViewFullPii, canViewCod))
            .ToList();

        return Result<IReadOnlyList<ShipmentListItemResponse>>.Success(response);
    }

    public async Task<Result<PagedResponse<ShipmentListItemResponse>>> SearchAsync(
        GetShipmentsForCurrentShopQuery query,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await _shopAccessService.GetShopAccessAsync(
            query.OwnerUserId,
            query.ShopId,
            requireActiveShop: false,
            ShopPermission.ViewShipments,
            cancellationToken);
        if (accessResult.IsFailure)
        {
            return Result<PagedResponse<ShipmentListItemResponse>>.Failure(accessResult.Error);
        }

        var canViewFullPii = accessResult.Value.HasPermission(ShopPermission.ViewFullPii);
        var canViewCod = accessResult.Value.HasPermission(ShopPermission.ViewCod);
        if (!canViewFullPii
            && (!string.IsNullOrWhiteSpace(query.ReceiverNameSearch)
                || !string.IsNullOrWhiteSpace(query.ReceiverPhoneSearch)
                || query.SortBy == ShopShipmentSortBy.ReceiverName))
        {
            return Result<PagedResponse<ShipmentListItemResponse>>.Failure(
                ApplicationErrors.Forbidden("ViewFullPii permission is required to filter or sort receiver data."));
        }

        if (!canViewCod
            && (query.MinCodAmount.HasValue
                || query.MaxCodAmount.HasValue
                || query.SortBy == ShopShipmentSortBy.CodAmount))
        {
            return Result<PagedResponse<ShipmentListItemResponse>>.Failure(
                ApplicationErrors.Forbidden("ViewCod permission is required to filter or sort COD data."));
        }

        var shop = accessResult.Value.Shop;
        var shipments = await _shipmentRepository.SearchByShopAsync(
            new ShopShipmentSearchCriteria(
                shop.Id,
                query.StatusFilter,
                query.TrackingCodeSearch,
                query.ReceiverNameSearch,
                query.ReceiverPhoneSearch,
                query.FromUtc,
                query.ToUtc,
                query.MinCodAmount,
                query.MaxCodAmount,
                query.SortBy,
                query.SortDirection,
                query.PageNumber,
                query.PageSize),
            cancellationToken);
        var response = shipments.Items
            .Select(shipment => ToResponse(shipment, canViewFullPii, canViewCod))
            .ToList();

        return Result<PagedResponse<ShipmentListItemResponse>>.Success(
            new PagedResponse<ShipmentListItemResponse>(
                response,
                shipments.PageNumber,
                shipments.PageSize,
                shipments.TotalCount));
    }

    private ShipmentListItemResponse ToResponse(
        Shipment shipment,
        bool canViewFullPii,
        bool canViewCod)
    {
        return new ShipmentListItemResponse(
            shipment.Id,
            shipment.TrackingCode.Value,
            canViewFullPii ? shipment.ReceiverName : _piiMaskingService.MaskName(shipment.ReceiverName),
            shipment.RouteType,
            shipment.Weight.Kilograms,
            shipment.ChargeableWeight.Kilograms,
            canViewCod ? shipment.CodAmount.Amount : 0m,
            shipment.ShippingFee.Amount,
            shipment.ShippingFee.Currency,
            shipment.Status,
            shipment.CreatedAtUtc);
    }
}
