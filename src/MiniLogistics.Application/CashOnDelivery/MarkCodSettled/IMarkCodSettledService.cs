using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.CashOnDelivery.MarkCodSettled;

/// <summary>
/// Defines the application use case contract for Mark Cod Settled.
/// </summary>
public interface IMarkCodSettledService
{
    Task<Result> MarkSettledAsync(
        MarkCodSettledCommand command,
        CancellationToken cancellationToken = default);
}
