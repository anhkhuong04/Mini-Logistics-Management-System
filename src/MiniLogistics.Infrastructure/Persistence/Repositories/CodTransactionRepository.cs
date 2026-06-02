using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Domain.CashOnDelivery;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class CodTransactionRepository : ICodTransactionRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public CodTransactionRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(CodTransaction codTransaction, CancellationToken cancellationToken = default)
    {
        await _dbContext.CodTransactions.AddAsync(codTransaction, cancellationToken);
    }
}
