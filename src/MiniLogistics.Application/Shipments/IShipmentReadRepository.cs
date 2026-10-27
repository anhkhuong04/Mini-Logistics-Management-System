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
    Task<PagedResult<Shipment>> GetByShopIdPagedAsync(
        Guid shopId,
        int pageNumber,
        int pageSize,
        ShipmentStatus? statusFilter = null,
        string? trackingCodeSearch = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<Shipment>> SearchByShopAsync(
        ShopShipmentSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all shipments currently in the specified status.
    /// </summary>
    /// <param name="status">The shipment status to match.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<IReadOnlyList<Shipment>> GetByStatusAsync(
        ShipmentStatus status,
        CancellationToken cancellationToken = default);

    Task<PagedResult<Shipment>> SearchPendingPickupAsync(
        PendingPickupShipmentSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a bounded page of shipments currently in the specified status.
    /// </summary>
    /// <param name="status">The shipment status to match.</param>
    /// <param name="pageNumber">The one-based page number. Values lower than one are normalized to one.</param>
    /// <param name="pageSize">The requested page size. Implementations clamp this to a bounded maximum.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<PagedResult<Shipment>> GetByStatusPagedAsync(
        ShipmentStatus status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns shipments whose status is in the provided set.
    /// </summary>
    /// <param name="statuses">The statuses to include.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<IReadOnlyList<Shipment>> GetByStatusesAsync(
        IReadOnlyCollection<ShipmentStatus> statuses,
        CancellationToken cancellationToken = default);

    Task<PagedResult<Shipment>> SearchOperationsAsync(
        OperationsShipmentSearchCriteria criteria,
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

    Task<PagedResult<Shipment>> SearchAssignedToShipperAsync(
        AssignedShipmentsForShipperSearchCriteria criteria,
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

}
