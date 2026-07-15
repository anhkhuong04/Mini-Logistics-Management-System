using MiniLogistics.Domain.Outbox;

namespace MiniLogistics.Application.Outbox;

public interface IOutboxMessageRepository : IOutboxWriter
{
    Task<IReadOnlyList<OutboxMessage>> GetDueAsync(
        DateTimeOffset dueAtUtc,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
