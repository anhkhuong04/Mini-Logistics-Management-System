using MiniLogistics.Application.Common;
using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Users;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.Shipments.GetAssignedShipmentsForShipper;

public sealed class GetAssignedShipmentsForShipperService : IGetAssignedShipmentsForShipperService
{
    private readonly IIdentityService _identityService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly ICodTransactionRepository _codTransactionRepository;

    public GetAssignedShipmentsForShipperService(
        IIdentityService identityService,
        IShipmentRepository shipmentRepository,
        ICodTransactionRepository codTransactionRepository)
    {
        _identityService = identityService;
        _shipmentRepository = shipmentRepository;
        _codTransactionRepository = codTransactionRepository;
    }

    public async Task<Result<IReadOnlyList<GetAssignedShipmentForShipperResponse>>> GetAsync(
        Guid shipperUserId,
        CancellationToken cancellationToken = default)
    {
        if (shipperUserId == Guid.Empty)
        {
            return Result<IReadOnlyList<GetAssignedShipmentForShipperResponse>>.Failure(
                ApplicationErrors.ValidationFailed("Shipper user id is required."));
        }

        var shipperCheck = await _identityService.CheckUserRoleAsync(
            shipperUserId,
            nameof(UserRole.Shipper),
            cancellationToken);

        if (!shipperCheck.Exists)
        {
            return Result<IReadOnlyList<GetAssignedShipmentForShipperResponse>>.Failure(
                ApplicationErrors.NotFound("Shipper was not found."));
        }

        if (!shipperCheck.IsActive)
        {
            return Result<IReadOnlyList<GetAssignedShipmentForShipperResponse>>.Failure(
                ApplicationErrors.Forbidden("Shipper is not active."));
        }

        if (!shipperCheck.IsInRole)
        {
            return Result<IReadOnlyList<GetAssignedShipmentForShipperResponse>>.Failure(
                ApplicationErrors.Forbidden("Current user is not a shipper."));
        }

        var shipments = await _shipmentRepository.GetAssignedToShipperAsync(
            shipperUserId,
            cancellationToken);

        var response = new List<GetAssignedShipmentForShipperResponse>(shipments.Count);

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

            response.Add(ToResponse(shipment, codStatus));
        }

        return Result<IReadOnlyList<GetAssignedShipmentForShipperResponse>>.Success(response);
    }

    private static GetAssignedShipmentForShipperResponse ToResponse(
        Shipment shipment,
        CodStatus codStatus)
    {
        return new GetAssignedShipmentForShipperResponse(
            shipment.Id,
            shipment.TrackingCode.Value,
            shipment.SenderName,
            shipment.SenderPhone.Value,
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
            shipment.StatusHistory
                .OrderBy(history => history.ChangedAtUtc)
                .Select(history => new ShipmentStatusHistoryResponse(
                    history.Status,
                    history.Note,
                    history.ChangedAtUtc))
                .ToList());
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
