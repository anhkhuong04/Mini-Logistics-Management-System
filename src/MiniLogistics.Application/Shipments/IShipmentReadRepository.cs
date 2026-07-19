using MiniLogistics.Application.Common;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments;

/// <summary>
/// Defines read-side shipment persistence operations.
/// </summary>
public interface IShipmentReadRepository
{
    /// <summary>
    /// Checks whether any shipment already owns the provided tracking code.
    /// </summary>
    /// <param name="trackingCode">The normalized tracking code to check.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<bool> ExistsByTrackingCodeAsync(
        TrackingCode trackingCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all shipments for a shop ordered for list display.
    /// </summary>
    /// <param name="shopId">The shop that owns the shipments.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<IReadOnlyList<Shipment>> GetByShopIdAsync(
        Guid shopId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a bounded page of shipments for a shop, optionally filtered by status and tracking/receiver text.
    /// </summary>
    /// <param name="shopId">The shop that owns the shipments.</param>
    /// <param name="pageNumber">The one-based page number. Values lower than one are normalized to one.</param>
    /// <param name="pageSize">The requested page size. Implementations clamp this to a bounded maximum.</param>
    /// <param name="statusFilter">Optional shipment status filter.</param>
    /// <param name="trackingCodeSearch">Optional tracking code or receiver-name search text.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    async Task<PagedResult<Shipment>> GetByShopIdPagedAsync(
        Guid shopId,
        int pageNumber,
        int pageSize,
        ShipmentStatus? statusFilter = null,
        string? trackingCodeSearch = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = NormalizePageNumber(pageNumber);
        var normalizedPageSize = NormalizePageSize(pageSize);
        var shipments = await GetByShopIdAsync(shopId, cancellationToken);
        var query = shipments.AsEnumerable();

        if (statusFilter.HasValue)
        {
            query = query.Where(shipment => shipment.Status == statusFilter.Value);
        }

        if (!string.IsNullOrWhiteSpace(trackingCodeSearch))
        {
            var keyword = trackingCodeSearch.Trim();
            query = query.Where(shipment =>
                shipment.TrackingCode.Value.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || shipment.ReceiverName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = query.ToList();
        var items = filtered
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToList();

        return new PagedResult<Shipment>(
            items,
            normalizedPageNumber,
            normalizedPageSize,
            filtered.Count);
    }

    /// <summary>
    /// Returns all shipments currently in the specified status.
    /// </summary>
    /// <param name="status">The shipment status to match.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<IReadOnlyList<Shipment>> GetByStatusAsync(
        ShipmentStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a bounded page of shipments currently in the specified status.
    /// </summary>
    /// <param name="status">The shipment status to match.</param>
    /// <param name="pageNumber">The one-based page number. Values lower than one are normalized to one.</param>
    /// <param name="pageSize">The requested page size. Implementations clamp this to a bounded maximum.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    async Task<PagedResult<Shipment>> GetByStatusPagedAsync(
        ShipmentStatus status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = NormalizePageNumber(pageNumber);
        var normalizedPageSize = NormalizePageSize(pageSize);
        var shipments = await GetByStatusAsync(status, cancellationToken);
        var items = shipments
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToList();

        return new PagedResult<Shipment>(
            items,
            normalizedPageNumber,
            normalizedPageSize,
            shipments.Count);
    }

    /// <summary>
    /// Returns shipments whose status is in the provided set.
    /// </summary>
    /// <param name="statuses">The statuses to include.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<IReadOnlyList<Shipment>> GetByStatusesAsync(
        IReadOnlyCollection<ShipmentStatus> statuses,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns shipments by their identifiers.
    /// </summary>
    /// <param name="shipmentIds">The shipment identifiers to load.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<IReadOnlyList<Shipment>> GetByIdsAsync(
        IReadOnlyCollection<Guid> shipmentIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns active, non-terminal shipments assigned to a shipper.
    /// </summary>
    /// <param name="shipperId">The shipper whose active assignment list should be loaded.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<IReadOnlyList<Shipment>> GetAssignedToShipperAsync(
        Guid shipperId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns active assignment counts for each requested shipper.
    /// </summary>
    /// <param name="shipperIds">The shipper identifiers to aggregate.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<IReadOnlyDictionary<Guid, int>> GetActiveAssignmentCountsByShipperIdsAsync(
        IReadOnlyCollection<Guid> shipperIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a read-only shipment owned by a specific shop.
    /// </summary>
    /// <param name="shipmentId">The shipment identifier.</param>
    /// <param name="shopId">The expected owner shop identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Shipment?> GetByIdAndShopIdAsync(
        Guid shipmentId,
        Guid shopId,
        CancellationToken cancellationToken = default);

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
    /// Returns a read-only shipment by tracking code.
    /// </summary>
    /// <param name="trackingCode">The normalized tracking code to load.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Shipment?> GetByTrackingCodeAsync(
        TrackingCode trackingCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a read-only shipment by tracking code and owner shop.
    /// </summary>
    /// <param name="trackingCode">The normalized tracking code to load.</param>
    /// <param name="shopId">The expected owner shop identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Shipment?> GetByTrackingCodeAndShopIdAsync(
        TrackingCode trackingCode,
        Guid shopId,
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

    private static int NormalizePageNumber(int pageNumber)
    {
        return Math.Max(1, pageNumber);
    }

    private static int NormalizePageSize(int pageSize)
    {
        return Math.Clamp(pageSize, 1, 100);
    }
}
