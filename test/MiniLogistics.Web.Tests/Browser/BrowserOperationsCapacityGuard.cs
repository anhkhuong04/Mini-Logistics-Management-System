using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shipments.AssignmentSelection;
using MiniLogistics.Infrastructure.Persistence;

namespace MiniLogistics.Web.Tests.Browser;

// The browser fixture uses InMemory. SQL locking is proved by the existing LocalDB concurrency suite.
internal sealed class BrowserOperationsCapacityGuard(MiniLogisticsDbContext db, IShipmentReadRepository shipments) : IShipperAssignmentCapacityGuard
{
    public async Task<IShipperAssignmentCapacityLease?> TryAcquireAsync(Guid shipperId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == shipperId, cancellationToken);
        var counts = await shipments.GetActiveAssignmentCountsByShipperIdsAsync([shipperId], cancellationToken);
        return user is null || counts.GetValueOrDefault(shipperId) >= user.MaxActiveShipments ? null : new Lease();
    }
    private sealed class Lease : IShipperAssignmentCapacityLease
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
