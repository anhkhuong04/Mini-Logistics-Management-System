namespace MiniLogistics.Domain.Outbox;

public enum OutboxMessageStatus
{
    Pending = 1,
    Processing = 2,
    Succeeded = 3,
    Failed = 4
}
