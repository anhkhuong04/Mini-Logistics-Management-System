using MiniLogistics.Application.Common;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.GetPendingPickupShipments;

public sealed class GetPendingPickupShipmentsService : IGetPendingPickupShipmentsService
{
    private static readonly TimeSpan PendingPickupSla = TimeSpan.FromHours(4);

    private readonly IShipmentReadRepository _shipmentRepository;
    private readonly TimeProvider _timeProvider;

    public GetPendingPickupShipmentsService(
        IShipmentReadRepository shipmentRepository,
        TimeProvider timeProvider)
    {
        _shipmentRepository = shipmentRepository;
        _timeProvider = timeProvider;
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
        var now = _timeProvider.GetUtcNow();
        var page = await _shipmentRepository.SearchPendingPickupAsync(
            new PendingPickupShipmentSearchCriteria(
                query.SearchText,
                query.Province,
                query.FromUtc,
                query.ToUtc,
                query.MinCodAmount,
                query.MaxCodAmount,
                query.SlaOnly ? now.Subtract(PendingPickupSla) : null,
                query.PageNumber,
                query.PageSize),
            cancellationToken);

        var response = page.Items
            .Select(shipment => ToResponse(shipment, now))
            .ToList();

        return Result<PagedResponse<GetPendingPickupShipmentResponse>>.Success(
            new PagedResponse<GetPendingPickupShipmentResponse>(
                response,
                page.PageNumber,
                page.PageSize,
                page.TotalCount));
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

}
