namespace MiniLogistics.Application.Shippers;

/// <summary>
/// Invalidates cached hubs after a committed hub configuration change.
/// </summary>
public interface IHubCacheInvalidator
{
    Task InvalidateAsync(CancellationToken cancellationToken = default);
}
