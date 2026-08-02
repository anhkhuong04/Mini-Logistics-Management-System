using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Shipments;

public sealed class ShipmentImportBatch : AuditableEntity
{
    private readonly List<ShipmentImportBatchRow> _rows = [];

    private ShipmentImportBatch()
    {
    }

    public ShipmentImportBatch(
        Guid shopId,
        Guid requestedByUserId,
        DateTimeOffset createdAtUtc)
        : base(Guid.NewGuid(), createdAtUtc)
    {
        if (shopId == Guid.Empty || requestedByUserId == Guid.Empty)
        {
            throw new DomainException("Import shop id and requesting user id are required.");
        }

        ShopId = shopId;
        RequestedByUserId = requestedByUserId;
        Status = ShipmentImportBatchStatus.Pending;
    }

    public Guid ShopId { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public ShipmentImportBatchStatus Status { get; private set; }

    public int TotalRows { get; private set; }

    public int ValidRows { get; private set; }

    public int CreatedRows { get; private set; }

    public int FailedRows { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public IReadOnlyCollection<ShipmentImportBatchRow> Rows => _rows.AsReadOnly();

    public void AddRow(ShipmentImportBatchRow row)
    {
        if (row.BatchId != Id || row.ShopId != ShopId)
        {
            throw new DomainException("Import row does not belong to this batch.");
        }

        _rows.Add(row);
        RefreshProgress(CreatedAtUtc);
    }

    public void MarkProcessing(DateTimeOffset processedAtUtc)
    {
        if (Status == ShipmentImportBatchStatus.Pending)
        {
            Status = ShipmentImportBatchStatus.Processing;
            MarkUpdated(processedAtUtc);
        }
    }

    public void RefreshProgress(DateTimeOffset updatedAtUtc)
    {
        TotalRows = _rows.Count;
        ValidRows = _rows.Count(row => row.IsValidAtSubmission);
        CreatedRows = _rows.Count(row => row.Status == ShipmentImportRowStatus.Created);
        FailedRows = _rows.Count(row => row.Status == ShipmentImportRowStatus.Failed);

        var hasWork = _rows.Any(row => row.Status is ShipmentImportRowStatus.Pending or ShipmentImportRowStatus.Processing);
        if (TotalRows > 0 && !hasWork)
        {
            Status = FailedRows == 0
                ? ShipmentImportBatchStatus.Completed
                : ShipmentImportBatchStatus.CompletedWithErrors;
            CompletedAtUtc = updatedAtUtc;
        }
        else if (_rows.Any(row => row.Status == ShipmentImportRowStatus.Processing))
        {
            Status = ShipmentImportBatchStatus.Processing;
            CompletedAtUtc = null;
        }
        else if (hasWork)
        {
            Status = ShipmentImportBatchStatus.Pending;
            CompletedAtUtc = null;
        }

        MarkUpdated(updatedAtUtc);
    }
}
