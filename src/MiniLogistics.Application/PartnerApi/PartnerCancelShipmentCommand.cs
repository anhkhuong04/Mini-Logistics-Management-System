namespace MiniLogistics.Application.PartnerApi;

public sealed record PartnerCancelShipmentCommand(
    Guid ApiClientId,
    Guid ShopId,
    string TrackingCode,
    string Reason);
