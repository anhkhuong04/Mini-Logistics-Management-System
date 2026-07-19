using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments;

/// <summary>
/// Defines write-side shipment persistence operations.
/// </summary>
public interface IShipmentWriteRepository
{
    /// <summary>
    /// Adds a new shipment to the current unit of work.
    /// </summary>
    /// <param name="shipment">The shipment aggregate to persist.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task AddAsync(Shipment shipment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists pending shipment repository changes.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
