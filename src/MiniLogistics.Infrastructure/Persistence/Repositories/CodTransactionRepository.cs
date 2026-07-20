using Microsoft.EntityFrameworkCore;
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

    public async Task<IReadOnlyList<CodTransaction>> GetByStatusesAsync(
        IReadOnlyCollection<CodStatus> statuses,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CodTransactions
            .AsNoTracking()
            .Where(codTransaction => statuses.Contains(codTransaction.Status))
            .OrderBy(codTransaction => codTransaction.CollectedAtUtc)
            .ThenBy(codTransaction => codTransaction.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<CodTransaction?> GetByShipmentIdAsync(
        Guid shipmentId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.CodTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                codTransaction => codTransaction.ShipmentId == shipmentId,
                cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, CodTransaction>> GetByShipmentIdsAsync(
        IReadOnlyCollection<Guid> shipmentIds,
        CancellationToken cancellationToken = default)
    {
        if (shipmentIds.Count == 0)
        {
            return new Dictionary<Guid, CodTransaction>();
        }

        return await _dbContext.CodTransactions
            .AsNoTracking()
            .Where(codTransaction => shipmentIds.Contains(codTransaction.ShipmentId))
            .ToDictionaryAsync(
                codTransaction => codTransaction.ShipmentId,
                cancellationToken);
    }

    public Task<CodTransaction?> GetTrackedByShipmentIdAsync(
        Guid shipmentId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.CodTransactions
            .FirstOrDefaultAsync(
                codTransaction => codTransaction.ShipmentId == shipmentId,
                cancellationToken);
    }

    public async Task AddAsync(CodTransaction codTransaction, CancellationToken cancellationToken = default)
    {
        await _dbContext.CodTransactions.AddAsync(codTransaction, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
