using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.Shipments.GetOperationsShipments;

public sealed class GetOperationsShipmentsService : IGetOperationsShipmentsService
{
    private static readonly TimeSpan CodPendingCollectionSla = TimeSpan.FromHours(24);

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

    private readonly IShipmentReadRepository _shipmentRepository;
    private readonly ICodTransactionRepository _codTransactionRepository;
    private readonly IIdentityService _identityService;
    private readonly TimeProvider _timeProvider;

    public GetOperationsShipmentsService(
        IShipmentReadRepository shipmentRepository,
        ICodTransactionRepository codTransactionRepository,
        IIdentityService identityService,
        TimeProvider timeProvider)
    {
        _shipmentRepository = shipmentRepository;
        _codTransactionRepository = codTransactionRepository;
        _identityService = identityService;
        _timeProvider = timeProvider;
    }

    public async Task<Result<IReadOnlyList<GetOperationsShipmentResponse>>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await SearchAsync(
            new GetOperationsShipmentsQuery(PageNumber: 1, PageSize: int.MaxValue),
            cancellationToken);

        return result.IsSuccess
            ? Result<IReadOnlyList<GetOperationsShipmentResponse>>.Success(result.Value.Items)
            : Result<IReadOnlyList<GetOperationsShipmentResponse>>.Failure(result.Error);
    }

    public async Task<Result<PagedResponse<GetOperationsShipmentResponse>>> SearchAsync(
        GetOperationsShipmentsQuery query,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<ShipmentStatus> statuses = query.Status.HasValue
            ? [query.Status.Value]
            : VisibleStatuses;
        var now = _timeProvider.GetUtcNow();
        var page = await _shipmentRepository.SearchOperationsAsync(
            new OperationsShipmentSearchCriteria(
                statuses,
                query.SearchText,
                query.CodStatus,
                query.ShipperId,
                query.Province,
                query.FromUtc,
                query.ToUtc,
                query.MinCodAmount,
                query.MaxCodAmount,
                query.SlaOnly,
                now,
                query.PageNumber,
                query.PageSize),
            cancellationToken);
        var shipments = page.Items;

        var activeShipperIds = shipments
            .Select(shipment => shipment.Assignments.FirstOrDefault(assignment => assignment.IsActive)?.ShipperId)
            .OfType<Guid>()
            .Distinct()
            .ToList();

        var users = await _identityService.GetUsersByIdsAsync(activeShipperIds, cancellationToken);
        var userById = users.ToDictionary(user => user.UserId);
        var codTransactionsByShipmentId = await _codTransactionRepository.GetByShipmentIdsAsync(
            shipments.Select(shipment => shipment.Id).ToList(),
            cancellationToken);

        var response = new List<GetOperationsShipmentResponse>();

        foreach (var shipment in shipments)
        {
            codTransactionsByShipmentId.TryGetValue(shipment.Id, out var codTransaction);
            var codStatus = codTransaction?.Status ?? CodStatus.NotRequired;

            if (shipment.Status == ShipmentStatus.Delivered && codStatus != CodStatus.PendingCollection)
            {
                continue;
            }

            if (query.CodStatus.HasValue && codStatus != query.CodStatus.Value)
            {
                continue;
            }

            if (query.SlaOnly && !IsSlaOverdue(shipment, codStatus, now))
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

            response.Add(ToResponse(
                shipment,
                codStatus,
                activeShipperId,
                activeShipper,
                trackingHistory,
                IsSlaOverdue(shipment, codStatus, now)));
        }

        return Result<PagedResponse<GetOperationsShipmentResponse>>.Success(
            new PagedResponse<GetOperationsShipmentResponse>(
                response,
                page.PageNumber,
                page.PageSize,
                page.TotalCount));
    }

    private static GetOperationsShipmentResponse ToResponse(
        Shipment shipment,
        CodStatus codStatus,
        Guid? activeShipperId,
        IdentityUserSummaryResponse? activeShipper,
        IReadOnlyList<ShipmentStatusHistoryResponse> trackingHistory,
        bool isSlaOverdue)
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
            trackingHistory,
            isSlaOverdue);
    }

    private static bool IsSlaOverdue(
        Shipment shipment,
        CodStatus codStatus,
        DateTimeOffset now)
    {
        if (shipment.Status == ShipmentStatus.DeliveryFailed
            && shipment.StatusHistory.Count(history => history.Status == ShipmentStatus.DeliveryFailed) > 1)
        {
            return true;
        }

        if (shipment.Status == ShipmentStatus.Delivered && codStatus == CodStatus.PendingCollection)
        {
            var deliveredAtUtc = shipment.StatusHistory
                .Where(history => history.Status == ShipmentStatus.Delivered)
                .OrderByDescending(history => history.ChangedAtUtc)
                .Select(history => history.ChangedAtUtc)
                .FirstOrDefault();
            var referenceDate = deliveredAtUtc == default ? shipment.CreatedAtUtc : deliveredAtUtc;

            return now - referenceDate >= CodPendingCollectionSla;
        }

        return false;
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
