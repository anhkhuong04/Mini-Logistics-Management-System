using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Shipments;

public sealed class ShipmentImportBatchRow : AuditableEntity
{
    private ShipmentImportBatchRow()
    {
        PayloadJson = string.Empty;
    }

    public ShipmentImportBatchRow(
        Guid batchId,
        Guid shopId,
        int rowNumber,
        string? clientOrderCode,
        string payloadJson,
        bool isValidAtSubmission,
        DateTimeOffset createdAtUtc,
        string? validationErrors = null)
        : base(Guid.NewGuid(), createdAtUtc)
    {
        if (batchId == Guid.Empty || shopId == Guid.Empty)
        {
            throw new DomainException("Import batch id and shop id are required.");
        }

        if (rowNumber <= 0)
        {
            throw new DomainException("Import row number must be positive.");
        }

        BatchId = batchId;
        ShopId = shopId;
        RowNumber = rowNumber;
        ClientOrderCode = DomainGuard.TrimOptional(clientOrderCode, 100);
        PayloadJson = DomainGuard.RequireText(payloadJson, nameof(payloadJson), 4000);
        IsValidAtSubmission = isValidAtSubmission;
        Status = isValidAtSubmission ? ShipmentImportRowStatus.Pending : ShipmentImportRowStatus.Failed;
        Errors = DomainGuard.TrimOptional(validationErrors, 4000);
    }

    public Guid BatchId { get; private set; }

    public Guid ShopId { get; private set; }

    public int RowNumber { get; private set; }

    public string? ClientOrderCode { get; private set; }

    public string PayloadJson { get; private set; }

    public bool IsValidAtSubmission { get; private set; }

    public ShipmentImportRowStatus Status { get; private set; }

    public Guid? ShipmentId { get; private set; }

    public string? TrackingCode { get; private set; }

    public string? Errors { get; private set; }

    public void MarkProcessing(DateTimeOffset processedAtUtc)
    {
        if (!IsValidAtSubmission || Status == ShipmentImportRowStatus.Created)
        {
            return;
        }

        Status = ShipmentImportRowStatus.Processing;
        MarkUpdated(processedAtUtc);
    }

    public void MarkCreated(Guid shipmentId, string trackingCode, DateTimeOffset completedAtUtc)
    {
        if (shipmentId == Guid.Empty)
        {
            throw new DomainException("Created shipment id is required.");
        }

        ShipmentId = shipmentId;
        TrackingCode = DomainGuard.RequireText(trackingCode, nameof(trackingCode), 50);
        Status = ShipmentImportRowStatus.Created;
        Errors = null;
        MarkUpdated(completedAtUtc);
    }

    public void MarkFailed(string error, DateTimeOffset completedAtUtc)
    {
        Status = ShipmentImportRowStatus.Failed;
        Errors = DomainGuard.RequireText(error, nameof(error), 4000);
        MarkUpdated(completedAtUtc);
    }
}
