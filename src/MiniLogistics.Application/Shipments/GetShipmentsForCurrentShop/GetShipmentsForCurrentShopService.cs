using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.GetShipmentsForCurrentShop;

public sealed class GetShipmentsForCurrentShopService : IGetShipmentsForCurrentShopService
{
    private readonly IShopRepository _shopRepository;
    private readonly IShipmentRepository _shipmentRepository;

    public GetShipmentsForCurrentShopService(
        IShopRepository shopRepository,
        IShipmentRepository shipmentRepository)
    {
        _shopRepository = shopRepository;
        _shipmentRepository = shipmentRepository;
    }

    public async Task<Result<IReadOnlyList<ShipmentListItemResponse>>> GetAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        if (ownerUserId == Guid.Empty)
        {
            return Result<IReadOnlyList<ShipmentListItemResponse>>.Failure(
                ApplicationErrors.ValidationFailed("Current user id is required."));
        }

        var shop = await _shopRepository.GetByOwnerUserIdAsync(ownerUserId, cancellationToken);
        if (shop is null)
        {
            return Result<IReadOnlyList<ShipmentListItemResponse>>.Failure(
                ApplicationErrors.NotFound("Shop was not found for current user."));
        }

        if (!shop.IsActive)
        {
            return Result<IReadOnlyList<ShipmentListItemResponse>>.Failure(
                ApplicationErrors.Forbidden("Shop account is not active."));
        }

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
