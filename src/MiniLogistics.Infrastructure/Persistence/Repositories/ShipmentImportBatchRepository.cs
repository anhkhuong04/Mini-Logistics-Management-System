using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Shipments.ImportShipments;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class ShipmentImportBatchRepository : IShipmentImportBatchRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public ShipmentImportBatchRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(ShipmentImportBatch batch, CancellationToken cancellationToken = default)
    {
        await _dbContext.ShipmentImportBatches.AddAsync(batch, cancellationToken);
    }

    public Task<ShipmentImportBatch?> GetByIdForShopAsync(
        Guid batchId,
        Guid shopId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ShipmentImportBatches
            .Include(batch => batch.Rows)
            .FirstOrDefaultAsync(batch => batch.Id == batchId && batch.ShopId == shopId, cancellationToken);
    }

    public Task<ShipmentImportBatch?> GetNextProcessableAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.ShipmentImportBatches
            .Include(batch => batch.Rows)
            .Where(batch => batch.Status == ShipmentImportBatchStatus.Pending
                || batch.Status == ShipmentImportBatchStatus.Processing)
            .Where(batch => batch.Rows.Any(row => row.Status == ShipmentImportRowStatus.Pending
                || row.Status == ShipmentImportRowStatus.Processing))
            .OrderBy(batch => batch.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> HasCreatedClientOrderAsync(
        Guid shopId,
        string clientOrderCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = clientOrderCode.Trim();
        return _dbContext.ShipmentImportBatchRows.AnyAsync(row =>
            row.ShopId == shopId
            && row.ClientOrderCode == normalizedCode
            && row.Status == ShipmentImportRowStatus.Created,
            cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
