namespace MiniLogistics.Domain.Shipments;

public enum ShipmentImportBatchStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    CompletedWithErrors = 3,
    Failed = 4
}
