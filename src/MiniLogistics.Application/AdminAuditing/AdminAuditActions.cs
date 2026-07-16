namespace MiniLogistics.Application.AdminAuditing;

public static class AdminAuditActions
{
    public const string InternalUserCreated = "internal_user.created";
    public const string UserActiveStatusChanged = "user.active_status_changed";
    public const string ShipperCapacityChanged = "shipper.capacity_changed";
    public const string ShipperWorkingAreasChanged = "shipper.working_areas_changed";
    public const string HubCreated = "hub.created";
    public const string HubUpdated = "hub.updated";
    public const string HubActiveStatusChanged = "hub.active_status_changed";
    public const string ShopActiveStatusChanged = "shop.active_status_changed";
    public const string ShipmentManualAssigned = "shipment.manual_assigned";
    public const string ShipmentReassigned = "shipment.reassigned";
    public const string ShipmentAssignmentCancelled = "shipment.assignment_cancelled";
    public const string ShipmentAutoAssignmentRetried = "shipment.auto_assignment_retried";
    public const string ShipmentStatusChanged = "shipment.status_changed";
    public const string CodCollected = "cod.collected";
    public const string CodSettled = "cod.settled";
    public const string PartnerApiClientCreated = "partner_api_client.created";
    public const string PartnerApiClientKeyRotated = "partner_api_client.key_rotated";
    public const string PartnerApiClientActiveStatusChanged = "partner_api_client.active_status_changed";
    public const string PartnerWebhookEndpointUpserted = "partner_webhook_endpoint.upserted";
    public const string PartnerWebhookTestQueued = "partner_webhook.test_queued";
    public const string RouteRegionConfigChanged = "route_region_config.changed";
    public const string FeeRuleVersionCreated = "fee_rule.version_created";
}
