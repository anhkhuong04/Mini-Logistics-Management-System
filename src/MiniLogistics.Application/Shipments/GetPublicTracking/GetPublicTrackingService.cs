using MiniLogistics.Application.Common;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.GetPublicTracking;

public sealed class GetPublicTrackingService : IGetPublicTrackingService
{
    private readonly IShipmentRepository _shipmentRepository;

    public GetPublicTrackingService(IShipmentRepository shipmentRepository)
    {
        _shipmentRepository = shipmentRepository;
    }

    public async Task<Result<PublicTrackingResponse>> GetAsync(
        string trackingCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(trackingCode))
        {
            return Result<PublicTrackingResponse>.Failure(
                ApplicationErrors.ValidationFailed("Tracking code is required."));
        }

        var shipment = await _shipmentRepository.GetByTrackingCodeAsync(
            new TrackingCode(trackingCode),
            cancellationToken);

        if (shipment is null)
        {
            return Result<PublicTrackingResponse>.Failure(
                ApplicationErrors.NotFound("Shipment was not found for tracking code."));
        }

        var timeline = shipment.StatusHistory
            .OrderBy(history => history.ChangedAtUtc)
            .Select(history => new ShipmentStatusHistoryResponse(
                history.Status,
                history.Note,
                history.ChangedAtUtc))
            .ToList();

        return Result<PublicTrackingResponse>.Success(new PublicTrackingResponse(
            shipment.TrackingCode.Value,
            shipment.Status,
            shipment.CreatedAtUtc,
            timeline));
    }
}
