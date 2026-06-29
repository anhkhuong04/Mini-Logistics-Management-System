namespace MiniLogistics.Application.PartnerApi;

public sealed record PartnerGetShipmentCommand(
    Guid ApiClientId,
    Guid ShopId,
    string TrackingCode);
