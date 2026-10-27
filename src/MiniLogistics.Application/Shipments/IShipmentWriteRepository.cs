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

    /// <summary>
    /// Returns a tracked shipment owned by a specific shop for domain mutation.
    /// </summary>
    /// <param name="shipmentId">The shipment identifier.</param>
    /// <param name="shopId">The expected owner shop identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Shipment?> GetTrackedByIdAndShopIdAsync(
        Guid shipmentId,
        Guid shopId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a tracked shipment by identifier for back-office mutation.
    /// </summary>
    /// <param name="shipmentId">The shipment identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Shipment?> GetTrackedByIdAsync(
        Guid shipmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a tracked shipment by tracking code and owner shop for domain mutation.
    /// </summary>
    /// <param name="trackingCode">The normalized tracking code to load.</param>
    /// <param name="shopId">The expected owner shop identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Shipment?> GetTrackedByTrackingCodeAndShopIdAsync(
        TrackingCode trackingCode,
        Guid shopId,
        CancellationToken cancellationToken = default);
}
