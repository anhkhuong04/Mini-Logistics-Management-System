using MiniLogistics.Domain.Outbox;

namespace MiniLogistics.Application.Outbox;

/// <summary>
/// Defines persistence operations for Outbox Message data.
/// </summary>
public interface IOutboxMessageRepository : IOutboxWriter
{
    Task<OutboxMessage?> GetByIdAsync(
        Guid outboxMessageId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<OutboxMessage?>(null);
    }

    Task<IReadOnlyList<OutboxMessage>> GetRecentFailuresAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<OutboxMessage>>([]);
    }

    Task<IReadOnlyList<OutboxMessage>> GetDueAsync(
        DateTimeOffset dueAtUtc,
        int batchSize,
        CancellationToken cancellationToken = default);

    async Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(
        string workerId,
        DateTimeOffset nowUtc,
        DateTimeOffset lockedUntilUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var due = await GetDueAsync(nowUtc, batchSize, cancellationToken);
        return due
            .Where(message => message.TryAcquireLease(workerId, Guid.NewGuid(), nowUtc, lockedUntilUtc))
            .ToList();
    }

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
