namespace MiniLogistics.Application.Shipments.AssignmentSelection;

public interface IShipperAssignmentCapacityGuard
{
    Task<IShipperAssignmentCapacityLease?> TryAcquireAsync(
        Guid shipperId,
        CancellationToken cancellationToken = default);
}

public interface IShipperAssignmentCapacityLease : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
