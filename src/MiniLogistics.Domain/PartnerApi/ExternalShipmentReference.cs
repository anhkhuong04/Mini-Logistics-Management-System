using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.PartnerApi;

/// <summary>
/// Represents the External Shipment Reference domain entity.
/// </summary>
public sealed class ExternalShipmentReference : AuditableEntity
{
    private ExternalShipmentReference()
    {
        ExternalOrderId = string.Empty;
        IdempotencyKey = string.Empty;
        RequestHash = string.Empty;
        ResponseSnapshotJson = string.Empty;
    }

    public ExternalShipmentReference(
        Guid apiClientId,
        Guid shopId,
        Guid shipmentId,
        string externalOrderId,
        string idempotencyKey,
        string requestHash,
        string responseSnapshotJson,
        DateTimeOffset createdAtUtc)
        : base(Guid.NewGuid(), createdAtUtc)
    {
        if (apiClientId == Guid.Empty)
        {
            throw new DomainException("API client id is required.");
        }

        if (shopId == Guid.Empty)
        {
            throw new DomainException("Shop id is required.");
        }

        if (shipmentId == Guid.Empty)
        {
            throw new DomainException("Shipment id is required.");
        }

        ApiClientId = apiClientId;
        ShopId = shopId;
        ShipmentId = shipmentId;
        ExternalOrderId = DomainGuard.RequireText(externalOrderId, nameof(externalOrderId), 100);
        IdempotencyKey = DomainGuard.RequireText(idempotencyKey, nameof(idempotencyKey), 150);
        RequestHash = DomainGuard.RequireText(requestHash, nameof(requestHash), 128);
        ResponseSnapshotJson = DomainGuard.RequireText(responseSnapshotJson, nameof(responseSnapshotJson), 4000);
    }

    public Guid ApiClientId { get; private set; }

    public Guid ShopId { get; private set; }

    public Guid ShipmentId { get; private set; }

    public string ExternalOrderId { get; private set; }

    public string IdempotencyKey { get; private set; }

    public string RequestHash { get; private set; }

    public string ResponseSnapshotJson { get; private set; }

    public void UpdateResponseSnapshot(string responseSnapshotJson, DateTimeOffset updatedAtUtc)
    {
        ResponseSnapshotJson = DomainGuard.RequireText(responseSnapshotJson, nameof(responseSnapshotJson), 4000);
        MarkUpdated(updatedAtUtc);
    }
}
