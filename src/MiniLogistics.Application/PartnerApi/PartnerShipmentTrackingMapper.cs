using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.PartnerApi;

internal static class PartnerShipmentTrackingMapper
{
    public static PartnerShipmentTrackingResponse ToResponse(
        Shipment shipment,
        ExternalShipmentReference reference,
        CodTransaction? codTransaction)
    {
        var timeline = shipment.StatusHistory
            .OrderBy(history => history.ChangedAtUtc)
            .Select(history => new PartnerShipmentTimelineItem(
                history.Status,
                history.Note,
                history.ChangedAtUtc))
            .ToList();

        return new PartnerShipmentTrackingResponse(
            shipment.TrackingCode.Value,
            reference.ExternalOrderId,
            shipment.Status,
            codTransaction?.Status ?? CodStatus.NotRequired,
            shipment.ShippingFee.Amount,
            shipment.ShippingFee.Currency,
            timeline);
    }
}
