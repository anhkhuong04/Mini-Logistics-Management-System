namespace MiniLogistics.Domain.Outbox;

/// <summary>
/// Defines the supported Outbox Message Status values in the domain model.
/// </summary>
public enum OutboxMessageStatus
{
    Pending = 1,
    Processing = 2,
    Succeeded = 3,
    Failed = 4
}
