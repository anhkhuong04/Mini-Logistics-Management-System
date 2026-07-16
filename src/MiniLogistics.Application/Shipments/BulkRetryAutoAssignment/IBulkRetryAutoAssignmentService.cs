using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.BulkRetryAutoAssignment;

public interface IBulkRetryAutoAssignmentService
{
    Task<Result<BulkRetryAutoAssignmentResult>> RetryAsync(
        BulkRetryAutoAssignmentCommand command,
        CancellationToken cancellationToken = default);
}
