using MiniLogistics.Domain.Outbox;

namespace MiniLogistics.Application.Outbox;

/// <summary>
/// Defines write operations for Outbox Writer.
/// </summary>
public interface IOutboxWriter
{
    Task AddAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default);
}
