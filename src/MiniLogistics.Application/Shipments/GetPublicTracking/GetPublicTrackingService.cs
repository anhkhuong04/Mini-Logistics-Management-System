using MiniLogistics.Application.Common;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.Shipments.GetPublicTracking;

public sealed class GetPublicTrackingService : IGetPublicTrackingService
{
    private readonly IShipmentReadRepository _shipmentRepository;

    public GetPublicTrackingService(IShipmentReadRepository shipmentRepository)
    {
        _shipmentRepository = shipmentRepository;
    }

    public async Task<Result<PublicTrackingResponse>> GetAsync(
        string trackingCode,
        string? phoneLast4 = null,
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

        if (shipment is null || shipment.Status == ShipmentStatus.Draft)
        {
            return Result<PublicTrackingResponse>.Failure(
                ApplicationErrors.NotFound("Shipment was not found for tracking code."));
        }

        var normalizedPhoneLast4 = phoneLast4?.Trim();
        if (!string.IsNullOrEmpty(normalizedPhoneLast4) && !IsPhoneLast4(normalizedPhoneLast4))
        {
            return Result<PublicTrackingResponse>.Failure(
                ApplicationErrors.ValidationFailed("Phone last 4 must contain exactly 4 digits."));
        }

        var accessLevel = MatchesPhoneLast4(shipment, normalizedPhoneLast4)
            ? PublicTrackingAccessLevel.Verified
            : PublicTrackingAccessLevel.Summary;

        return Result<PublicTrackingResponse>.Success(MapResponse(shipment, accessLevel));
    }

    private static PublicTrackingResponse MapResponse(
        Shipment shipment,
        PublicTrackingAccessLevel accessLevel)
    {
        var isVerified = accessLevel == PublicTrackingAccessLevel.Verified;
        var timeline = shipment.StatusHistory
            .OrderBy(history => history.ChangedAtUtc)
            .Select(history => new PublicTrackingTimelineItemResponse(
                history.Status,
                history.ChangedAtUtc))
            .ToList();
        var lastUpdatedAtUtc = timeline.Count > 0
            ? timeline[^1].ChangedAtUtc
            : shipment.UpdatedAtUtc ?? shipment.CreatedAtUtc;

        return new PublicTrackingResponse(
            shipment.TrackingCode.Value,
            accessLevel,
            shipment.Status,
            shipment.PickupAddress.Province,
            shipment.DeliveryAddress.Province,
            shipment.CreatedAtUtc,
            shipment.UpdatedAtUtc ?? lastUpdatedAtUtc,
            isVerified ? shipment.SenderName : MaskName(shipment.SenderName),
            isVerified ? shipment.SenderPhone.Value : null,
            isVerified ? shipment.ReceiverName : MaskName(shipment.ReceiverName),
            isVerified ? shipment.ReceiverPhone.Value : null,
            isVerified ? ToAddressResponse(shipment.PickupAddress) : null,
            isVerified ? ToAddressResponse(shipment.DeliveryAddress) : null,
            timeline);
    }

    private static bool MatchesPhoneLast4(Shipment shipment, string? phoneLast4)
    {
        return !string.IsNullOrEmpty(phoneLast4)
            && (EndsWithLast4(shipment.ReceiverPhone.Value, phoneLast4)
                || EndsWithLast4(shipment.SenderPhone.Value, phoneLast4));
    }

    private static bool EndsWithLast4(string phoneNumber, string phoneLast4)
    {
        var digits = ExtractDigits(phoneNumber);
        return digits.Length >= 4 && digits.EndsWith(phoneLast4, StringComparison.Ordinal);
    }

    private static bool IsPhoneLast4(string value)
    {
        return value.Length == 4 && value.All(char.IsDigit);
    }

    private static string ExtractDigits(string value)
    {
        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static string MaskName(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return trimmed.Length == 1
            ? "*"
            : $"{trimmed[0]}***";
    }

    private static PublicTrackingAddressResponse ToAddressResponse(Address address)
    {
        return new PublicTrackingAddressResponse(
            address.Street,
            address.Ward,
            address.Province,
            address.Country,
            address.FullAddress);
    }
}
