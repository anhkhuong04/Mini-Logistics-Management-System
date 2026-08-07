using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Outbox;

/// <summary>
/// Represents the Outbox Message domain entity.
/// </summary>
public sealed class OutboxMessage : AuditableEntity
{
    private OutboxMessage()
    {
        Type = string.Empty;
        PayloadJson = string.Empty;
        RowVersion = [];
    }

    public OutboxMessage(
        Guid id,
        string type,
        Guid aggregateId,
        string payloadJson,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? nextAttemptAtUtc = null)
        : base(id, createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Outbox message id is required.");
        }

        if (aggregateId == Guid.Empty)
        {
            throw new DomainException("Aggregate id is required.");
        }

        Type = DomainGuard.RequireText(type, nameof(type), 120);
        AggregateId = aggregateId;
        PayloadJson = DomainGuard.RequireText(payloadJson, nameof(payloadJson), 4000);
        Status = OutboxMessageStatus.Pending;
        NextAttemptAtUtc = nextAttemptAtUtc ?? createdAtUtc;
    }

    public string Type { get; private set; }

    public Guid AggregateId { get; private set; }

    public string PayloadJson { get; private set; }

    public OutboxMessageStatus Status { get; private set; }

    public int RetryCount { get; private set; }

    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public string? LockedBy { get; private set; }

    public DateTimeOffset? LockedUntilUtc { get; private set; }

    public Guid? AttemptId { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool TryAcquireLease(
        string workerId,
        Guid attemptId,
        DateTimeOffset nowUtc,
        DateTimeOffset lockedUntilUtc)
    {
        if (Status is OutboxMessageStatus.Succeeded or OutboxMessageStatus.DeadLettered
            || NextAttemptAtUtc is null
            || NextAttemptAtUtc > nowUtc
            || (LockedUntilUtc.HasValue && LockedUntilUtc > nowUtc))
        {
            return false;
        }

        LockedBy = DomainGuard.RequireText(workerId, nameof(workerId), 200);
        AttemptId = attemptId == Guid.Empty
            ? throw new DomainException("Attempt id is required.")
            : attemptId;
        LockedUntilUtc = lockedUntilUtc > nowUtc
            ? lockedUntilUtc
            : throw new DomainException("Lease expiration must be in the future.");
        Status = OutboxMessageStatus.Processing;
        MarkUpdated(nowUtc);
        return true;
    }

    public void MarkSucceeded(DateTimeOffset processedAtUtc)
    {
        Status = OutboxMessageStatus.Succeeded;
        ProcessedAtUtc = processedAtUtc;
        NextAttemptAtUtc = null;
        LastError = null;
        ReleaseLease();
        MarkUpdated(processedAtUtc);
    }

    public void MarkFailed(
        string error,
        DateTimeOffset? nextAttemptAtUtc,
        DateTimeOffset failedAtUtc)
    {
        Status = nextAttemptAtUtc.HasValue
            ? OutboxMessageStatus.Failed
            : OutboxMessageStatus.DeadLettered;
        RetryCount++;
        LastError = DomainGuard.TrimOptional(error, 1000);
        NextAttemptAtUtc = nextAttemptAtUtc;
        ReleaseLease();
        MarkUpdated(failedAtUtc);
    }

    public Result Retry(DateTimeOffset queuedAtUtc)
    {
        if (Status is not (OutboxMessageStatus.Failed or OutboxMessageStatus.DeadLettered))
        {
            return Result.Failure(new Error(
                "OutboxMessage.NotFailed",
                "Only failed or dead-lettered outbox messages can be retried."));
        }

        Status = OutboxMessageStatus.Pending;
        NextAttemptAtUtc = queuedAtUtc;
        LastError = null;
        ReleaseLease();
        MarkUpdated(queuedAtUtc);
        return Result.Success();
    }

    private void ReleaseLease()
    {
        LockedBy = null;
        LockedUntilUtc = null;
        AttemptId = null;
    }
}
