using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.GetPublicTracking;

public sealed class GetPublicTrackingService : IGetPublicTrackingService
{
    private readonly IIdentityService _identityService;
    private readonly IShipmentRepository _shipmentRepository;

    public GetPublicTrackingService(
        IIdentityService identityService,
        IShipmentRepository shipmentRepository)
    {
        _identityService = identityService;
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

        var timeline = await ShipmentStatusHistoryMapper.ToResponseAsync(
            shipment.StatusHistory,
            _identityService,
            cancellationToken);

        return Result<PublicTrackingResponse>.Success(new PublicTrackingResponse(
            shipment.TrackingCode.Value,
            shipment.Status,
            shipment.CreatedAtUtc,
            timeline));
    }
}
