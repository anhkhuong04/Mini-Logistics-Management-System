using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.ImportShipments;

public interface IShipmentImportBatchRepository
{
    Task AddAsync(ShipmentImportBatch batch, CancellationToken cancellationToken = default);

    Task<ShipmentImportBatch?> GetByIdForShopAsync(
        Guid batchId,
        Guid shopId,
        CancellationToken cancellationToken = default);

    Task<ShipmentImportBatch?> GetNextProcessableAsync(CancellationToken cancellationToken = default);

    Task<bool> HasCreatedClientOrderAsync(
        Guid shopId,
        string clientOrderCode,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
