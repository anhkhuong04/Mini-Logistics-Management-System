namespace MiniLogistics.Application.Shipments.BulkRetryAutoAssignment;

public sealed record BulkRetryAutoAssignmentResult(
    int RequestedCount,
    int RetriedCount,
    int AssignedCount,
    int SkippedCount,
    IReadOnlyList<BulkRetryAutoAssignmentItem> Items);

public sealed record BulkRetryAutoAssignmentItem(
    Guid ShipmentId,
    string TrackingCode,
    string Status,
    string Result,
    string Reason);
