using MiniLogistics.Domain.Outbox;

namespace MiniLogistics.Application.Outbox;

public interface IOutboxWriter
{
    Task AddAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default);
}
