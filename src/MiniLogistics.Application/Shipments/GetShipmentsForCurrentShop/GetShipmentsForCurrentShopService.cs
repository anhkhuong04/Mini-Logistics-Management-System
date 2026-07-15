using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.GetShipmentsForCurrentShop;

public sealed class GetShipmentsForCurrentShopService : IGetShipmentsForCurrentShopService
{
    private readonly IShopAccessService _shopAccessService;
    private readonly IShipmentRepository _shipmentRepository;

    public GetShipmentsForCurrentShopService(
        IShopAccessService shopAccessService,
        IShipmentRepository shipmentRepository)
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
            .Select(shipment => new ShipmentListItemResponse(
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
                shipment.CreatedAtUtc))
            .ToList();

        return Result<IReadOnlyList<ShipmentListItemResponse>>.Success(response);
    }
}
