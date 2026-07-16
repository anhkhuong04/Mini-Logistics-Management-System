namespace MiniLogistics.Application.AdminAuditing;

public static class AdminAuditTargetTypes
{
    public const string User = "User";
    public const string Shipper = "Shipper";
    public const string Hub = "Hub";
    public const string Shop = "Shop";
    public const string Shipment = "Shipment";
    public const string CodTransaction = "CodTransaction";
    public const string PartnerApiClient = "PartnerApiClient";
    public const string PartnerWebhookEndpoint = "PartnerWebhookEndpoint";
    public const string WebhookDelivery = "WebhookDelivery";
    public const string IntegrationManagementScope = "IntegrationManagementScope";
    public const string RouteRegionConfig = "RouteRegionConfig";
    public const string FeeRule = "FeeRule";
}
