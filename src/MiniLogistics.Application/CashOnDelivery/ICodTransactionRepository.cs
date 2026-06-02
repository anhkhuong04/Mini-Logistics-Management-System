using MiniLogistics.Domain.CashOnDelivery;

namespace MiniLogistics.Application.CashOnDelivery;

public interface ICodTransactionRepository
{
    Task AddAsync(CodTransaction codTransaction, CancellationToken cancellationToken = default);
}
