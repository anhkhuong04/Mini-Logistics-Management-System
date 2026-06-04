using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.GetPendingPickupShipments;

public sealed class GetPendingPickupShipmentsService : IGetPendingPickupShipmentsService
{
    private readonly IShipmentRepository _shipmentRepository;

    public GetPendingPickupShipmentsService(IShipmentRepository shipmentRepository)
    {
        _shipmentRepository = shipmentRepository;
    }

    public async Task<Result<IReadOnlyList<GetPendingPickupShipmentResponse>>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var shipments = await _shipmentRepository.GetByStatusAsync(
            ShipmentStatus.PendingPickup,
            cancellationToken);

        var response = shipments
            .Select(shipment => new GetPendingPickupShipmentResponse(
                shipment.Id,
                shipment.TrackingCode.Value,
                shipment.ReceiverName,
                shipment.PickupAddress.Province,
                shipment.DeliveryAddress.Province,
                shipment.CodAmount.Amount,
                shipment.ShippingFee.Amount,
                shipment.ShippingFee.Currency,
                shipment.CreatedAtUtc))
            .ToList();

        return Result<IReadOnlyList<GetPendingPickupShipmentResponse>>.Success(response);
    }
}
