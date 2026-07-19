using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.AdminUsers.SetUserActiveStatus;
using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.CashOnDelivery.MarkCodSettled;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shops;
using MiniLogistics.Application.Shops.SetShopActiveStatus;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shipments.CancelShipmentAssignment;
using MiniLogistics.Application.Shipments.ReassignShipment;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.Users;
using MiniLogistics.Domain.ValueObjects;
using Xunit;

namespace MiniLogistics.Application.Tests;

public sealed class AdminAuditActionTests
{
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _operatorId = Guid.NewGuid();
    private readonly Guid _shipperId = Guid.NewGuid();
    private readonly Guid _alternateShipperId = Guid.NewGuid();
    private readonly Guid _shopOwnerId = Guid.NewGuid();

    [Fact]
    public async Task SetShopActiveStatus_WritesAdminAuditLog()
    {
        var shop = CreateShop();
        var auditService = new FakeAdminAuditService();
        var service = new SetShopActiveStatusService(
            new SetShopActiveStatusCommandValidator(),
            CreateIdentityService(),
            new FakeShopRepository([shop]),
            TestClock.Provider,
            auditService);

        var result = await service.SetAsync(new SetShopActiveStatusCommand(
            _adminId,
            shop.Id,
            false));

        Assert.True(result.IsSuccess, result.Error.Description);
        var auditEntry = Assert.Single(auditService.Entries);
        Assert.Equal(AdminAuditActions.ShopActiveStatusChanged, auditEntry.Action);
        Assert.Equal(AdminAuditTargetTypes.Shop, auditEntry.TargetType);
        Assert.Equal(shop.Id, auditEntry.TargetId);
    }

    [Fact]
    public async Task SetUserActiveStatus_WritesAdminAuditLog()
    {
        var auditService = new FakeAdminAuditService();
        var identityService = CreateIdentityService();
        var service = new SetUserActiveStatusService(
            new SetUserActiveStatusCommandValidator(),
            identityService,
            auditService);

        var result = await service.SetAsync(new SetUserActiveStatusCommand(
            _adminId,
            _shipperId,
            false));

        Assert.True(result.IsSuccess, result.Error.Description);
        var auditEntry = Assert.Single(auditService.Entries);
        Assert.Equal(AdminAuditActions.UserActiveStatusChanged, auditEntry.Action);
        Assert.Equal(AdminAuditTargetTypes.User, auditEntry.TargetType);
        Assert.Equal(_shipperId, auditEntry.TargetId);
        Assert.Equal(1, auditService.SaveChangesCount);
    }

    [Fact]
    public async Task MarkCodSettled_WritesAdminAuditLog()
    {
        var shipmentId = Guid.NewGuid();
        var codTransaction = CodTransaction.Create(shipmentId, new Money(100_000m), TestClock.UtcNow);
        var collectResult = codTransaction.MarkCollected(ShipmentStatus.Delivered, _shipperId, TestClock.UtcNow);
        Assert.True(collectResult.IsSuccess, collectResult.Error.Description);

        var auditService = new FakeAdminAuditService();
        var service = new MarkCodSettledService(
            new MarkCodSettledCommandValidator(),
            CreateIdentityService(),
            new FakeCodTransactionRepository([codTransaction]),
            TestClock.Provider,
            auditService);

        var result = await service.MarkSettledAsync(new MarkCodSettledCommand(
            shipmentId,
            _adminId));

        Assert.True(result.IsSuccess, result.Error.Description);
        var auditEntry = Assert.Single(auditService.Entries);
        Assert.Equal(AdminAuditActions.CodSettled, auditEntry.Action);
        Assert.Equal(AdminAuditTargetTypes.CodTransaction, auditEntry.TargetType);
        Assert.Equal(codTransaction.Id, auditEntry.TargetId);
    }

    [Fact]
    public async Task ReassignShipment_WritesAdminAuditLogAndKeepsSingleActiveAssignment()
    {
        var shipment = CreateAssignedShipment();
        var auditService = new FakeAdminAuditService();
        var shipmentRepository = new FakeShipmentRepository([shipment]);
        var service = new ReassignShipmentService(
            new ReassignShipmentCommandValidator(),
            CreateIdentityService(),
            shipmentRepository,
            TestClock.Provider,
            adminAuditService: auditService);

        var result = await service.ReassignAsync(new ReassignShipmentCommand(
            shipment.Id,
            _alternateShipperId,
            _operatorId,
            "Shipper unavailable."));

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal(ShipmentStatus.Assigned, shipment.Status);
        Assert.Single(shipment.Assignments, assignment => assignment.IsActive);
        Assert.Contains(shipment.Assignments, assignment => assignment.ShipperId == _shipperId && !assignment.IsActive);
        Assert.Contains(shipment.Assignments, assignment => assignment.ShipperId == _alternateShipperId && assignment.IsActive);
        Assert.Equal(1, shipmentRepository.SaveChangesCount);

        var auditEntry = Assert.Single(auditService.Entries);
        Assert.Equal(AdminAuditActions.ShipmentReassigned, auditEntry.Action);
        Assert.Equal(AdminAuditTargetTypes.Shipment, auditEntry.TargetType);
        Assert.Equal(shipment.Id, auditEntry.TargetId);
        Assert.Equal("Shipper unavailable.", auditEntry.Reason);
    }

    [Fact]
    public async Task CancelShipmentAssignment_WritesAdminAuditLogAndReturnsPendingPickup()
    {
        var shipment = CreateAssignedShipment();
        var auditService = new FakeAdminAuditService();
        var shipmentRepository = new FakeShipmentRepository([shipment]);
        var service = new CancelShipmentAssignmentService(
            new CancelShipmentAssignmentCommandValidator(),
            CreateIdentityService(),
            shipmentRepository,
            TestClock.Provider,
            adminAuditService: auditService);

        var result = await service.CancelAsync(new CancelShipmentAssignmentCommand(
            shipment.Id,
            _operatorId,
            "Need to dispatch later."));

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal(ShipmentStatus.PendingPickup, shipment.Status);
        Assert.DoesNotContain(shipment.Assignments, assignment => assignment.IsActive);
        Assert.Equal(1, shipmentRepository.SaveChangesCount);

        var auditEntry = Assert.Single(auditService.Entries);
        Assert.Equal(AdminAuditActions.ShipmentAssignmentCancelled, auditEntry.Action);
        Assert.Equal(AdminAuditTargetTypes.Shipment, auditEntry.TargetType);
        Assert.Equal(shipment.Id, auditEntry.TargetId);
        Assert.Equal("Need to dispatch later.", auditEntry.Reason);
    }

    [Theory]
    [InlineData(ShipmentStatus.PickingUp)]
    [InlineData(ShipmentStatus.PickedUp)]
    [InlineData(ShipmentStatus.InTransit)]
    [InlineData(ShipmentStatus.Delivering)]
    [InlineData(ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.DeliveryFailed)]
    [InlineData(ShipmentStatus.Returned)]
    [InlineData(ShipmentStatus.Cancelled)]
    public void ReassignShipment_RejectsStatusesAfterAssignedWindow(ShipmentStatus targetStatus)
    {
        var shipment = CreateAssignedShipment();
        MoveShipmentToStatus(shipment, targetStatus);

        var result = shipment.ReassignShipper(
            _alternateShipperId,
            _operatorId,
            TestClock.UtcNow,
            "Attempt after pickup flow started.");

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.CannotReassign.Code, result.Error.Code);
    }

    private Shop CreateShop()
    {
        return new Shop(
            _shopOwnerId,
            "Audit Shop",
            new PhoneNumber("0912345678"),
            new Address("1 Le Loi", "Ben Thanh", "Ho Chi Minh"),
            TestClock.UtcNow);
    }

    private FakeIdentityService CreateIdentityService()
    {
        return new FakeIdentityService([
            new FakeIdentityService.FakeUser(_adminId, true, [nameof(UserRole.Admin)]),
            new FakeIdentityService.FakeUser(_operatorId, true, [nameof(UserRole.Operator)]),
            new FakeIdentityService.FakeUser(_shipperId, true, [nameof(UserRole.Shipper)]),
            new FakeIdentityService.FakeUser(_alternateShipperId, true, [nameof(UserRole.Shipper)]),
            new FakeIdentityService.FakeUser(_shopOwnerId, true, [nameof(UserRole.Shop)])
        ]);
    }

    private Shipment CreateAssignedShipment()
    {
        var shipment = CreateShipment();
        var assignResult = shipment.AssignShipper(_shipperId, _operatorId, TestClock.UtcNow, "Assigned for audit test.");
        Assert.True(assignResult.IsSuccess, assignResult.Error.Description);
        return shipment;
    }

    private Shipment CreateShipment()
    {
        return Shipment.Create(
            Guid.NewGuid(),
            "Audit Sender",
            new PhoneNumber("0900000000"),
            "Audit Receiver",
            new PhoneNumber("0911111111"),
            new Address("1 Le Loi", "Ben Thanh", "Ho Chi Minh"),
            new Address("9 Hang Bai", "Hoan Kiem", "Ha Noi"),
            new Weight(1m),
            new ParcelDimensions(10m, 10m, 10m),
            new Weight(1m),
            new Money(500_000m),
            new Money(100_000m),
            new ShippingFeeBreakdown(new Money(20_000m), Money.Zero, Money.Zero, Money.Zero),
            RouteType.InterRegion,
            _shopOwnerId,
            TestClock.UtcNow);
    }

    private void MoveShipmentToStatus(Shipment shipment, ShipmentStatus targetStatus)
    {
        if (targetStatus == ShipmentStatus.Assigned)
        {
            return;
        }

        if (targetStatus == ShipmentStatus.Cancelled)
        {
            var cancelResult = shipment.UpdateStatus(ShipmentStatus.Cancelled, _operatorId, TestClock.UtcNow, "Cancelled.");
            Assert.True(cancelResult.IsSuccess, cancelResult.Error.Description);
            return;
        }

        foreach (var nextStatus in GetStatusPath(targetStatus))
        {
            var result = shipment.UpdateStatus(
                nextStatus,
                _operatorId,
                TestClock.UtcNow,
                nextStatus == ShipmentStatus.DeliveryFailed ? "Receiver unavailable." : null);
            Assert.True(result.IsSuccess, result.Error.Description);
        }
    }

    private static IReadOnlyList<ShipmentStatus> GetStatusPath(ShipmentStatus targetStatus)
    {
        return targetStatus switch
        {
            ShipmentStatus.PickingUp => [ShipmentStatus.PickingUp],
            ShipmentStatus.PickedUp => [ShipmentStatus.PickingUp, ShipmentStatus.PickedUp],
            ShipmentStatus.InTransit => [ShipmentStatus.PickingUp, ShipmentStatus.PickedUp, ShipmentStatus.InTransit],
            ShipmentStatus.Delivering => [ShipmentStatus.PickingUp, ShipmentStatus.PickedUp, ShipmentStatus.InTransit, ShipmentStatus.Delivering],
            ShipmentStatus.Delivered => [ShipmentStatus.PickingUp, ShipmentStatus.PickedUp, ShipmentStatus.InTransit, ShipmentStatus.Delivering, ShipmentStatus.Delivered],
            ShipmentStatus.DeliveryFailed => [ShipmentStatus.PickingUp, ShipmentStatus.PickedUp, ShipmentStatus.InTransit, ShipmentStatus.Delivering, ShipmentStatus.DeliveryFailed],
            ShipmentStatus.Returned => [ShipmentStatus.PickingUp, ShipmentStatus.PickedUp, ShipmentStatus.InTransit, ShipmentStatus.Returned],
            _ => throw new ArgumentOutOfRangeException(nameof(targetStatus), targetStatus, "Unsupported target status.")
        };
    }

    private sealed class FakeAdminAuditService : IAdminAuditService
    {
        public List<AdminAuditEntry> Entries { get; } = [];

        public int SaveChangesCount { get; private set; }

        public Task RecordAsync(AdminAuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
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

        public Task<Result> SetUserActiveStatusAsync(
            Guid userId,
            bool isActive,
            CancellationToken cancellationToken = default)
        {
            _users[userId] = _users[userId] with { IsActive = isActive };
            return Task.FromResult(Result.Success());
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

        public Task<IReadOnlyList<IdentityUserWithRolesResponse>> ListUsersWithRolesAsync(
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ActiveShipperResponse>> GetActiveShippersAsync(
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<IdentityUserSummaryResponse>> GetUsersByIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<IdentityUserSummaryResponse> response = userIds
                .Where(_users.ContainsKey)
                .Select(userId => _users[userId])
                .Select(user => new IdentityUserSummaryResponse(
                    user.UserId,
                    "Audit User",
                    "audit@example.test",
                    null,
                    user.IsActive,
                    true,
                    30))
                .ToList();

            return Task.FromResult(response);
        }

        public sealed record FakeUser(Guid UserId, bool IsActive, HashSet<string> Roles);
    }

    private sealed class FakeShopRepository : IShopRepository
    {
        private readonly List<Shop> _shops;

        public FakeShopRepository(IReadOnlyList<Shop> shops)
        {
            _shops = shops.ToList();
        }

        public Task<Shop?> GetByIdAsync(Guid shopId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shops.FirstOrDefault(shop => shop.Id == shopId));
        }

        public Task<Shop?> GetByOwnerUserIdAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shops.FirstOrDefault(shop => shop.OwnerUserId == ownerUserId));
        }

        public Task<IReadOnlyList<Shop>> GetAllByOwnerUserIdAsync(
            Guid ownerUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shop>>(_shops
                .Where(shop => shop.OwnerUserId == ownerUserId)
                .ToList());
        }

        public Task<IReadOnlyList<Shop>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shop>>(_shops.ToList());
        }

        public Task<bool> ExistsByOwnerUserIdAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shops.Any(shop => shop.OwnerUserId == ownerUserId));
        }

        public Task AddAsync(Shop shop, CancellationToken cancellationToken = default)
        {
            _shops.Add(shop);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeShipmentRepository : IShipmentRepository
    {
        private readonly List<Shipment> _shipments;

        public FakeShipmentRepository(IReadOnlyList<Shipment> shipments)
        {
            _shipments = shipments.ToList();
        }

        public int SaveChangesCount { get; private set; }

        public Task<bool> ExistsByTrackingCodeAsync(
            TrackingCode trackingCode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shipments.Any(shipment => shipment.TrackingCode == trackingCode));
        }

        public Task<IReadOnlyList<Shipment>> GetByShopIdAsync(
            Guid shopId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shipment>>(_shipments
                .Where(shipment => shipment.ShopId == shopId)
                .ToList());
        }

        public Task<IReadOnlyList<Shipment>> GetByStatusAsync(
            ShipmentStatus status,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shipment>>(_shipments
                .Where(shipment => shipment.Status == status)
                .ToList());
        }

        public Task<IReadOnlyList<Shipment>> GetByStatusesAsync(
            IReadOnlyCollection<ShipmentStatus> statuses,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shipment>>(_shipments
                .Where(shipment => statuses.Contains(shipment.Status))
                .ToList());
        }

        public Task<IReadOnlyList<Shipment>> GetByIdsAsync(
            IReadOnlyCollection<Guid> shipmentIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shipment>>(_shipments
                .Where(shipment => shipmentIds.Contains(shipment.Id))
                .ToList());
        }

        public Task<IReadOnlyList<Shipment>> GetAssignedToShipperAsync(
            Guid shipperId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shipment>>(_shipments
                .Where(shipment => shipment.Assignments.Any(assignment =>
                    assignment.IsActive && assignment.ShipperId == shipperId))
                .ToList());
        }

        public Task<IReadOnlyDictionary<Guid, int>> GetActiveAssignmentCountsByShipperIdsAsync(
            IReadOnlyCollection<Guid> shipperIds,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<Guid, int> counts = _shipments
                .Where(shipment => ShipmentLoadStatuses.ActiveAssignmentStatuses.Contains(shipment.Status))
                .SelectMany(shipment => shipment.Assignments.Where(assignment =>
                    assignment.IsActive && shipperIds.Contains(assignment.ShipperId)))
                .GroupBy(assignment => assignment.ShipperId)
                .ToDictionary(group => group.Key, group => group.Count());

            return Task.FromResult(counts);
        }

        public Task<Shipment?> GetByIdAndShopIdAsync(
            Guid shipmentId,
            Guid shopId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shipments.FirstOrDefault(shipment =>
                shipment.Id == shipmentId && shipment.ShopId == shopId));
        }

        public Task<Shipment?> GetTrackedByIdAndShopIdAsync(
            Guid shipmentId,
            Guid shopId,
            CancellationToken cancellationToken = default)
        {
            return GetByIdAndShopIdAsync(shipmentId, shopId, cancellationToken);
        }

        public Task<Shipment?> GetTrackedByIdAsync(
            Guid shipmentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shipments.FirstOrDefault(shipment => shipment.Id == shipmentId));
        }

        public Task<Shipment?> GetByTrackingCodeAsync(
            TrackingCode trackingCode,
            CancellationToken cancellationToken = default)
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
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCodTransactionRepository : ICodTransactionRepository
    {
        private readonly List<CodTransaction> _codTransactions;

        public FakeCodTransactionRepository(IReadOnlyList<CodTransaction> codTransactions)
        {
            _codTransactions = codTransactions.ToList();
        }

        public Task<IReadOnlyList<CodTransaction>> GetByStatusesAsync(
            IReadOnlyCollection<CodStatus> statuses,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CodTransaction>>(_codTransactions
                .Where(codTransaction => statuses.Contains(codTransaction.Status))
                .ToList());
        }

        public Task<CodTransaction?> GetByShipmentIdAsync(
            Guid shipmentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_codTransactions.FirstOrDefault(item => item.ShipmentId == shipmentId));
        }

        public Task<CodTransaction?> GetTrackedByShipmentIdAsync(
            Guid shipmentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_codTransactions.FirstOrDefault(item => item.ShipmentId == shipmentId));
        }

        public Task AddAsync(CodTransaction codTransaction, CancellationToken cancellationToken = default)
        {
            _codTransactions.Add(codTransaction);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
