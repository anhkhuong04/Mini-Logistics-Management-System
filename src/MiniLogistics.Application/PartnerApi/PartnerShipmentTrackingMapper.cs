using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Application.Shipments;

namespace MiniLogistics.Application.PartnerApi;

internal static class PartnerShipmentTrackingMapper
{
    public static PartnerShipmentTrackingResponse ToResponse(
        Shipment shipment,
        ExternalShipmentReference? reference,
        CodTransaction? codTransaction)
    {
        var timeline = shipment.StatusHistory
            .OrderBy(history => history.ChangedAtUtc)
            .Select(history =>
            {
                var publicMessage = ShipmentPublicStatusMessageMapper.FromHistory(
                    history.Status,
                    history.FailureReasonCode);
                return new PartnerShipmentTimelineItem(
                    history.Status,
                    publicMessage.MessageCode,
                    publicMessage.Message,
                    publicMessage.Locale,
                    history.ChangedAtUtc);
            })
            .ToList();

        return new PartnerShipmentTrackingResponse(
            shipment.TrackingCode.Value,
            reference?.ExternalOrderId,
            shipment.Status,
            codTransaction?.Status ?? CodStatus.NotRequired,
            shipment.ShippingFee.Amount,
            shipment.ShippingFee.Currency,
            timeline);
    }
}
