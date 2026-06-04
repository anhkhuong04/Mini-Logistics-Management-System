using MiniLogistics.Domain.CashOnDelivery;

namespace MiniLogistics.Application.CashOnDelivery;

public interface ICodTransactionRepository
{
    Task<CodTransaction?> GetByShipmentIdAsync(
        Guid shipmentId,
        CancellationToken cancellationToken = default);

    Task<CodTransaction?> GetTrackedByShipmentIdAsync(
        Guid shipmentId,
        CancellationToken cancellationToken = default);

    Task AddAsync(CodTransaction codTransaction, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
