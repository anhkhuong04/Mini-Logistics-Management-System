namespace MiniLogistics.Application.AdminAuditing;

public static class AdminAuditActions
{
    public const string InternalUserCreated = "internal_user.created";
    public const string UserActiveStatusChanged = "user.active_status_changed";
    public const string ShipperCapacityChanged = "shipper.capacity_changed";
    public const string ShipperAvailabilityChanged = "shipper.availability.changed";
    public const string ShipperWorkingAreasChanged = "shipper.working_areas_changed";
    public const string HubCreated = "hub.created";
    public const string HubUpdated = "hub.updated";
    public const string HubActiveStatusChanged = "hub.active_status_changed";
    public const string ShopActiveStatusChanged = "shop.active_status_changed";
    public const string ShopProfileUpdated = "shop.profile.updated";
    public const string ShopAdditionalShopCreated = "shop.additional_shop.created";
    public const string ShipmentCreated = "shipment.created";
    public const string ShipmentDraftCreated = "shipment.draft_created";
    public const string ShipmentDraftSubmitted = "shipment.draft_submitted";
    public const string ShipmentUpdatedBeforePickup = "shipment.updated_before_pickup";
    public const string ShipmentCancelledByShop = "shipment.cancelled_by_shop";
    public const string ShipmentImportPreviewed = "shipment.import.previewed";
    public const string ShipmentImportConfirmed = "shipment.import.confirmed";
    public const string ShipmentManualAssigned = "shipment.manual_assigned";
    public const string ShipmentReassigned = "shipment.reassigned";
    public const string ShipmentAssignmentCancelled = "shipment.assignment_cancelled";
    public const string ShipmentAutoAssignmentRetried = "shipment.auto_assignment_retried";
    public const string ShipmentBulkAutoAssignmentRetried = "shipment.bulk_auto_assignment_retried";
    public const string ShipmentStatusChanged = "shipment.status_changed";
    public const string ShipmentStatusChangedByOperator = "shipment.status_changed_by_operator";
    public const string ShipmentStatusChangedByShipper = "shipment.status_changed_by_shipper";
    public const string ShipmentPodUploaded = "shipment.pod_uploaded";
    public const string ShipmentGpsCheckInRecorded = "shipment.gps_check_in_recorded";
    public const string CodCollected = "cod.collected";
    public const string CodCollectedByOperator = "cod.collected_by_operator";
    public const string CodCollectedByShipper = "cod.collected_by_shipper";
    public const string CodSettled = "cod.settled";
    public const string PartnerApiClientCreated = "partner_api_client.created";
    public const string PartnerApiClientKeyRotated = "partner_api_client.key_rotated";
    public const string PartnerApiClientActiveStatusChanged = "partner_api_client.active_status_changed";
    public const string PartnerWebhookEndpointUpserted = "partner_webhook_endpoint.upserted";
    public const string PartnerWebhookTestQueued = "partner_webhook.test_queued";
    public const string RouteRegionConfigChanged = "route_region_config.changed";
    public const string FeeRuleVersionCreated = "fee_rule.version_created";
}
