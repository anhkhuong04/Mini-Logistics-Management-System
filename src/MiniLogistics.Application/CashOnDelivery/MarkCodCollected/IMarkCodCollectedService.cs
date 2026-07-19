using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.CashOnDelivery.MarkCodCollected;

/// <summary>
/// Defines the application use case contract for Mark Cod Collected.
/// </summary>
public interface IMarkCodCollectedService
{
    Task<Result> MarkCollectedAsync(
        MarkCodCollectedCommand command,
        CancellationToken cancellationToken = default);
}
