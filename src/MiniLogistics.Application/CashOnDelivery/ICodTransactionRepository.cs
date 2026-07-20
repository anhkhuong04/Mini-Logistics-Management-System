using MiniLogistics.Domain.CashOnDelivery;

namespace MiniLogistics.Application.CashOnDelivery;

/// <summary>
/// Defines persistence operations for Cod Transaction data.
/// </summary>
public interface ICodTransactionRepository
{
    Task<IReadOnlyList<CodTransaction>> GetByStatusesAsync(
        IReadOnlyCollection<CodStatus> statuses,
        CancellationToken cancellationToken = default);

    Task<CodTransaction?> GetByShipmentIdAsync(
        Guid shipmentId,
        CancellationToken cancellationToken = default);

    async Task<IReadOnlyDictionary<Guid, CodTransaction>> GetByShipmentIdsAsync(
        IReadOnlyCollection<Guid> shipmentIds,
        CancellationToken cancellationToken = default)
    {
        var transactions = new Dictionary<Guid, CodTransaction>();

        foreach (var shipmentId in shipmentIds.Distinct())
        {
            var transaction = await GetByShipmentIdAsync(shipmentId, cancellationToken);
            if (transaction is not null)
            {
                transactions[shipmentId] = transaction;
            }
        }

        return transactions;
    }

    Task<CodTransaction?> GetTrackedByShipmentIdAsync(
        Guid shipmentId,
        CancellationToken cancellationToken = default);

    Task AddAsync(CodTransaction codTransaction, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
