namespace MiniLogistics.Application.AdminDashboard;

public interface IAdminDashboardMetricsRepository
{
    Task<AdminDashboardRepositoryMetrics> GetAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? province,
        IReadOnlyCollection<ActiveShipperCapacity> activeShippers,
        CancellationToken cancellationToken = default);
}

public sealed record ActiveShipperCapacity(
    Guid ShipperId,
    bool IsAvailableForAssignment,
    int MaxActiveShipments);

public sealed record AdminDashboardRepositoryMetrics(
    ShipmentOverviewMetrics Shipments,
    ShipperOverviewMetrics Shippers,
    CodOverviewMetrics Cod,
    ShopOverviewMetrics Shops,
    WebhookOverviewMetrics Webhooks);
