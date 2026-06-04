using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.CashOnDelivery.MarkCodCollected;

public interface IMarkCodCollectedService
{
    Task<Result> MarkCollectedAsync(
        MarkCodCollectedCommand command,
        CancellationToken cancellationToken = default);
}
