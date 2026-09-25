using MiniLogistics.Application.Shipments.AssignmentSelection;

namespace MiniLogistics.Application.Tests;

internal sealed class StubShipperAssignmentCapacityGuard(
    Func<int, Guid, bool>? canAcquire = null) : IShipperAssignmentCapacityGuard
{
    private int _acquireCount;
    private int _commitCount;

    public int AcquireCount => _acquireCount;

    public int CommitCount => _commitCount;

    public Task<IShipperAssignmentCapacityLease?> TryAcquireAsync(
        Guid shipperId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var callNumber = Interlocked.Increment(ref _acquireCount);
        if (canAcquire is not null && !canAcquire(callNumber, shipperId))
        {
            return Task.FromResult<IShipperAssignmentCapacityLease?>(null);
        }

        return Task.FromResult<IShipperAssignmentCapacityLease?>(new Lease(this));
    }

    private sealed class Lease(StubShipperAssignmentCapacityGuard owner) : IShipperAssignmentCapacityLease
    {
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref owner._commitCount);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
