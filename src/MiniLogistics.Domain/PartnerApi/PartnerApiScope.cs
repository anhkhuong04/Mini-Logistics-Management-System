namespace MiniLogistics.Domain.PartnerApi;

[Flags]
public enum PartnerApiScope
{
    None = 0,
    Quote = 1,
    CreateShipment = 2,
    TrackShipment = 4,
    CancelShipment = 8,
    WebhookManage = 16,
    All = Quote | CreateShipment | TrackShipment | CancelShipment | WebhookManage
}
