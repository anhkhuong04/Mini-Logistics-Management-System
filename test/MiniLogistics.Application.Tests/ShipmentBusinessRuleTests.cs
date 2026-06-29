using FluentValidation;
using MiniLogistics.Application.AdminUsers.CreateInternalUser;
using MiniLogistics.Application.AdminUsers.GetAdminUsers;
using MiniLogistics.Application.AdminUsers.SetUserActiveStatus;
using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.CashOnDelivery.GetCodSettlementCandidates;
using MiniLogistics.Application.CashOnDelivery.MarkCodCollected;
using MiniLogistics.Application.CashOnDelivery.MarkCodSettled;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shipments.CancelShipmentForCurrentShop;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shipments.AssignShipperToShipment;
using MiniLogistics.Application.Shipments.GetAssignedShipmentsForShipper;
using MiniLogistics.Application.Shipments.GetOperationsShipments;
using MiniLogistics.Application.Shipments.GetPublicTracking;
using MiniLogistics.Application.Shipments.UpdateShipmentStatus;
using MiniLogistics.Application.Shippers.GetActiveShippers;
using MiniLogistics.Application.Shops;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;
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
    public async Task CreateInternalUser_AdminCreatesActiveShipperVisibleForAssignment()
    {
        var identityService = CreateIdentityService();
        var createService = new CreateInternalUserService(
            new CreateInternalUserCommandValidator(),
            identityService);
        var listService = new GetAdminUsersService(identityService);
        var shippersService = new GetActiveShippersService(identityService);

        var result = await createService.CreateAsync(new CreateInternalUserCommand(
            _adminId,
            "New Demo Shipper",
            "new.shipper@example.test",
            "0900000099",
            "Password1",
            nameof(UserRole.Shipper)));

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(UserRole.Shipper), result.Value.Role);

        var usersResult = await listService.GetAsync(_adminId);
        Assert.True(usersResult.IsSuccess);
        Assert.Contains(usersResult.Value, user =>
            user.UserId == result.Value.UserId
            && user.IsActive
            && user.Roles.Contains(nameof(UserRole.Shipper)));

        var shippersResult = await shippersService.GetAsync();
        Assert.True(shippersResult.IsSuccess);
        Assert.Contains(shippersResult.Value, shipper => shipper.UserId == result.Value.UserId);
    }

    [Fact]
    public async Task CreateInternalUser_NonAdminUser_IsRejected()
    {
        var identityService = CreateIdentityService();
        var createService = new CreateInternalUserService(
            new CreateInternalUserCommandValidator(),
            identityService);

        var result = await createService.CreateAsync(new CreateInternalUserCommand(
            _operatorId,
            "Blocked Shipper",
            "blocked.shipper@example.test",
            "0900000098",
            "Password1",
            nameof(UserRole.Shipper)));

        Assert.True(result.IsFailure);
        Assert.Equal("Application.Forbidden", result.Error.Code);
        Assert.DoesNotContain(
            await identityService.ListUsersWithRolesAsync(),
            user => user.Email == "blocked.shipper@example.test");
    }

    [Fact]
    public async Task SetUserActiveStatus_DeactivatedShipperDisappearsAndCannotBeAssigned()
    {
        var identityService = CreateIdentityService();
        var managedShipperId = Guid.NewGuid();
        identityService.AddUser(managedShipperId, true, nameof(UserRole.Shipper));
        var setActiveService = new SetUserActiveStatusService(
            new SetUserActiveStatusCommandValidator(),
            identityService);

        var deactivateResult = await setActiveService.SetAsync(new SetUserActiveStatusCommand(
            _adminId,
            managedShipperId,
            false));

        Assert.True(deactivateResult.IsSuccess);

        var shippersResult = await new GetActiveShippersService(identityService).GetAsync();
        Assert.True(shippersResult.IsSuccess);
        Assert.DoesNotContain(shippersResult.Value, shipper => shipper.UserId == managedShipperId);

        var shipment = CreateShipment(_shopUserId);
        var assignService = new AssignShipperToShipmentService(
            new AssignShipperCommandValidator(),
            identityService,
            new FakeShipmentRepository([shipment]));

        var assignResult = await assignService.AssignAsync(new AssignShipperCommand(
            shipment.Id,
            managedShipperId,
            _adminId,
            "Assign to inactive shipper."));

        Assert.True(assignResult.IsFailure);
        Assert.Equal("Application.Forbidden", assignResult.Error.Code);
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
    public async Task MarkCodSettled_AdminCanSettleCollectedCod()
    {
        var shipment = CreateShipmentAtStatus(ShipmentStatus.Delivered, codAmount: 100_000m);
        var codTransaction = CodTransaction.Create(shipment.Id, new Money(100_000m));
        var collectResult = codTransaction.MarkCollected(shipment.Status, _shipperId);
        var repository = new FakeCodTransactionRepository([codTransaction]);
        var service = CreateMarkCodSettledService(repository);

        var result = await service.MarkSettledAsync(new MarkCodSettledCommand(
            shipment.Id,
            _adminId));

        Assert.True(collectResult.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.Equal(CodStatus.Settled, codTransaction.Status);
        Assert.Equal(_adminId, codTransaction.SettledByUserId);
        Assert.NotNull(codTransaction.SettledAtUtc);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Theory]
    [InlineData(nameof(UserRole.Operator))]
    [InlineData(nameof(UserRole.Shipper))]
    public async Task MarkCodSettled_NonAdminUser_IsRejected(string role)
    {
        var settledByUserId = role == nameof(UserRole.Operator) ? _operatorId : _shipperId;
        var shipment = CreateShipmentAtStatus(ShipmentStatus.Delivered, codAmount: 100_000m);
        var codTransaction = CodTransaction.Create(shipment.Id, new Money(100_000m));
        var collectResult = codTransaction.MarkCollected(shipment.Status, _shipperId);
        var repository = new FakeCodTransactionRepository([codTransaction]);
        var service = CreateMarkCodSettledService(repository);

        var result = await service.MarkSettledAsync(new MarkCodSettledCommand(
            shipment.Id,
            settledByUserId));

        Assert.True(collectResult.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("Application.Forbidden", result.Error.Code);
        Assert.Equal(CodStatus.Collected, codTransaction.Status);
        Assert.Equal(0, repository.SaveChangesCount);
    }

    [Theory]
    [InlineData(CodStatus.PendingCollection)]
    [InlineData(CodStatus.NotRequired)]
    [InlineData(CodStatus.Settled)]
    public async Task MarkCodSettled_WhenCodIsNotCollected_IsRejected(CodStatus initialStatus)
    {
        var shipment = CreateShipmentAtStatus(ShipmentStatus.Delivered, codAmount: initialStatus == CodStatus.NotRequired ? 0m : 100_000m);
        var codTransaction = CodTransaction.Create(shipment.Id, new Money(initialStatus == CodStatus.NotRequired ? 0m : 100_000m));

        if (initialStatus == CodStatus.Settled)
        {
            var collectResult = codTransaction.MarkCollected(shipment.Status, _shipperId);
            var settleResult = codTransaction.MarkSettled(_adminId);
            Assert.True(collectResult.IsSuccess);
            Assert.True(settleResult.IsSuccess);
        }

        var repository = new FakeCodTransactionRepository([codTransaction]);
        var service = CreateMarkCodSettledService(repository);

        var result = await service.MarkSettledAsync(new MarkCodSettledCommand(
            shipment.Id,
            _adminId));

        Assert.True(result.IsFailure);
        Assert.Equal(CodErrors.CannotSettle.Code, result.Error.Code);
        Assert.Equal(initialStatus, codTransaction.Status);
        Assert.Equal(0, repository.SaveChangesCount);
    }

    [Fact]
    public async Task GetCodSettlementCandidates_ReturnsOnlyCollectedCodWithCollector()
    {
        var collectedShipment = CreateShipmentAtStatus(ShipmentStatus.Delivered, codAmount: 100_000m);
        var pendingShipment = CreateShipmentAtStatus(ShipmentStatus.Delivered, codAmount: 200_000m);
        var settledShipment = CreateShipmentAtStatus(ShipmentStatus.Delivered, codAmount: 300_000m);
        var collectedCod = CodTransaction.Create(collectedShipment.Id, new Money(100_000m));
        var pendingCod = CodTransaction.Create(pendingShipment.Id, new Money(200_000m));
        var settledCod = CodTransaction.Create(settledShipment.Id, new Money(300_000m));
        var collectResult = collectedCod.MarkCollected(collectedShipment.Status, _shipperId);
        var settledCollectResult = settledCod.MarkCollected(settledShipment.Status, _shipperId);
        var settleResult = settledCod.MarkSettled(_adminId);
        var service = new GetCodSettlementCandidatesService(
            new FakeCodTransactionRepository([collectedCod, pendingCod, settledCod]),
            new FakeShipmentRepository([collectedShipment, pendingShipment, settledShipment]),
            CreateIdentityService());

        var result = await service.GetAsync();

        Assert.True(collectResult.IsSuccess);
        Assert.True(settledCollectResult.IsSuccess);
        Assert.True(settleResult.IsSuccess);
        Assert.True(result.IsSuccess);
        var candidate = Assert.Single(result.Value);
        Assert.Equal(collectedShipment.Id, candidate.ShipmentId);
        Assert.Equal(collectedShipment.TrackingCode.Value, candidate.TrackingCode);
        Assert.Equal(100_000m, candidate.CodAmount);
        Assert.Equal(CodStatus.Collected, candidate.CodStatus);
        Assert.Equal(_shipperId, candidate.CollectedByUserId);
        Assert.Equal(FormatFakeUserName(_shipperId), candidate.CollectedByName);
        Assert.Equal(FormatFakeUserEmail(_shipperId), candidate.CollectedByEmail);
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

    [Fact]
    public async Task GetOperationsShipments_FiltersTerminalAndCodCompletedShipments()
    {
        var activeShipment = CreateShipmentAtStatus(ShipmentStatus.InTransit, codAmount: 0m);
        var deliveredPendingCodShipment = CreateShipmentAtStatus(ShipmentStatus.Delivered, codAmount: 100_000m);
        var deliveredCollectedCodShipment = CreateShipmentAtStatus(ShipmentStatus.Delivered, codAmount: 100_000m);
        var returnedShipment = CreateShipmentAtStatus(ShipmentStatus.Delivering, codAmount: 100_000m);
        var cancelledShipment = CreateAssignedShipment(_shipperId, codAmount: 100_000m);
        var returnedResult = returnedShipment.UpdateStatus(ShipmentStatus.Returned, _operatorId, "Recipient refused parcel.");
        var cancelledResult = cancelledShipment.Cancel(_shopUserId, "Shop cancelled before pickup.");
        var deliveredPendingCod = CodTransaction.Create(deliveredPendingCodShipment.Id, new Money(100_000m));
        var deliveredCollectedCod = CodTransaction.Create(deliveredCollectedCodShipment.Id, new Money(100_000m));
        var collectedResult = deliveredCollectedCod.MarkCollected(deliveredCollectedCodShipment.Status, _shipperId);
        var service = new GetOperationsShipmentsService(
            new FakeShipmentRepository([
                activeShipment,
                deliveredPendingCodShipment,
                deliveredCollectedCodShipment,
                returnedShipment,
                cancelledShipment
            ]),
            new FakeCodTransactionRepository([
                deliveredPendingCod,
                deliveredCollectedCod,
                CodTransaction.Create(activeShipment.Id, Money.Zero),
                CodTransaction.Create(returnedShipment.Id, new Money(100_000m)),
                CodTransaction.Create(cancelledShipment.Id, new Money(100_000m))
            ]),
            CreateIdentityService());

        Assert.True(returnedResult.IsSuccess);
        Assert.True(cancelledResult.IsSuccess);
        Assert.True(collectedResult.IsSuccess);

        var result = await service.GetAsync();

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value, response => response.ShipmentId == activeShipment.Id);
        Assert.Contains(result.Value, response =>
            response.ShipmentId == deliveredPendingCodShipment.Id
            && response.CodStatus == CodStatus.PendingCollection);
        Assert.DoesNotContain(result.Value, response => response.ShipmentId == deliveredCollectedCodShipment.Id);
        Assert.DoesNotContain(result.Value, response => response.ShipmentId == returnedShipment.Id);
        Assert.DoesNotContain(result.Value, response => response.ShipmentId == cancelledShipment.Id);
    }

    [Fact]
    public async Task GetAssignedShipments_AfterMarkCodCollectedService_HidesShipmentFromShipper()
    {
        var shipment = CreateShipmentAtStatus(ShipmentStatus.Delivered, codAmount: 100_000m);
        var codTransaction = CodTransaction.Create(shipment.Id, new Money(100_000m));
        var shipmentRepository = new FakeShipmentRepository([shipment]);
        var codTransactionRepository = new FakeCodTransactionRepository([codTransaction]);
        var collectService = CreateMarkCodCollectedService(shipmentRepository, codTransactionRepository);
        var getAssignedService = new GetAssignedShipmentsForShipperService(
            CreateIdentityService(),
            shipmentRepository,
            codTransactionRepository);

        var collectResult = await collectService.MarkCollectedAsync(new MarkCodCollectedCommand(
            shipment.Id,
            _shipperId));
        var assignedResult = await getAssignedService.GetAsync(_shipperId);

        Assert.True(collectResult.IsSuccess);
        Assert.Equal(CodStatus.Collected, codTransaction.Status);
        Assert.DoesNotContain(shipment.Assignments, assignment => assignment.IsActive);
        Assert.True(assignedResult.IsSuccess);
        Assert.DoesNotContain(assignedResult.Value, response => response.ShipmentId == shipment.Id);
    }

    [Theory]
    [InlineData(ShipmentStatus.Assigned)]
    [InlineData(ShipmentStatus.PickingUp)]
    public async Task CancelShipmentForCurrentShop_AssignedOrPickingUp_DeactivatesActiveAssignment(ShipmentStatus statusBeforeCancel)
    {
        var shop = CreateShop(_shopUserId);
        var shipment = CreateShipmentForShop(shop.Id, _shopUserId, codAmount: 100_000m);
        var assignResult = shipment.AssignShipper(_shipperId, _operatorId, "Assign before cancel test.");
        var shipmentRepository = new FakeShipmentRepository([shipment]);
        var service = new CancelShipmentForCurrentShopService(
            new CancelShipmentCommandValidator(),
            new FakeShopRepository([shop]),
            shipmentRepository);

        Assert.True(assignResult.IsSuccess);
        if (statusBeforeCancel == ShipmentStatus.PickingUp)
        {
            var pickupResult = shipment.UpdateStatus(ShipmentStatus.PickingUp, _shipperId, "Pickup started.");
            Assert.True(pickupResult.IsSuccess);
        }

        var result = await service.CancelAsync(new CancelShipmentCommand(
            _shopUserId,
            shipment.Id,
            "Shop requested cancellation before pickup completion."));

        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.Cancelled, shipment.Status);
        Assert.DoesNotContain(shipment.Assignments, assignment => assignment.IsActive);
        Assert.Equal(1, shipmentRepository.SaveChangesCount);
    }

    [Theory]
    [InlineData("Ho Chi Minh", "Ho Chi Minh", RouteType.IntraProvince)]
    [InlineData("Thanh pho Ho Chi Minh", "Ho Chi Minh", RouteType.IntraProvince)]
    [InlineData("Ha Noi", "Thanh pho Ha Noi", RouteType.IntraProvince)]
    [InlineData("Thanh pho Ho Chi Minh", "Thanh pho Ha Noi", RouteType.InterRegion)]
    public void RouteClassification_SupportsSeedAndAdministrativeProvinceAliases(
        string pickupProvince,
        string deliveryProvince,
        RouteType expectedRouteType)
    {
        var service = new RouteClassificationService();

        var result = service.Classify(pickupProvince, deliveryProvince);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedRouteType, result.Value.RouteType);
    }

    [Fact]
    public async Task CreateShipmentService_ActiveShopCreatesShipmentCodTransactionAndFeeBreakdown()
    {
        var shop = CreateShop(_shopUserId);
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var feeRule = new FeeRule(
            RouteType.InterRegion,
            baseWeightKg: 1m,
            baseFee: new Money(35_000m),
            extraWeightStepKg: 0.5m,
            extraStepFee: new Money(8_000m));
        var service = new CreateShipmentService(
            new CreateShipmentCommandValidator(),
            new ShippingFeeService(new FakeFeeRuleRepository([feeRule])),
            new RouteClassificationService(),
            shipmentRepository,
            new FakeShopRepository([shop]),
            codTransactionRepository);

        var result = await service.CreateAsync(new CreateShipmentCommand(
            _shopUserId,
            "Demo Shop",
            "0900000000",
            "Demo Receiver",
            "0911111111",
            new ShipmentAddressDto("1 Nguyen Trai", "Ben Thanh", "Ho Chi Minh"),
            new ShipmentAddressDto("9 Le Loi", "Hoan Kiem", "Thanh pho Ha Noi"),
            WeightKg: 1.2m,
            LengthCm: 20m,
            WidthCm: 15m,
            HeightCm: 10m,
            GoodsValueAmount: 2_000_000m,
            CodAmount: 150_000m,
            Currency: "VND",
            Note: "Create service E2E test."));

        Assert.True(result.IsSuccess);
        Assert.Single(shipmentRepository.Shipments);
        Assert.Single(codTransactionRepository.CodTransactions);

        var shipment = shipmentRepository.Shipments.Single();
        var codTransaction = codTransactionRepository.CodTransactions.Single();

        Assert.Equal(shop.Id, shipment.ShopId);
        Assert.Equal(ShipmentStatus.PendingPickup, shipment.Status);
        Assert.Equal(RouteType.InterRegion, shipment.RouteType);
        Assert.Equal(1.2m, result.Value.ChargeableWeightKg);
        Assert.Equal(35_000m, result.Value.BaseFeeAmount);
        Assert.Equal(8_000m, result.Value.ExtraWeightFeeAmount);
        Assert.Equal(10_000m, result.Value.InsuranceFeeAmount);
        Assert.Equal(53_000m, result.Value.ShippingFeeAmount);
        Assert.Equal(shipment.Id, codTransaction.ShipmentId);
        Assert.Equal(new Money(150_000m), codTransaction.Amount);
        Assert.Equal(CodStatus.PendingCollection, codTransaction.Status);
        Assert.Equal(1, shipmentRepository.SaveChangesCount);
    }

    [Fact]
    public async Task GetPublicTracking_MapsTimelineActorsAndKeepsUnknownUsersVisible()
    {
        var inactiveOperatorId = Guid.NewGuid();
        var unknownUserId = Guid.NewGuid();
        var identityService = CreateIdentityService();
        identityService.AddUser(inactiveOperatorId, false, nameof(UserRole.Operator));

        var shipment = CreateAssignedShipment(_shipperId);
        var pickupResult = shipment.UpdateStatus(ShipmentStatus.PickingUp, inactiveOperatorId, "Inactive operator support update.");
        var pickedUpResult = shipment.UpdateStatus(ShipmentStatus.PickedUp, unknownUserId, "Legacy user update.");
        var service = new GetPublicTrackingService(
            identityService,
            new FakeShipmentRepository([shipment]));

        var result = await service.GetAsync(shipment.TrackingCode.Value);

        Assert.True(pickupResult.IsSuccess);
        Assert.True(pickedUpResult.IsSuccess);
        Assert.True(result.IsSuccess);

        var timeline = result.Value.Timeline;
        Assert.Contains(timeline, history =>
            history.Status == ShipmentStatus.PendingPickup
            && history.ChangedByUserId == _shopUserId
            && history.ChangedByUserFound
            && history.ChangedByDisplayName == FormatFakeUserName(_shopUserId)
            && history.ChangedByEmail == FormatFakeUserEmail(_shopUserId));
        Assert.Contains(timeline, history =>
            history.Status == ShipmentStatus.Assigned
            && history.ChangedByUserId == _operatorId
            && history.ChangedByUserFound
            && history.ChangedByDisplayName == FormatFakeUserName(_operatorId));
        Assert.Contains(timeline, history =>
            history.Status == ShipmentStatus.PickingUp
            && history.ChangedByUserId == inactiveOperatorId
            && history.ChangedByUserFound
            && history.ChangedByDisplayName == FormatFakeUserName(inactiveOperatorId));
        Assert.Contains(timeline, history =>
            history.Status == ShipmentStatus.PickedUp
            && history.ChangedByUserId == unknownUserId
            && !history.ChangedByUserFound
            && history.ChangedByDisplayName == "Người dùng không xác định"
            && history.ChangedByEmail is null);
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

    private MarkCodSettledService CreateMarkCodSettledService(FakeCodTransactionRepository codTransactionRepository)
    {
        return new MarkCodSettledService(
            new MarkCodSettledCommandValidator(),
            CreateIdentityService(),
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
        return CreateShipmentForShop(createdByUserId, createdByUserId, codAmount);
    }

    private static Shipment CreateShipmentForShop(
        Guid shopId,
        Guid createdByUserId,
        decimal codAmount = 100_000m)
    {
        return Shipment.Create(
            shopId,
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

    private static Shop CreateShop(Guid ownerUserId)
    {
        return new Shop(
            ownerUserId,
            "Demo Shop",
            new PhoneNumber("0900000000"),
            new Address("1 Nguyen Trai", "Ben Thanh", "Ho Chi Minh"));
    }

    private static string FormatFakeUserName(Guid userId)
    {
        return $"User {userId.ToString()[..8]}";
    }

    private static string FormatFakeUserEmail(Guid userId)
    {
        return $"{userId:N}@example.test";
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        private readonly Dictionary<Guid, FakeUser> _users = [];

        public void AddUser(Guid userId, bool isActive, params string[] roles)
        {
            _users[userId] = new FakeUser(
                userId,
                $"User {userId.ToString()[..8]}",
                $"{userId:N}@example.test",
                null,
                isActive,
                roles.ToHashSet(),
                DateTimeOffset.UtcNow);
        }

        public Task<Result<Guid>> CreateUserAsync(
            string fullName,
            string email,
            string phoneNumber,
            string password,
            CancellationToken cancellationToken = default)
        {
            var userId = Guid.NewGuid();
            _users[userId] = new FakeUser(
                userId,
                fullName.Trim(),
                email.Trim(),
                phoneNumber.Trim(),
                true,
                [],
                DateTimeOffset.UtcNow);

            return Task.FromResult(Result<Guid>.Success(userId));
        }

        public Task<Result> AddToRoleAsync(
            Guid userId,
            string role,
            CancellationToken cancellationToken = default)
        {
            if (!_users.TryGetValue(userId, out var user))
            {
                return Task.FromResult(Result.Failure(ApplicationErrors.NotFound("User was not found.")));
            }

            user.Roles.Add(role);
            return Task.FromResult(Result.Success());
        }

        public async Task<Result<Guid>> CreateInternalUserAsync(
            string fullName,
            string email,
            string phoneNumber,
            string password,
            string role,
            CancellationToken cancellationToken = default)
        {
            if (_users.Values.Any(user => string.Equals(user.Email, email.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return Result<Guid>.Failure(ApplicationErrors.Conflict("Email already exists."));
            }

            var createResult = await CreateUserAsync(
                fullName,
                email,
                phoneNumber,
                password,
                cancellationToken);

            if (createResult.IsFailure)
            {
                return createResult;
            }

            var roleResult = await AddToRoleAsync(createResult.Value, role, cancellationToken);
            return roleResult.IsSuccess
                ? createResult
                : Result<Guid>.Failure(roleResult.Error);
        }

        public Task<Result> SetUserActiveStatusAsync(
            Guid userId,
            bool isActive,
            CancellationToken cancellationToken = default)
        {
            if (!_users.TryGetValue(userId, out var user))
            {
                return Task.FromResult(Result.Failure(ApplicationErrors.NotFound("User was not found.")));
            }

            user.IsActive = isActive;
            return Task.FromResult(Result.Success());
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

        public Task<IReadOnlyList<IdentityUserWithRolesResponse>> ListUsersWithRolesAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<IdentityUserWithRolesResponse> users = _users.Values
                .OrderBy(user => user.FullName)
                .Select(user => new IdentityUserWithRolesResponse(
                    user.UserId,
                    user.FullName,
                    user.Email,
                    user.PhoneNumber,
                    user.IsActive,
                    user.Roles.OrderBy(role => role).ToList(),
                    user.CreatedAtUtc))
                .ToList();

            return Task.FromResult(users);
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

        private sealed class FakeUser
        {
            public FakeUser(
                Guid userId,
                string fullName,
                string email,
                string? phoneNumber,
                bool isActive,
                HashSet<string> roles,
                DateTimeOffset createdAtUtc)
            {
                UserId = userId;
                FullName = fullName;
                Email = email;
                PhoneNumber = phoneNumber;
                IsActive = isActive;
                Roles = roles;
                CreatedAtUtc = createdAtUtc;
            }

            public Guid UserId { get; }

            public string FullName { get; }

            public string Email { get; }

            public string? PhoneNumber { get; }

            public bool IsActive { get; set; }

            public HashSet<string> Roles { get; }

            public DateTimeOffset CreatedAtUtc { get; }
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

        public IReadOnlyList<Shipment> Shipments => _shipments.AsReadOnly();

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

        public Task<IReadOnlyList<Shipment>> GetByIdsAsync(
            IReadOnlyCollection<Guid> shipmentIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shipment>>(_shipments.Where(shipment => shipmentIds.Contains(shipment.Id)).ToList());
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

        public int SaveChangesCount { get; private set; }

        public IReadOnlyList<CodTransaction> CodTransactions => _codTransactions.AsReadOnly();

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

    private sealed class FakeShopRepository : IShopRepository
    {
        private readonly List<Shop> _shops;

        public FakeShopRepository(IReadOnlyList<Shop> shops)
        {
            _shops = shops.ToList();
        }

        public int SaveChangesCount { get; private set; }

        public Task<Shop?> GetByIdAsync(
            Guid shopId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shops.FirstOrDefault(shop => shop.Id == shopId));
        }

        public Task<Shop?> GetByOwnerUserIdAsync(
            Guid ownerUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shops.FirstOrDefault(shop => shop.OwnerUserId == ownerUserId));
        }

        public Task<IReadOnlyList<Shop>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shop>>(_shops.ToList());
        }

        public Task<bool> ExistsByOwnerUserIdAsync(
            Guid ownerUserId,
            CancellationToken cancellationToken = default)
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
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFeeRuleRepository : IFeeRuleRepository
    {
        private readonly List<FeeRule> _feeRules;

        public FakeFeeRuleRepository(IReadOnlyList<FeeRule> feeRules)
        {
            _feeRules = feeRules.ToList();
        }

        public Task<IReadOnlyCollection<FeeRule>> GetActiveRulesAsync(
            RouteType routeType,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<FeeRule> rules = _feeRules
                .Where(rule => rule.IsActive && rule.RouteType == routeType)
                .ToList();

            return Task.FromResult(rules);
        }
    }
}
