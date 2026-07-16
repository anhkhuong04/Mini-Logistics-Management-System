using MiniLogistics.Domain.Operations;

namespace MiniLogistics.Application.Routing;

public interface IRouteRegionConfigRepository : IRouteRegionConfigSource
{
    Task<IReadOnlyList<RouteRegionConfig>> GetAllAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RouteRegionConfig>> GetActiveByProvinceAsync(
        string province,
        CancellationToken cancellationToken = default);

    Task<int> GetLatestVersionAsync(
        string province,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        RouteRegionConfig config,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
