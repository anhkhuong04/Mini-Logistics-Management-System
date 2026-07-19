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

    public void MarkProcessing(DateTimeOffset processingAtUtc)
    {
        Status = OutboxMessageStatus.Processing;
        MarkUpdated(processingAtUtc);
    }

    public void MarkSucceeded(DateTimeOffset processedAtUtc)
    {
        Status = OutboxMessageStatus.Succeeded;
        ProcessedAtUtc = processedAtUtc;
        NextAttemptAtUtc = null;
        LastError = null;
        MarkUpdated(processedAtUtc);
    }

    public void MarkFailed(
        string error,
        DateTimeOffset? nextAttemptAtUtc,
        DateTimeOffset failedAtUtc)
    {
        Status = OutboxMessageStatus.Failed;
        RetryCount++;
        LastError = DomainGuard.TrimOptional(error, 1000);
        NextAttemptAtUtc = nextAttemptAtUtc;
        MarkUpdated(failedAtUtc);
    }
}
