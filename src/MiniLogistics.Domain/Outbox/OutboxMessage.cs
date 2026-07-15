using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Outbox;

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
        DateTimeOffset? nextAttemptAtUtc = null)
        : base(id)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Outbox message id is required.");
        }

        if (aggregateId == Guid.Empty)
        {
            throw new DomainException("Aggregate id is required.");
        }

        Type = RequireText(type, nameof(type), 120);
        AggregateId = aggregateId;
        PayloadJson = RequireText(payloadJson, nameof(payloadJson), 4000);
        Status = OutboxMessageStatus.Pending;
        NextAttemptAtUtc = nextAttemptAtUtc ?? DateTimeOffset.UtcNow;
    }

    public string Type { get; private set; }

    public Guid AggregateId { get; private set; }

    public string PayloadJson { get; private set; }

    public OutboxMessageStatus Status { get; private set; }

    public int RetryCount { get; private set; }

    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public void MarkProcessing()
    {
        Status = OutboxMessageStatus.Processing;
        MarkUpdated();
    }

    public void MarkSucceeded(DateTimeOffset processedAtUtc)
    {
        Status = OutboxMessageStatus.Succeeded;
        ProcessedAtUtc = processedAtUtc;
        NextAttemptAtUtc = null;
        LastError = null;
        MarkUpdated();
    }

    public void MarkFailed(
        string error,
        DateTimeOffset? nextAttemptAtUtc)
    {
        Status = OutboxMessageStatus.Failed;
        RetryCount++;
        LastError = TrimOptional(error, 1000);
        NextAttemptAtUtc = nextAttemptAtUtc;
        MarkUpdated();
    }

    private static string RequireText(string value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{fieldName} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{fieldName} cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }

    private static string? TrimOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed[..Math.Min(trimmed.Length, maxLength)];
    }
}
