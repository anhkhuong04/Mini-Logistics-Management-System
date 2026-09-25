namespace MiniLogistics.Application.Routing;

/// <summary>
/// Invalidates cached route-region configuration after a committed configuration change.
/// </summary>
public interface IRouteRegionConfigCacheInvalidator
{
    Task InvalidateAsync(CancellationToken cancellationToken = default);
}
