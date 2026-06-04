using FluentValidation;
using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.CashOnDelivery.MarkCodCollected;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shipments.AssignShipperToShipment;
using MiniLogistics.Application.Shipments.GetAssignedShipmentsForShipper;
using MiniLogistics.Application.Shipments.UpdateShipmentStatus;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Users;
using MiniLogistics.Domain.ValueObjects;
using Xunit;

namespace MiniLogistics.Application.Tests;

public sealed class ShipmentBusinessRuleTests
{
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _operatorId = Guid.NewGuid();
    private readonly Guid _shopUserId = Guid.NewGuid();
    private readonly Guid _shipperId = Guid.NewGuid();
    private readonly Guid _otherShipperId = Guid.NewGuid();

    [Fact]
    public async Task AssignShipper_ShopUser_IsRejected()
    {
        var shipment = CreateShipment(_shopUserId);
        var service = CreateAssignService([shipment]);

        var result = await service.AssignAsync(new AssignShipperCommand(
            shipment.Id,
            _shipperId,
            _shopUserId,
            "Shop tries to assign."));

        Assert.True(result.IsFailure);
        Assert.Equal("Application.Forbidden", result.Error.Code);
    }

    [Theory]
    [InlineData(nameof(UserRole.Admin))]
    [InlineData(nameof(UserRole.Operator))]
    public async Task AssignShipper_AdminOrOperator_AssignsShipment(string assigningRole)
    {
        var shipment = CreateShipment(_shopUserId);
        var assigningUserId = assigningRole == nameof(UserRole.Admin) ? _adminId : _operatorId;
        var repository = new FakeShipmentRepository([shipment]);
        var service = CreateAssignService(repository);

        var result = await service.AssignAsync(new AssignShipperCommand(
            shipment.Id,
            _shipperId,
            assigningUserId,
            "Assign for delivery."));

        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.Assigned, shipment.Status);
        Assert.Contains(shipment.Assignments, assignment => assignment.ShipperId == _shipperId && assignment.IsActive);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task AssignShipper_TargetUserWithoutShipperRole_IsRejected()
    {
        var shipment = CreateShipment(_shopUserId);
        var service = CreateAssignService([shipment]);

        var result = await service.AssignAsync(new AssignShipperCommand(
            shipment.Id,
            _shopUserId,
            _operatorId,
            "Assign to wrong role."));

        Assert.True(result.IsFailure);
        Assert.Equal("Application.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task UpdateShipmentStatus_ShipperCannotUpdateShipmentWithoutActiveAssignment()
    {
        var shipment = CreateAssignedShipment(_shipperId);
        var service = CreateUpdateStatusService([shipment]);

        var result = await service.UpdateAsync(new UpdateShipmentStatusCommand(
            shipment.Id,
            _otherShipperId,
            ShipmentStatus.PickingUp,
            "Trying another shipper's shipment."));

        Assert.True(result.IsFailure);
        Assert.Equal("Application.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task UpdateShipmentStatus_InvalidTransition_IsRejected()
    {
        var shipment = CreateAssignedShipment(_shipperId);
        var service = CreateUpdateStatusService([shipment]);

        var result = await service.UpdateAsync(new UpdateShipmentStatusCommand(
            shipment.Id,
            _operatorId,
            ShipmentStatus.InTransit,
            "Skip pickup."));

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.InvalidStatusTransition.Code, result.Error.Code);
    }

    [Fact]
    public async Task UpdateShipmentStatus_DeliveryFailedWithoutNote_IsRejected()
    {
        var shipment = CreateShipmentAtStatus(ShipmentStatus.Delivering, codAmount: 100_000m);
        var service = CreateUpdateStatusService([shipment]);

        var result = await service.UpdateAsync(new UpdateShipmentStatusCommand(
            shipment.Id,
            _operatorId,
            ShipmentStatus.DeliveryFailed,
            null));

        Assert.True(result.IsFailure);
        Assert.Equal("Application.ValidationFailed", result.Error.Code);
    }

    [Fact]
    public async Task MarkCodCollected_ShipmentNotDelivered_IsRejected()
    {
        var shipment = CreateAssignedShipment(_shipperId, codAmount: 100_000m);
        var codTransaction = CodTransaction.Create(shipment.Id, new Money(100_000m));
        var service = CreateMarkCodCollectedService([shipment], [codTransaction]);

        var result = await service.MarkCollectedAsync(new MarkCodCollectedCommand(
            shipment.Id,
            _shipperId));

        Assert.True(result.IsFailure);
        Assert.Equal(CodErrors.ShipmentMustBeDelivered.Code, result.Error.Code);
    }

    [Fact]
    public async Task MarkCodCollected_WhenCodIsNotPendingCollection_IsRejected()
    {
        var shipment = CreateShipmentAtStatus(ShipmentStatus.Delivered, codAmount: 0m);
        var codTransaction = CodTransaction.Create(shipment.Id, Money.Zero);
        var service = CreateMarkCodCollectedService([shipment], [codTransaction]);

        var result = await service.MarkCollectedAsync(new MarkCodCollectedCommand(
            shipment.Id,
            _operatorId));

        Assert.True(result.IsFailure);
        Assert.Equal(CodErrors.CollectionNotRequired.Code, result.Error.Code);
    }

    [Fact]
    public async Task MarkCodCollected_AssignedShipperCanCollectDeliveredPendingCod()
    {
        var shipment = CreateShipmentAtStatus(ShipmentStatus.Delivered, codAmount: 100_000m);
        var codTransaction = CodTransaction.Create(shipment.Id, new Money(100_000m));
        var repository = new FakeCodTransactionRepository([codTransaction]);
        var service = CreateMarkCodCollectedService(new FakeShipmentRepository([shipment]), repository);

        var result = await service.MarkCollectedAsync(new MarkCodCollectedCommand(
            shipment.Id,
            _shipperId));

        Assert.True(result.IsSuccess);
        Assert.Equal(CodStatus.Collected, codTransaction.Status);
        Assert.Equal(_shipperId, codTransaction.CollectedByUserId);
        Assert.DoesNotContain(shipment.Assignments, assignment => assignment.IsActive);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public void UpdateShipmentStatus_DeliveredWithPendingCod_KeepsAssignmentActive()
    {
        var shipment = CreateShipmentAtStatus(ShipmentStatus.Delivered, codAmount: 100_000m);

        Assert.Contains(shipment.Assignments, assignment => assignment.IsActive && assignment.ShipperId == _shipperId);
    }

    [Fact]
    public void UpdateShipmentStatus_Returned_DeactivatesAssignment()
    {
        var shipment = CreateShipmentAtStatus(ShipmentStatus.Delivering, codAmount: 100_000m);
        var result = shipment.UpdateStatus(ShipmentStatus.Returned, _operatorId, "Recipient refused parcel.");

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(shipment.Assignments, assignment => assignment.IsActive);
    }

    [Fact]
    public async Task GetAssignedShipments_DeliveredWithPendingCod_IsVisibleToShipper()
    {
        var shipment = CreateShipmentAtStatus(ShipmentStatus.Delivered, codAmount: 100_000m);
        var codTransaction = CodTransaction.Create(shipment.Id, new Money(100_000m));
        var service = CreateGetAssignedShipmentsForShipperService([shipment], [codTransaction]);

        var result = await service.GetAsync(_shipperId);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value, response => response.ShipmentId == shipment.Id);
    }

    [Fact]
    public async Task GetAssignedShipments_DeliveredWithCollectedCod_IsHiddenFromShipper()
    {
        var shipment = CreateShipmentAtStatus(ShipmentStatus.Delivered, codAmount: 100_000m);
        var codTransaction = CodTransaction.Create(shipment.Id, new Money(100_000m));
        var collectResult = codTransaction.MarkCollected(shipment.Status, _shipperId);
        Assert.True(collectResult.IsSuccess);

        var service = CreateGetAssignedShipmentsForShipperService([shipment], [codTransaction]);

        var result = await service.GetAsync(_shipperId);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Value, response => response.ShipmentId == shipment.Id);
    }

    private AssignShipperToShipmentService CreateAssignService(IReadOnlyList<Shipment> shipments)
    {
        return CreateAssignService(new FakeShipmentRepository(shipments));
    }

    private AssignShipperToShipmentService CreateAssignService(FakeShipmentRepository shipmentRepository)
    {
        return new AssignShipperToShipmentService(
            new AssignShipperCommandValidator(),
            CreateIdentityService(),
            shipmentRepository);
    }

    private UpdateShipmentStatusService CreateUpdateStatusService(IReadOnlyList<Shipment> shipments)
    {
        return new UpdateShipmentStatusService(
            new UpdateShipmentStatusCommandValidator(),
            CreateIdentityService(),
            new FakeShipmentRepository(shipments));
    }

    private MarkCodCollectedService CreateMarkCodCollectedService(
        IReadOnlyList<Shipment> shipments,
        IReadOnlyList<CodTransaction> codTransactions)
    {
        return CreateMarkCodCollectedService(
            new FakeShipmentRepository(shipments),
            new FakeCodTransactionRepository(codTransactions));
    }

    private MarkCodCollectedService CreateMarkCodCollectedService(
        FakeShipmentRepository shipmentRepository,
        FakeCodTransactionRepository codTransactionRepository)
    {
        return new MarkCodCollectedService(
            new MarkCodCollectedCommandValidator(),
            CreateIdentityService(),
            shipmentRepository,
            codTransactionRepository);
    }

    private GetAssignedShipmentsForShipperService CreateGetAssignedShipmentsForShipperService(
        IReadOnlyList<Shipment> shipments,
        IReadOnlyList<CodTransaction> codTransactions)
    {
        return new GetAssignedShipmentsForShipperService(
            CreateIdentityService(),
            new FakeShipmentRepository(shipments),
            new FakeCodTransactionRepository(codTransactions));
    }

    private FakeIdentityService CreateIdentityService()
    {
        var identityService = new FakeIdentityService();
        identityService.AddUser(_adminId, true, nameof(UserRole.Admin));
        identityService.AddUser(_operatorId, true, nameof(UserRole.Operator));
        identityService.AddUser(_shopUserId, true, nameof(UserRole.Shop));
        identityService.AddUser(_shipperId, true, nameof(UserRole.Shipper));
        identityService.AddUser(_otherShipperId, true, nameof(UserRole.Shipper));
        return identityService;
    }

    private Shipment CreateAssignedShipment(Guid shipperId, decimal codAmount = 100_000m)
    {
        var shipment = CreateShipment(_shopUserId, codAmount);
        var assignResult = shipment.AssignShipper(shipperId, _operatorId, "Assigned for test.");
        Assert.True(assignResult.IsSuccess);
        return shipment;
    }

    private Shipment CreateShipmentAtStatus(ShipmentStatus targetStatus, decimal codAmount)
    {
        var shipment = CreateAssignedShipment(_shipperId, codAmount);
        MoveShipmentToStatus(shipment, targetStatus);
        return shipment;
    }

    private void MoveShipmentToStatus(Shipment shipment, ShipmentStatus targetStatus)
    {
        var transitions = new[]
        {
            ShipmentStatus.PickingUp,
            ShipmentStatus.PickedUp,
            ShipmentStatus.InTransit,
            ShipmentStatus.Delivering,
            ShipmentStatus.Delivered
        };

        foreach (var status in transitions)
        {
            if (shipment.Status == targetStatus)
            {
                return;
            }

            var result = shipment.UpdateStatus(status, _shipperId, $"Move to {status}.");
            Assert.True(result.IsSuccess);
        }

        Assert.Equal(targetStatus, shipment.Status);
    }

    private static Shipment CreateShipment(Guid createdByUserId, decimal codAmount = 100_000m)
    {
        return Shipment.Create(
            Guid.NewGuid(),
            "Shop Demo",
            new PhoneNumber("0900000000"),
            "Customer Demo",
            new PhoneNumber("0911111111"),
            new Address("1 Nguyen Trai", "Ben Thanh", "Ho Chi Minh"),
            new Address("9 Le Loi", "Ben Nghe", "Ho Chi Minh"),
            new Weight(1m),
            new ParcelDimensions(10m, 10m, 10m),
            new Weight(1m),
            new Money(500_000m),
            new Money(codAmount),
            new ShippingFeeBreakdown(new Money(20_000m), Money.Zero, Money.Zero, Money.Zero),
            RouteType.IntraProvince,
            createdByUserId,
            "Test shipment.");
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        private readonly Dictionary<Guid, FakeUser> _users = [];

        public void AddUser(Guid userId, bool isActive, params string[] roles)
        {
            _users[userId] = new FakeUser(userId, $"User {userId.ToString()[..8]}", $"{userId:N}@example.test", null, isActive, roles.ToHashSet());
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

        public Task<Result> AddToRoleAsync(
            Guid userId,
            string role,
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

        public Task<IReadOnlyList<ActiveShipperResponse>> GetActiveShippersAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ActiveShipperResponse> shippers = _users.Values
                .Where(user => user.IsActive && user.Roles.Contains(nameof(UserRole.Shipper)))
                .Select(user => new ActiveShipperResponse(user.UserId, user.FullName, user.Email, user.PhoneNumber))
                .ToList();

            return Task.FromResult(shippers);
        }

        public Task<IReadOnlyList<IdentityUserSummaryResponse>> GetUsersByIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<IdentityUserSummaryResponse> users = userIds
                .Where(_users.ContainsKey)
                .Select(userId => _users[userId])
                .Select(user => new IdentityUserSummaryResponse(user.UserId, user.FullName, user.Email, user.PhoneNumber, user.IsActive))
                .ToList();

            return Task.FromResult(users);
        }

        private sealed record FakeUser(
            Guid UserId,
            string FullName,
            string Email,
            string? PhoneNumber,
            bool IsActive,
            HashSet<string> Roles);
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
            return Task.FromResult<IReadOnlyList<Shipment>>(_shipments.Where(shipment => shipment.ShopId == shopId).ToList());
        }

        public Task<IReadOnlyList<Shipment>> GetByStatusAsync(
            ShipmentStatus status,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shipment>>(_shipments.Where(shipment => shipment.Status == status).ToList());
        }

        public Task<IReadOnlyList<Shipment>> GetByStatusesAsync(
            IReadOnlyCollection<ShipmentStatus> statuses,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shipment>>(_shipments.Where(shipment => statuses.Contains(shipment.Status)).ToList());
        }

        public Task<IReadOnlyList<Shipment>> GetAssignedToShipperAsync(
            Guid shipperId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shipment>>(_shipments
                .Where(shipment => shipment.Assignments.Any(assignment => assignment.IsActive && assignment.ShipperId == shipperId))
                .Where(shipment => shipment.Status is not ShipmentStatus.Returned and not ShipmentStatus.Cancelled)
                .ToList());
        }

        public Task<Shipment?> GetByIdAndShopIdAsync(
            Guid shipmentId,
            Guid shopId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shipments.FirstOrDefault(shipment => shipment.Id == shipmentId && shipment.ShopId == shopId));
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

        public int SaveChangesCount { get; private set; }

        public Task<CodTransaction?> GetByShipmentIdAsync(
            Guid shipmentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_codTransactions.FirstOrDefault(codTransaction => codTransaction.ShipmentId == shipmentId));
        }

        public Task<CodTransaction?> GetTrackedByShipmentIdAsync(
            Guid shipmentId,
            CancellationToken cancellationToken = default)
        {
            return GetByShipmentIdAsync(shipmentId, cancellationToken);
        }

        public Task AddAsync(CodTransaction codTransaction, CancellationToken cancellationToken = default)
        {
            _codTransactions.Add(codTransaction);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }
}
