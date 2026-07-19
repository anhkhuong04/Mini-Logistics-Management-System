using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.GetShipmentsForCurrentShop;

public sealed class GetShipmentsForCurrentShopService : IGetShipmentsForCurrentShopService
{
    private readonly IShopAccessService _shopAccessService;
    private readonly IShipmentReadRepository _shipmentRepository;

    public GetShipmentsForCurrentShopService(
        IShopAccessService shopAccessService,
        IShipmentReadRepository shipmentRepository)
    {
        _shopAccessService = shopAccessService;
        _shipmentRepository = shipmentRepository;
    }

    public async Task<Result<IReadOnlyList<ShipmentListItemResponse>>> GetAsync(
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
            return Result<IReadOnlyList<ShipmentListItemResponse>>.Failure(shopResult.Error);
        }

        var shop = shopResult.Value;
        var shipments = await _shipmentRepository.GetByShopIdAsync(shop.Id, cancellationToken);
        var response = shipments
            .Select(ToResponse)
            .ToList();

        return Result<IReadOnlyList<ShipmentListItemResponse>>.Success(response);
    }

    public async Task<Result<PagedResponse<ShipmentListItemResponse>>> SearchAsync(
        GetShipmentsForCurrentShopQuery query,
        CancellationToken cancellationToken = default)
    {
        var shopResult = await _shopAccessService.GetShopForUserAsync(
            query.OwnerUserId,
            query.ShopId,
            requireActiveShop: false,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<PagedResponse<ShipmentListItemResponse>>.Failure(shopResult.Error);
        }

        var shop = shopResult.Value;
        var shipments = await _shipmentRepository.GetByShopIdPagedAsync(
            shop.Id,
            query.PageNumber,
            query.PageSize,
            query.StatusFilter,
            query.TrackingCodeSearch,
            cancellationToken);
        var response = shipments.Items
            .Select(ToResponse)
            .ToList();

        return Result<PagedResponse<ShipmentListItemResponse>>.Success(
            new PagedResponse<ShipmentListItemResponse>(
                response,
                shipments.PageNumber,
                shipments.PageSize,
                shipments.TotalCount));
    }

    private static ShipmentListItemResponse ToResponse(Shipment shipment)
    {
        return new ShipmentListItemResponse(
            shipment.Id,
            shipment.TrackingCode.Value,
            shipment.ReceiverName,
            shipment.RouteType,
            shipment.Weight.Kilograms,
            shipment.ChargeableWeight.Kilograms,
            shipment.CodAmount.Amount,
            shipment.ShippingFee.Amount,
            shipment.ShippingFee.Currency,
            shipment.Status,
            shipment.CreatedAtUtc);
    }
}
