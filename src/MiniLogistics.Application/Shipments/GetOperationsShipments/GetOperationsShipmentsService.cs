using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.Shipments.GetOperationsShipments;

public sealed class GetOperationsShipmentsService : IGetOperationsShipmentsService
{
    private static readonly ShipmentStatus[] VisibleStatuses =
    [
        ShipmentStatus.Assigned,
        ShipmentStatus.PickingUp,
        ShipmentStatus.PickedUp,
        ShipmentStatus.InTransit,
        ShipmentStatus.Delivering,
        ShipmentStatus.DeliveryFailed,
        ShipmentStatus.Delivered
    ];

    private readonly IShipmentRepository _shipmentRepository;
    private readonly ICodTransactionRepository _codTransactionRepository;
    private readonly IIdentityService _identityService;

    public GetOperationsShipmentsService(
        IShipmentRepository shipmentRepository,
        ICodTransactionRepository codTransactionRepository,
        IIdentityService identityService)
    {
        _shipmentRepository = shipmentRepository;
        _codTransactionRepository = codTransactionRepository;
        _identityService = identityService;
    }

    public async Task<Result<IReadOnlyList<GetOperationsShipmentResponse>>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var shipments = await _shipmentRepository.GetByStatusesAsync(
            VisibleStatuses,
            cancellationToken);

        var activeShipperIds = shipments
            .Select(shipment => shipment.Assignments.FirstOrDefault(assignment => assignment.IsActive)?.ShipperId)
            .OfType<Guid>()
            .Distinct()
            .ToList();

        var users = await _identityService.GetUsersByIdsAsync(activeShipperIds, cancellationToken);
        var userById = users.ToDictionary(user => user.UserId);

        var response = new List<GetOperationsShipmentResponse>();

        foreach (var shipment in shipments)
        {
            var codTransaction = await _codTransactionRepository.GetByShipmentIdAsync(
                shipment.Id,
                cancellationToken);
            var codStatus = codTransaction?.Status ?? CodStatus.NotRequired;

            if (shipment.Status == ShipmentStatus.Delivered && codStatus != CodStatus.PendingCollection)
            {
                continue;
            }

            var activeShipperId = shipment.Assignments.FirstOrDefault(assignment => assignment.IsActive)?.ShipperId;
            var activeShipper = activeShipperId.HasValue && userById.TryGetValue(activeShipperId.Value, out var user)
                ? user
                : null;

            var trackingHistory = await ShipmentStatusHistoryMapper.ToResponseAsync(
                shipment.StatusHistory,
                _identityService,
                cancellationToken);

            response.Add(ToResponse(shipment, codStatus, activeShipperId, activeShipper, trackingHistory));
        }

        return Result<IReadOnlyList<GetOperationsShipmentResponse>>.Success(response);
    }

    private static GetOperationsShipmentResponse ToResponse(
        Shipment shipment,
        CodStatus codStatus,
        Guid? activeShipperId,
        IdentityUserSummaryResponse? activeShipper,
        IReadOnlyList<ShipmentStatusHistoryResponse> trackingHistory)
    {
        return new GetOperationsShipmentResponse(
            shipment.Id,
            shipment.TrackingCode.Value,
            shipment.ReceiverName,
            shipment.ReceiverPhone.Value,
            ToAddressResponse(shipment.PickupAddress),
            ToAddressResponse(shipment.DeliveryAddress),
            shipment.CodAmount.Amount,
            codStatus,
            shipment.ShippingFee.Amount,
            shipment.ShippingFee.Currency,
            shipment.Status,
            shipment.CreatedAtUtc,
            activeShipperId,
            activeShipper?.FullName,
            activeShipper?.PhoneNumber,
            trackingHistory);
    }

    private static ShipmentAddressResponse ToAddressResponse(Address address)
    {
        return new ShipmentAddressResponse(
            address.Street,
            address.Ward,
            address.Province,
            address.Country,
            address.FullAddress);
    }
}
