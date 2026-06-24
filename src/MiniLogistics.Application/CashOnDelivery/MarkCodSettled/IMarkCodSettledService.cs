using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.CashOnDelivery.MarkCodSettled;

public interface IMarkCodSettledService
{
    Task<Result> MarkSettledAsync(
        MarkCodSettledCommand command,
        CancellationToken cancellationToken = default);
}
