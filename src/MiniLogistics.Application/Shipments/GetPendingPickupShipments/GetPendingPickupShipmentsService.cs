using MiniLogistics.Application.Common;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.GetPendingPickupShipments;

public sealed class GetPendingPickupShipmentsService : IGetPendingPickupShipmentsService
{
    private static readonly TimeSpan PendingPickupSla = TimeSpan.FromHours(4);

    private readonly IShipmentRepository _shipmentRepository;

    public GetPendingPickupShipmentsService(IShipmentRepository shipmentRepository)
    {
        _shipmentRepository = shipmentRepository;
    }

    public async Task<Result<IReadOnlyList<GetPendingPickupShipmentResponse>>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await SearchAsync(
            new GetPendingPickupShipmentsQuery(PageNumber: 1, PageSize: int.MaxValue),
            cancellationToken);

        return result.IsSuccess
            ? Result<IReadOnlyList<GetPendingPickupShipmentResponse>>.Success(result.Value.Items)
            : Result<IReadOnlyList<GetPendingPickupShipmentResponse>>.Failure(result.Error);
    }

    public async Task<Result<PagedResponse<GetPendingPickupShipmentResponse>>> SearchAsync(
        GetPendingPickupShipmentsQuery query,
        CancellationToken cancellationToken = default)
    {
        var shipments = await _shipmentRepository.GetByStatusAsync(
            ShipmentStatus.PendingPickup,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var response = shipments
            .Where(shipment => Matches(query, shipment, now))
            .Select(shipment => ToResponse(shipment, now))
            .ToList();

        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = query.PageSize == int.MaxValue
            ? int.MaxValue
            : Math.Clamp(query.PageSize, 1, 100);
        var items = response
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Result<PagedResponse<GetPendingPickupShipmentResponse>>.Success(
            new PagedResponse<GetPendingPickupShipmentResponse>(
                items,
                pageNumber,
                pageSize,
                response.Count));
    }

    private static GetPendingPickupShipmentResponse ToResponse(Shipment shipment, DateTimeOffset now)
    {
        return new GetPendingPickupShipmentResponse(
                shipment.Id,
                shipment.TrackingCode.Value,
                shipment.ReceiverName,
                shipment.PickupAddress.Province,
                shipment.DeliveryAddress.Province,
                shipment.CodAmount.Amount,
                shipment.ShippingFee.Amount,
                shipment.ShippingFee.Currency,
                shipment.CreatedAtUtc,
                now - shipment.CreatedAtUtc >= PendingPickupSla);
    }

    private static bool Matches(
        GetPendingPickupShipmentsQuery query,
        Shipment shipment,
        DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var keyword = query.SearchText.Trim();
            var matchesKeyword = shipment.TrackingCode.Value.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || shipment.ReceiverName.Contains(keyword, StringComparison.OrdinalIgnoreCase);
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

        if (query.SlaOnly && now - shipment.CreatedAtUtc < PendingPickupSla)
        {
            return false;
        }

        return true;
    }
}
