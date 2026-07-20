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
    private readonly IShipmentReadRepository _shipmentRepository;
    private readonly ICodTransactionRepository _codTransactionRepository;

    public GetAssignedShipmentsForShipperService(
        IIdentityService identityService,
        IShipmentReadRepository shipmentRepository,
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
        var result = await SearchAsync(
            shipperUserId,
            new GetAssignedShipmentsForShipperQuery(PageNumber: 1, PageSize: int.MaxValue),
            cancellationToken);

        return result.IsSuccess
            ? Result<IReadOnlyList<GetAssignedShipmentForShipperResponse>>.Success(result.Value.Items)
            : Result<IReadOnlyList<GetAssignedShipmentForShipperResponse>>.Failure(result.Error);
    }

    public async Task<Result<PagedResponse<GetAssignedShipmentForShipperResponse>>> SearchAsync(
        Guid shipperUserId,
        GetAssignedShipmentsForShipperQuery query,
        CancellationToken cancellationToken = default)
    {
        if (shipperUserId == Guid.Empty)
        {
            return Result<PagedResponse<GetAssignedShipmentForShipperResponse>>.Failure(
                ApplicationErrors.ValidationFailed("Shipper user id is required."));
        }

        var shipperCheck = await _identityService.CheckUserRoleAsync(
            shipperUserId,
            nameof(UserRole.Shipper),
            cancellationToken);

        if (!shipperCheck.Exists)
        {
            return Result<PagedResponse<GetAssignedShipmentForShipperResponse>>.Failure(
                ApplicationErrors.NotFound("Shipper was not found."));
        }

        if (!shipperCheck.IsActive)
        {
            return Result<PagedResponse<GetAssignedShipmentForShipperResponse>>.Failure(
                ApplicationErrors.Forbidden("Shipper is not active."));
        }

        if (!shipperCheck.IsInRole)
        {
            return Result<PagedResponse<GetAssignedShipmentForShipperResponse>>.Failure(
                ApplicationErrors.Forbidden("Current user is not a shipper."));
        }

        var page = await _shipmentRepository.SearchAssignedToShipperAsync(
            new AssignedShipmentsForShipperSearchCriteria(
                shipperUserId,
                ShipperWorkspaceStageMapping.ToStatuses(query.Stage),
                query.SearchText,
                query.Stage == ShipperWorkspaceStage.CodPending
                    ? CodStatus.PendingCollection
                    : query.CodStatus,
                query.PageNumber,
                query.PageSize),
            cancellationToken);
        var shipments = page.Items;
        var codTransactionsByShipmentId = await _codTransactionRepository.GetByShipmentIdsAsync(
            shipments.Select(shipment => shipment.Id).ToList(),
            cancellationToken);

        var response = new List<GetAssignedShipmentForShipperResponse>(shipments.Count);

        foreach (var shipment in shipments)
        {
            codTransactionsByShipmentId.TryGetValue(shipment.Id, out var codTransaction);
            var codStatus = codTransaction?.Status ?? CodStatus.NotRequired;

            if (shipment.Status == ShipmentStatus.Delivered && codStatus != CodStatus.PendingCollection)
            {
                continue;
            }

            var trackingHistory = await ShipmentStatusHistoryMapper.ToResponseAsync(
                shipment.StatusHistory,
                _identityService,
                cancellationToken);

            response.Add(ToResponse(shipment, codStatus, trackingHistory));
        }

        return Result<PagedResponse<GetAssignedShipmentForShipperResponse>>.Success(
            new PagedResponse<GetAssignedShipmentForShipperResponse>(
                response,
                page.PageNumber,
                page.PageSize,
                page.TotalCount));
    }

    private static GetAssignedShipmentForShipperResponse ToResponse(
        Shipment shipment,
        CodStatus codStatus,
        IReadOnlyList<ShipmentStatusHistoryResponse> trackingHistory)
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
