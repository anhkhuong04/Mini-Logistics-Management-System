namespace MiniLogistics.Application.Shipments.BulkRetryAutoAssignment;

public sealed record BulkRetryAutoAssignmentCommand(
    Guid RequestedByUserId,
    IReadOnlyList<Guid> ShipmentIds);
