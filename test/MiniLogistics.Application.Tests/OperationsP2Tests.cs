using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shipments.AutoAssignShipment;
using MiniLogistics.Application.Shipments.BulkRetryAutoAssignment;
using MiniLogistics.Application.Shipments.GetPendingPickupShipments;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Users;
using MiniLogistics.Domain.ValueObjects;
using Xunit;

namespace MiniLogistics.Application.Tests;

public sealed class OperationsP2Tests
{
    private readonly Guid _operatorId = Guid.NewGuid();
    private readonly Guid _shipperId = Guid.NewGuid();

    [Fact]
    public async Task PendingPickupSearch_FiltersAndPagesResults()
    {
        var matched = CreateShipment("Ha Noi", "Receiver A", codAmount: 200_000m);
        var unmatchedProvince = CreateShipment("Ho Chi Minh", "Receiver B", codAmount: 200_000m);
        var unmatchedAmount = CreateShipment("Ha Noi", "Receiver C", codAmount: 50_000m);
        var service = new GetPendingPickupShipmentsService(new FakeShipmentRepository([
            matched,
            unmatchedProvince,
            unmatchedAmount
        ]));

        var result = await service.SearchAsync(new GetPendingPickupShipmentsQuery(
            Province: "Ha Noi",
            MinCodAmount: 100_000m,
            PageNumber: 1,
            PageSize: 10));

        Assert.True(result.IsSuccess, result.Error.Description);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(matched.Id, item.ShipmentId);
        Assert.Equal(1, result.Value.TotalCount);
    }

    [Fact]
    public async Task BulkRetryAutoAssignment_PartialSuccessSkipsNonPendingPickup()
    {
        var pending = CreateShipment("Ha Noi", "Receiver A");
        var assigned = CreateShipment("Ha Noi", "Receiver B");
        var assignResult = assigned.AssignShipper(_shipperId, _operatorId, "Assigned before bulk retry.");
        Assert.True(assignResult.IsSuccess, assignResult.Error.Description);

        var service = new BulkRetryAutoAssignmentService(
            new BulkRetryAutoAssignmentCommandValidator(),
            CreateIdentityService(),
            new FakeShipmentRepository([pending, assigned]),
            new FakeAutoAssignShipmentService([pending, assigned]));

        var result = await service.RetryAsync(new BulkRetryAutoAssignmentCommand(
            _operatorId,
            [pending.Id, assigned.Id, Guid.NewGuid()]));

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal(3, result.Value.RequestedCount);
        Assert.Equal(1, result.Value.RetriedCount);
        Assert.Equal(1, result.Value.AssignedCount);
        Assert.Equal(2, result.Value.SkippedCount);
        Assert.Contains(result.Value.Items, item => item.ShipmentId == pending.Id && item.Result == AutoAssignShipmentStatus.Assigned.ToString());
        Assert.Contains(result.Value.Items, item => item.ShipmentId == assigned.Id && item.Result == "Skipped");
        Assert.Contains(result.Value.Items, item => item.Status == "NotFound" && item.Result == "Skipped");
    }

    private FakeIdentityService CreateIdentityService()
    {
        return new FakeIdentityService([
            new FakeIdentityService.FakeUser(_operatorId, true, [nameof(UserRole.Operator)]),
            new FakeIdentityService.FakeUser(_shipperId, true, [nameof(UserRole.Shipper)])
        ]);
    }

    private static Shipment CreateShipment(
        string pickupProvince,
        string receiverName,
        decimal codAmount = 100_000m)
    {
        return Shipment.Create(
            Guid.NewGuid(),
            "Sender",
            new PhoneNumber("0900000000"),
            receiverName,
            new PhoneNumber("0911111111"),
            new Address("1 Le Loi", "Ben Thanh", pickupProvince),
            new Address("9 Hang Bai", "Hoan Kiem", "Ha Noi"),
            new Weight(1m),
            new ParcelDimensions(10m, 10m, 10m),
            new Weight(1m),
            new Money(500_000m),
            new Money(codAmount),
            new ShippingFeeBreakdown(new Money(20_000m), Money.Zero, Money.Zero, Money.Zero),
            RouteType.InterRegion,
            Guid.NewGuid());
    }

    private sealed class FakeAutoAssignShipmentService : IAutoAssignShipmentService
    {
        private readonly Dictionary<Guid, Shipment> _shipments;

        public FakeAutoAssignShipmentService(IReadOnlyList<Shipment> shipments)
        {
            _shipments = shipments.ToDictionary(shipment => shipment.Id);
        }

        public Task<Result<AutoAssignShipmentResult>> AutoAssignAsync(
            Guid shipmentId,
            CancellationToken cancellationToken = default,
            Guid? requestedByUserId = null)
        {
            var shipment = _shipments[shipmentId];
            return Task.FromResult(Result<AutoAssignShipmentResult>.Success(
                AutoAssignShipmentResult.Assigned(
                    shipment,
                    Guid.NewGuid(),
                    "Assigned by fake bulk retry.")));
        }
    }

    private sealed class FakeShipmentRepository : IShipmentRepository
    {
        private readonly List<Shipment> _shipments;

        public FakeShipmentRepository(IReadOnlyList<Shipment> shipments)
        {
            _shipments = shipments.ToList();
        }

        public Task<bool> ExistsByTrackingCodeAsync(TrackingCode trackingCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shipments.Any(shipment => shipment.TrackingCode == trackingCode));
        }

        public Task<IReadOnlyList<Shipment>> GetByShopIdAsync(Guid shopId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shipment>>(_shipments.Where(shipment => shipment.ShopId == shopId).ToList());
        }

        public Task<IReadOnlyList<Shipment>> GetByStatusAsync(ShipmentStatus status, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shipment>>(_shipments.Where(shipment => shipment.Status == status).ToList());
        }

        public Task<IReadOnlyList<Shipment>> GetByStatusesAsync(IReadOnlyCollection<ShipmentStatus> statuses, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shipment>>(_shipments.Where(shipment => statuses.Contains(shipment.Status)).ToList());
        }

        public Task<IReadOnlyList<Shipment>> GetByIdsAsync(IReadOnlyCollection<Guid> shipmentIds, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shipment>>(_shipments.Where(shipment => shipmentIds.Contains(shipment.Id)).ToList());
        }

        public Task<IReadOnlyList<Shipment>> GetAssignedToShipperAsync(Guid shipperId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shipment>>(_shipments
                .Where(shipment => shipment.Assignments.Any(assignment => assignment.IsActive && assignment.ShipperId == shipperId))
                .ToList());
        }

        public Task<IReadOnlyDictionary<Guid, int>> GetActiveAssignmentCountsByShipperIdsAsync(
            IReadOnlyCollection<Guid> shipperIds,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<Guid, int> counts = _shipments
                .SelectMany(shipment => shipment.Assignments.Where(assignment =>
                    assignment.IsActive && shipperIds.Contains(assignment.ShipperId)))
                .GroupBy(assignment => assignment.ShipperId)
                .ToDictionary(group => group.Key, group => group.Count());

            return Task.FromResult(counts);
        }

        public Task<Shipment?> GetByIdAndShopIdAsync(Guid shipmentId, Guid shopId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shipments.FirstOrDefault(shipment => shipment.Id == shipmentId && shipment.ShopId == shopId));
        }

        public Task<Shipment?> GetTrackedByIdAndShopIdAsync(Guid shipmentId, Guid shopId, CancellationToken cancellationToken = default)
        {
            return GetByIdAndShopIdAsync(shipmentId, shopId, cancellationToken);
        }

        public Task<Shipment?> GetTrackedByIdAsync(Guid shipmentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shipments.FirstOrDefault(shipment => shipment.Id == shipmentId));
        }

        public Task<Shipment?> GetByTrackingCodeAsync(TrackingCode trackingCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shipments.FirstOrDefault(shipment => shipment.TrackingCode == trackingCode));
        }

        public Task<Shipment?> GetByTrackingCodeAndShopIdAsync(
            TrackingCode trackingCode,
            Guid shopId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shipments.FirstOrDefault(shipment =>
                shipment.TrackingCode == trackingCode && shipment.ShopId == shopId));
        }

        public Task<Shipment?> GetTrackedByTrackingCodeAndShopIdAsync(
            TrackingCode trackingCode,
            Guid shopId,
            CancellationToken cancellationToken = default)
        {
            return GetByTrackingCodeAndShopIdAsync(trackingCode, shopId, cancellationToken);
        }

        public Task AddAsync(Shipment shipment, CancellationToken cancellationToken = default)
        {
            _shipments.Add(shipment);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        private readonly Dictionary<Guid, FakeUser> _users;

        public FakeIdentityService(IReadOnlyList<FakeUser> users)
        {
            _users = users.ToDictionary(user => user.UserId);
        }

        public Task<Result<Guid>> CreateUserAsync(
            string fullName,
            string email,
            string phoneNumber,
            string password,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result> AddToRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<Guid>> CreateInternalUserAsync(
            string fullName,
            string email,
            string phoneNumber,
            string password,
            string role,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result> SetUserActiveStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result> SetShipperCapacityAsync(
            Guid userId,
            bool isAvailableForAssignment,
            int maxActiveShipments,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IdentityUserRoleCheckResponse> CheckUserRoleAsync(
            Guid userId,
            string role,
            CancellationToken cancellationToken = default)
        {
            if (!_users.TryGetValue(userId, out var user))
            {
                return Task.FromResult(new IdentityUserRoleCheckResponse(userId, false, false, false));
            }

            return Task.FromResult(new IdentityUserRoleCheckResponse(
                userId,
                true,
                user.IsActive,
                user.Roles.Contains(role)));
        }

        public Task<IReadOnlyList<IdentityUserWithRolesResponse>> ListUsersWithRolesAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ActiveShipperResponse>> GetActiveShippersAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<IdentityUserSummaryResponse>> GetUsersByIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public sealed record FakeUser(Guid UserId, bool IsActive, HashSet<string> Roles);
    }
}
