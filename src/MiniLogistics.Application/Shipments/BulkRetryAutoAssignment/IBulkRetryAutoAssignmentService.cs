using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.BulkRetryAutoAssignment;

/// <summary>
/// Defines the application use case contract for Bulk Retry Auto Assignment.
/// </summary>
public interface IBulkRetryAutoAssignmentService
{
    Task<Result<BulkRetryAutoAssignmentResult>> RetryAsync(
        BulkRetryAutoAssignmentCommand command,
        CancellationToken cancellationToken = default);
}
