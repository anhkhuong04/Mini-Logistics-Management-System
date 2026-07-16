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
        var shipments = await _shipmentRepository.GetByStatusesAsync(
            statuses,
            cancellationToken);

        var activeShipperIds = shipments
            .Select(shipment => shipment.Assignments.FirstOrDefault(assignment => assignment.IsActive)?.ShipperId)
            .OfType<Guid>()
            .Distinct()
            .ToList();

        var users = await _identityService.GetUsersByIdsAsync(activeShipperIds, cancellationToken);
        var userById = users.ToDictionary(user => user.UserId);

        var now = DateTimeOffset.UtcNow;
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

            if (!Matches(query, shipment, codStatus, now))
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

        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = query.PageSize == int.MaxValue
            ? int.MaxValue
            : Math.Clamp(query.PageSize, 1, 100);
        var items = response
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Result<PagedResponse<GetOperationsShipmentResponse>>.Success(
            new PagedResponse<GetOperationsShipmentResponse>(
                items,
                pageNumber,
                pageSize,
                response.Count));
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

    private static bool Matches(
        GetOperationsShipmentsQuery query,
        Shipment shipment,
        CodStatus codStatus,
        DateTimeOffset now)
    {
        if (query.CodStatus.HasValue && codStatus != query.CodStatus.Value)
        {
            return false;
        }

        if (query.ShipperId.HasValue
            && shipment.Assignments.FirstOrDefault(assignment => assignment.IsActive)?.ShipperId != query.ShipperId.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var keyword = query.SearchText.Trim();
            var matchesKeyword = shipment.TrackingCode.Value.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || shipment.ReceiverName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || shipment.ReceiverPhone.Value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
            if (!matchesKeyword)
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Province))
        {
            var province = query.Province.Trim();
            var matchesProvince = string.Equals(shipment.PickupAddress.Province, province, StringComparison.OrdinalIgnoreCase)
                || string.Equals(shipment.DeliveryAddress.Province, province, StringComparison.OrdinalIgnoreCase);
            if (!matchesProvince)
            {
                return false;
            }
        }

        if (query.FromUtc.HasValue && shipment.CreatedAtUtc < query.FromUtc.Value)
        {
            return false;
        }

        if (query.ToUtc.HasValue && shipment.CreatedAtUtc > query.ToUtc.Value)
        {
            return false;
        }

        if (query.MinCodAmount.HasValue && shipment.CodAmount.Amount < query.MinCodAmount.Value)
        {
            return false;
        }

        if (query.MaxCodAmount.HasValue && shipment.CodAmount.Amount > query.MaxCodAmount.Value)
        {
            return false;
        }

        if (query.SlaOnly && !IsSlaOverdue(shipment, codStatus, now))
        {
            return false;
        }

        return true;
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
