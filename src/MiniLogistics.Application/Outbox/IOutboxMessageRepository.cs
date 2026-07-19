using MiniLogistics.Domain.Outbox;

namespace MiniLogistics.Application.Outbox;

/// <summary>
/// Defines persistence operations for Outbox Message data.
/// </summary>
public interface IOutboxMessageRepository : IOutboxWriter
{
    Task<IReadOnlyList<OutboxMessage>> GetDueAsync(
        DateTimeOffset dueAtUtc,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
