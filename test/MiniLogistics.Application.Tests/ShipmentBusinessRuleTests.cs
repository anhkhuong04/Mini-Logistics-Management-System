using FluentValidation;
using MiniLogistics.Application.AdminUsers.CreateInternalUser;
using MiniLogistics.Application.AdminUsers.GetAdminUsers;
using MiniLogistics.Application.AdminUsers.SetShipperCapacity;
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
using MiniLogistics.Application.Shipments.DraftShipments;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shipments.AssignShipperToShipment;
using MiniLogistics.Application.Shipments.GetAssignedShipmentsForShipper;
using MiniLogistics.Application.Shipments.GetShipmentsForCurrentShop;
using MiniLogistics.Application.Shipments.GetOperationsShipments;
using MiniLogistics.Application.Shipments.GetPublicTracking;
using MiniLogistics.Application.Shipments.UpdateShipmentStatus;
using MiniLogistics.Application.Shipments.ImportShipments;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Application.Shippers.GetActiveShippers;
using MiniLogistics.Application.Shippers.SetShipperWorkingAreas;
using MiniLogistics.Application.Shipments.AssignmentSelection;
using MiniLogistics.Application.Shipments.AutoAssignShipment;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Application.Shops;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Operations;
using MiniLogistics.Domain.PartnerApi;
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
    public async Task AssignShipper_AdminManualOverride_AllowsActiveShipperWithoutWorkingAreaMatch()
    {
        var shipment = CreateShipment(_shopUserId);
        var repository = new FakeShipmentRepository([shipment]);
        var service = CreateAssignService(repository);

        var result = await service.AssignAsync(new AssignShipperCommand(
            shipment.Id,
            _shipperId,
            _adminId,
            "Manual override outside pickup area."));

        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.Assigned, shipment.Status);
        Assert.Contains(shipment.Assignments, assignment => assignment.ShipperId == _shipperId && assignment.IsActive);
        Assert.Contains(shipment.StatusHistory, history =>
            history.Status == ShipmentStatus.Assigned
            && history.ChangedByUserId == _adminId
            && history.Note == "Manual override outside pickup area.");
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task AssignmentSelector_ProvinceMatchWithoutPickupHub_SelectsMatchingShipper()
    {
        var targetShipment = CreateShipment(_shopUserId);
        var provinceOnlyArea = new ShipperWorkingArea(_shipperId, Guid.NewGuid(), "Ho Chi Minh", TestClock.UtcNow);
        var selector = new ShipmentAssignmentSelector(
            CreateIdentityService(),
            new FakeHubRepository([]),
            new FakeShipperWorkingAreaRepository([provinceOnlyArea]),
            new FakeShipmentRepository([targetShipment]));

        var result = await selector.SelectAsync(targetShipment);

        Assert.Equal(ShipmentAssignmentSelectionStatus.Selected, result.Status);
        Assert.Equal(_shipperId, result.ShipperId);
        Assert.Equal(provinceOnlyArea.Id, result.WorkingAreaId);
        Assert.Null(result.HubCode);
        Assert.Contains("Matched pickup province", result.Reason);
    }

    [Fact]
    public async Task AssignmentSelector_MatchingArea_SelectsLowestActiveLoad()
    {
        var targetShipment = CreateShipment(_shopUserId);
        var busyShipment = CreateAssignedShipment(_shipperId);
        var hub = new Hub("SPX-HCM-HUB", "SPX Ho Chi Minh Province Hub", "Ho Chi Minh", TestClock.UtcNow);
        var selector = new ShipmentAssignmentSelector(
            CreateIdentityService(),
            new FakeHubRepository([hub]),
            new FakeShipperWorkingAreaRepository([
                new ShipperWorkingArea(_shipperId, hub.Id, "Ho Chi Minh", TestClock.UtcNow),
                new ShipperWorkingArea(_otherShipperId, hub.Id, "Ho Chi Minh", TestClock.UtcNow)
            ]),
            new FakeShipmentRepository([targetShipment, busyShipment]));

        var result = await selector.SelectAsync(targetShipment);

        Assert.Equal(ShipmentAssignmentSelectionStatus.Selected, result.Status);
        Assert.Equal(_otherShipperId, result.ShipperId);
        Assert.Equal(0, result.ActiveShipmentCount);
    }

    [Fact]
    public async Task AssignmentSelector_WardSpecificArea_WinsBeforeLoadTieBreaker()
    {
        var targetShipment = CreateShipment(_shopUserId);
        var exactWardBusyShipment = CreateAssignedShipment(_shipperId);
        var hub = new Hub("SPX-HCM-HUB", "SPX Ho Chi Minh Province Hub", "Ho Chi Minh", TestClock.UtcNow);
        var selector = new ShipmentAssignmentSelector(
            CreateIdentityService(),
            new FakeHubRepository([hub]),
            new FakeShipperWorkingAreaRepository([
                new ShipperWorkingArea(_shipperId, hub.Id, "Ho Chi Minh", TestClock.UtcNow, "Ben Thanh"),
                new ShipperWorkingArea(_otherShipperId, hub.Id, "Ho Chi Minh", TestClock.UtcNow)
            ]),
            new FakeShipmentRepository([targetShipment, exactWardBusyShipment]));

        var result = await selector.SelectAsync(targetShipment);

        Assert.Equal(ShipmentAssignmentSelectionStatus.Selected, result.Status);
        Assert.Equal(_shipperId, result.ShipperId);
    }

    [Fact]
    public async Task AssignmentSelector_NoMatchingWorkingArea_ReturnsNoEligibleShipper()
    {
        var targetShipment = CreateShipment(_shopUserId);
        var hub = new Hub("SPX-HCM-HUB", "SPX Ho Chi Minh Province Hub", "Ho Chi Minh", TestClock.UtcNow);
        var selector = new ShipmentAssignmentSelector(
            CreateIdentityService(),
            new FakeHubRepository([hub]),
            new FakeShipperWorkingAreaRepository([]),
            new FakeShipmentRepository([targetShipment]));

        var result = await selector.SelectAsync(targetShipment);

        Assert.Equal(ShipmentAssignmentSelectionStatus.NoEligibleShipper, result.Status);
        Assert.Null(result.ShipperId);
        Assert.Contains("No shipper working area", result.Reason);
    }

    [Fact]
    public void ShipmentLoadStatuses_ActiveAssignmentStatuses_MatchCapacityDefinition()
    {
        Assert.Equal(
            [
                ShipmentStatus.Assigned,
                ShipmentStatus.PickingUp,
                ShipmentStatus.PickedUp,
                ShipmentStatus.InTransit,
                ShipmentStatus.Delivering,
                ShipmentStatus.DeliveryFailed
            ],
            ShipmentLoadStatuses.ActiveAssignmentStatuses);
    }

    [Fact]
    public async Task AssignmentSelector_MatchingArea_SkipsShipperAtCapacity()
    {
        var identityService = CreateIdentityService();
        identityService.SetShipperCapacity(_shipperId, isAvailableForAssignment: true, maxActiveShipments: 1);
        identityService.SetShipperCapacity(_otherShipperId, isAvailableForAssignment: true, maxActiveShipments: 10);
        var targetShipment = CreateShipment(_shopUserId);
        var busyShipment = CreateAssignedShipment(_shipperId);
        var hub = new Hub("SPX-HCM-HUB", "SPX Ho Chi Minh Province Hub", "Ho Chi Minh", TestClock.UtcNow);
        var selector = new ShipmentAssignmentSelector(
            identityService,
            new FakeHubRepository([hub]),
            new FakeShipperWorkingAreaRepository([
                new ShipperWorkingArea(_shipperId, hub.Id, "Ho Chi Minh", TestClock.UtcNow),
                new ShipperWorkingArea(_otherShipperId, hub.Id, "Ho Chi Minh", TestClock.UtcNow)
            ]),
            new FakeShipmentRepository([targetShipment, busyShipment]));

        var result = await selector.SelectAsync(targetShipment);

        Assert.Equal(ShipmentAssignmentSelectionStatus.Selected, result.Status);
        Assert.Equal(_otherShipperId, result.ShipperId);
    }

    [Fact]
    public async Task AssignmentSelector_MatchingArea_WhenAllShippersAtCapacity_ReturnsNoEligibleShipper()
    {
        var identityService = CreateIdentityService();
        identityService.SetShipperCapacity(_shipperId, isAvailableForAssignment: true, maxActiveShipments: 1);
        var targetShipment = CreateShipment(_shopUserId);
        var busyShipment = CreateAssignedShipment(_shipperId);
        var hub = new Hub("SPX-HCM-HUB", "SPX Ho Chi Minh Province Hub", "Ho Chi Minh", TestClock.UtcNow);
        var selector = new ShipmentAssignmentSelector(
            identityService,
            new FakeHubRepository([hub]),
            new FakeShipperWorkingAreaRepository([
                new ShipperWorkingArea(_shipperId, hub.Id, "Ho Chi Minh", TestClock.UtcNow)
            ]),
            new FakeShipmentRepository([targetShipment, busyShipment]));

        var result = await selector.SelectAsync(targetShipment);

        Assert.Equal(ShipmentAssignmentSelectionStatus.NoEligibleShipper, result.Status);
        Assert.Null(result.ShipperId);
        Assert.Contains("at capacity", result.Reason);
    }

    [Fact]
    public async Task AssignmentSelector_MatchingArea_SkipsUnavailableShipper()
    {
        var identityService = CreateIdentityService();
        identityService.SetShipperCapacity(_shipperId, isAvailableForAssignment: false, maxActiveShipments: 10);
        identityService.SetShipperCapacity(_otherShipperId, isAvailableForAssignment: true, maxActiveShipments: 10);
        var targetShipment = CreateShipment(_shopUserId);
        var hub = new Hub("SPX-HCM-HUB", "SPX Ho Chi Minh Province Hub", "Ho Chi Minh", TestClock.UtcNow);
        var selector = new ShipmentAssignmentSelector(
            identityService,
            new FakeHubRepository([hub]),
            new FakeShipperWorkingAreaRepository([
                new ShipperWorkingArea(_shipperId, hub.Id, "Ho Chi Minh", TestClock.UtcNow),
                new ShipperWorkingArea(_otherShipperId, hub.Id, "Ho Chi Minh", TestClock.UtcNow)
            ]),
            new FakeShipmentRepository([targetShipment]));

        var result = await selector.SelectAsync(targetShipment);

        Assert.Equal(ShipmentAssignmentSelectionStatus.Selected, result.Status);
        Assert.Equal(_otherShipperId, result.ShipperId);
    }

    [Fact]
    public async Task AssignmentSelector_MatchingArea_SkipsInactiveShipper()
    {
        var identityService = CreateIdentityService();
        identityService.AddUser(_shipperId, isActive: false, nameof(UserRole.Shipper));
        var targetShipment = CreateShipment(_shopUserId);
        var hub = new Hub("SPX-HCM-HUB", "SPX Ho Chi Minh Province Hub", "Ho Chi Minh", TestClock.UtcNow);
        var selector = new ShipmentAssignmentSelector(
            identityService,
            new FakeHubRepository([hub]),
            new FakeShipperWorkingAreaRepository([
                new ShipperWorkingArea(_shipperId, hub.Id, "Ho Chi Minh", TestClock.UtcNow),
                new ShipperWorkingArea(_otherShipperId, hub.Id, "Ho Chi Minh", TestClock.UtcNow)
            ]),
            new FakeShipmentRepository([targetShipment]));

        var result = await selector.SelectAsync(targetShipment);

        Assert.Equal(ShipmentAssignmentSelectionStatus.Selected, result.Status);
        Assert.Equal(_otherShipperId, result.ShipperId);
    }

    [Fact]
    public async Task AutoAssignShipment_AssignsPendingPickupAndPublishesWebhook()
    {
        var targetShipment = CreateShipment(_shopUserId);
        var hub = new Hub("SPX-HCM-HUB", "SPX Ho Chi Minh Province Hub", "Ho Chi Minh", TestClock.UtcNow);
        var shipmentRepository = new FakeShipmentRepository([targetShipment]);
        var publisher = new FakeWebhookEventPublisher();
        var selector = new ShipmentAssignmentSelector(
            CreateIdentityService(),
            new FakeHubRepository([hub]),
            new FakeShipperWorkingAreaRepository([
                new ShipperWorkingArea(_shipperId, hub.Id, "Ho Chi Minh", TestClock.UtcNow)
            ]),
            shipmentRepository);
        var service = new AutoAssignShipmentService(
            shipmentRepository,
            selector,
            TestClock.Provider,
            publisher);

        var result = await service.AutoAssignAsync(targetShipment.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(AutoAssignShipmentStatus.Assigned, result.Value.Status);
        Assert.Equal(_shipperId, result.Value.ShipperId);
        Assert.Equal(ShipmentStatus.Assigned, targetShipment.Status);
        Assert.Contains(targetShipment.Assignments, assignment => assignment.IsActive && assignment.ShipperId == _shipperId);
        Assert.Equal(1, shipmentRepository.SaveChangesCount);
        Assert.Equal(1, publisher.PublishCount);
        Assert.Equal(WebhookEventTypes.ShipmentStatusChanged, publisher.LastEventType);
    }

    [Fact]
    public async Task AutoAssignShipment_DeliveredCodShipment_CanStillBeCollectedByAssignedShipper()
    {
        var targetShipment = CreateShipment(_shopUserId, codAmount: 100_000m);
        var hub = new Hub("SPX-HCM-HUB", "SPX Ho Chi Minh Province Hub", "Ho Chi Minh", TestClock.UtcNow);
        var shipmentRepository = new FakeShipmentRepository([targetShipment]);
        var selector = new ShipmentAssignmentSelector(
            CreateIdentityService(),
            new FakeHubRepository([hub]),
            new FakeShipperWorkingAreaRepository([
                new ShipperWorkingArea(_shipperId, hub.Id, "Ho Chi Minh", TestClock.UtcNow)
            ]),
            shipmentRepository);
        var autoAssignService = new AutoAssignShipmentService(shipmentRepository, selector, TestClock.Provider);

        var autoAssignResult = await autoAssignService.AutoAssignAsync(targetShipment.Id);
        MoveShipmentToStatus(targetShipment, ShipmentStatus.Delivered);
        var codTransaction = CodTransaction.Create(targetShipment.Id, new Money(100_000m), TestClock.UtcNow);
        var codRepository = new FakeCodTransactionRepository([codTransaction]);
        var collectService = CreateMarkCodCollectedService(shipmentRepository, codRepository);
        var collectResult = await collectService.MarkCollectedAsync(new MarkCodCollectedCommand(
            targetShipment.Id,
            _shipperId));

        Assert.True(autoAssignResult.IsSuccess);
        Assert.Equal(AutoAssignShipmentStatus.Assigned, autoAssignResult.Value.Status);
        Assert.True(collectResult.IsSuccess);
        Assert.Equal(CodStatus.Collected, codTransaction.Status);
        Assert.Equal(_shipperId, codTransaction.CollectedByUserId);
    }

    [Fact]
    public async Task CreateInternalUser_AdminCreatesActiveShipperVisibleForAssignment()
    {
        var identityService = CreateIdentityService();
        var createService = new CreateInternalUserService(
            new CreateInternalUserCommandValidator(),
            identityService);
        var listService = CreateGetAdminUsersService(identityService);
        var shippersService = CreateGetActiveShippersService(identityService);

        var result = await createService.CreateAsync(new CreateInternalUserCommand(
            _adminId,
            "New Demo Shipper",
            "new.shipper@example.test",
            "0900000099",
            "Password1!",
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
            "Password1!",
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

        var shippersResult = await CreateGetActiveShippersService(identityService).GetAsync();
        Assert.True(shippersResult.IsSuccess);
        Assert.DoesNotContain(shippersResult.Value, shipper => shipper.UserId == managedShipperId);

        var shipment = CreateShipment(_shopUserId);
        var assignService = new AssignShipperToShipmentService(
            new AssignShipperCommandValidator(),
            identityService,
            new FakeShipmentRepository([shipment]),
            TestClock.Provider);

        var assignResult = await assignService.AssignAsync(new AssignShipperCommand(
            shipment.Id,
            managedShipperId,
            _adminId,
            "Assign to inactive shipper."));

        Assert.True(assignResult.IsFailure);
        Assert.Equal("Application.Forbidden", assignResult.Error.Code);
    }

    [Fact]
    public async Task SetShipperWorkingAreas_DuplicateNormalizedAreas_ReturnsValidationFailure()
    {
        var hub = new Hub("SPX-HCM-HUB", "SPX Ho Chi Minh Province Hub", "Ho Chi Minh", TestClock.UtcNow);
        var workingAreaRepository = new FakeShipperWorkingAreaRepository([]);
        var service = new SetShipperWorkingAreasService(
            new SetShipperWorkingAreasCommandValidator(),
            CreateIdentityService(),
            new FakeHubRepository([hub]),
            workingAreaRepository,
            TestClock.Provider);

        var result = await service.SetAsync(new SetShipperWorkingAreasCommand(
            _adminId,
            _shipperId,
            [
                new SetShipperWorkingAreaItem(hub.Id, " Ben Thanh "),
                new SetShipperWorkingAreaItem(hub.Id, "Ben Thanh", " ")
            ]));

        Assert.True(result.IsFailure);
        Assert.Equal("Application.ValidationFailed", result.Error.Code);
        Assert.Contains("duplicates", result.Error.Description);
        Assert.Empty(workingAreaRepository.WorkingAreas);
        Assert.Equal(0, workingAreaRepository.SaveChangesCount);
    }

    [Fact]
    public async Task SetShipperWorkingAreas_InactiveHub_IsRejected()
    {
        var hub = new Hub("SPX-HCM-HUB", "SPX Ho Chi Minh Province Hub", "Ho Chi Minh", TestClock.UtcNow);
        hub.Deactivate(TestClock.UtcNow);
        var workingAreaRepository = new FakeShipperWorkingAreaRepository([]);
        var service = new SetShipperWorkingAreasService(
            new SetShipperWorkingAreasCommandValidator(),
            CreateIdentityService(),
            new FakeHubRepository([hub]),
            workingAreaRepository,
            TestClock.Provider);

        var result = await service.SetAsync(new SetShipperWorkingAreasCommand(
            _adminId,
            _shipperId,
            [new SetShipperWorkingAreaItem(hub.Id)]));

        Assert.True(result.IsFailure);
        Assert.Equal("Application.Forbidden", result.Error.Code);
        Assert.Contains("Inactive hubs", result.Error.Description);
        Assert.Empty(workingAreaRepository.WorkingAreas);
        Assert.Equal(0, workingAreaRepository.SaveChangesCount);
    }

    [Fact]
    public async Task SetShipperWorkingAreas_AdminReplacesAreas_DeactivatesRemovedAreas()
    {
        var oldHub = new Hub("SPX-HCM-HUB", "SPX Ho Chi Minh Province Hub", "Ho Chi Minh", TestClock.UtcNow);
        var newHub = new Hub("SPX-HN-HUB", "SPX Ha Noi Province Hub", "Ha Noi", TestClock.UtcNow);
        var oldArea = new ShipperWorkingArea(_shipperId, oldHub.Id, oldHub.Province, TestClock.UtcNow);
        var workingAreaRepository = new FakeShipperWorkingAreaRepository([oldArea]);
        var service = new SetShipperWorkingAreasService(
            new SetShipperWorkingAreasCommandValidator(),
            CreateIdentityService(),
            new FakeHubRepository([oldHub, newHub]),
            workingAreaRepository,
            TestClock.Provider);

        var result = await service.SetAsync(new SetShipperWorkingAreasCommand(
            _adminId,
            _shipperId,
            [new SetShipperWorkingAreaItem(newHub.Id, "Hoan Kiem")]));

        Assert.True(result.IsSuccess);
        var activeArea = Assert.Single(result.Value);
        Assert.Equal(newHub.Id, activeArea.HubId);
        Assert.Equal("Hoan Kiem", activeArea.Ward);
        Assert.Equal(2, workingAreaRepository.WorkingAreas.Count);
        Assert.Contains(workingAreaRepository.WorkingAreas, area => area.Id == oldArea.Id && !area.IsActive);
        Assert.Contains(workingAreaRepository.WorkingAreas, area =>
            area.HubId == newHub.Id
            && area.Ward == "Hoan Kiem"
            && area.IsActive);
        Assert.Equal(1, workingAreaRepository.SaveChangesCount);
    }

    [Fact]
    public async Task SetShipperCapacity_AdminUpdatesAutoAssignmentCapacity()
    {
        var identityService = CreateIdentityService();
        var service = new SetShipperCapacityService(
            new SetShipperCapacityCommandValidator(),
            identityService);

        var result = await service.SetAsync(new SetShipperCapacityCommand(
            _adminId,
            _shipperId,
            IsAvailableForAssignment: false,
            MaxActiveShipments: 12));
        var shippersResult = await CreateGetActiveShippersService(identityService).GetAsync();

        Assert.True(result.IsSuccess);
        Assert.True(shippersResult.IsSuccess);
        var shipper = Assert.Single(shippersResult.Value, shipper => shipper.UserId == _shipperId);
        Assert.False(shipper.IsAvailableForAssignment);
        Assert.Equal(12, shipper.MaxActiveShipments);
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
        var codTransaction = CodTransaction.Create(shipment.Id, new Money(100_000m), TestClock.UtcNow);
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
        var codTransaction = CodTransaction.Create(shipment.Id, Money.Zero, TestClock.UtcNow);
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
        var codTransaction = CodTransaction.Create(shipment.Id, new Money(100_000m), TestClock.UtcNow);
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
        var codTransaction = CodTransaction.Create(shipment.Id, new Money(100_000m), TestClock.UtcNow);
        var collectResult = codTransaction.MarkCollected(shipment.Status, _shipperId, TestClock.UtcNow);
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
        var codTransaction = CodTransaction.Create(shipment.Id, new Money(100_000m), TestClock.UtcNow);
        var collectResult = codTransaction.MarkCollected(shipment.Status, _shipperId, TestClock.UtcNow);
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
        var codTransaction = CodTransaction.Create(
            shipment.Id,
            new Money(initialStatus == CodStatus.NotRequired ? 0m : 100_000m),
            TestClock.UtcNow);

        if (initialStatus == CodStatus.Settled)
        {
            var collectResult = codTransaction.MarkCollected(shipment.Status, _shipperId, TestClock.UtcNow);
            var settleResult = codTransaction.MarkSettled(_adminId, TestClock.UtcNow);
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
        var collectedCod = CodTransaction.Create(collectedShipment.Id, new Money(100_000m), TestClock.UtcNow);
        var pendingCod = CodTransaction.Create(pendingShipment.Id, new Money(200_000m), TestClock.UtcNow);
        var settledCod = CodTransaction.Create(settledShipment.Id, new Money(300_000m), TestClock.UtcNow);
        var collectResult = collectedCod.MarkCollected(collectedShipment.Status, _shipperId, TestClock.UtcNow);
        var settledCollectResult = settledCod.MarkCollected(settledShipment.Status, _shipperId, TestClock.UtcNow);
        var settleResult = settledCod.MarkSettled(_adminId, TestClock.UtcNow);
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
        var result = shipment.UpdateStatus(ShipmentStatus.Returned, _operatorId, TestClock.UtcNow, "Recipient refused parcel.");

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(shipment.Assignments, assignment => assignment.IsActive);
    }

    [Fact]
    public async Task GetAssignedShipments_DeliveredWithPendingCod_IsVisibleToShipper()
    {
        var shipment = CreateShipmentAtStatus(ShipmentStatus.Delivered, codAmount: 100_000m);
        var codTransaction = CodTransaction.Create(shipment.Id, new Money(100_000m), TestClock.UtcNow);
        var service = CreateGetAssignedShipmentsForShipperService([shipment], [codTransaction]);

        var result = await service.GetAsync(_shipperId);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value, response => response.ShipmentId == shipment.Id);
    }

    [Fact]
    public async Task GetAssignedShipments_DeliveredWithCollectedCod_IsHiddenFromShipper()
    {
        var shipment = CreateShipmentAtStatus(ShipmentStatus.Delivered, codAmount: 100_000m);
        var codTransaction = CodTransaction.Create(shipment.Id, new Money(100_000m), TestClock.UtcNow);
        var collectResult = codTransaction.MarkCollected(shipment.Status, _shipperId, TestClock.UtcNow);
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
        var returnedResult = returnedShipment.UpdateStatus(ShipmentStatus.Returned, _operatorId, TestClock.UtcNow, "Recipient refused parcel.");
        var cancelledResult = cancelledShipment.Cancel(_shopUserId, TestClock.UtcNow, "Shop cancelled before pickup.");
        var deliveredPendingCod = CodTransaction.Create(deliveredPendingCodShipment.Id, new Money(100_000m), TestClock.UtcNow);
        var deliveredCollectedCod = CodTransaction.Create(deliveredCollectedCodShipment.Id, new Money(100_000m), TestClock.UtcNow);
        var collectedResult = deliveredCollectedCod.MarkCollected(deliveredCollectedCodShipment.Status, _shipperId, TestClock.UtcNow);
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
                CodTransaction.Create(activeShipment.Id, Money.Zero, TestClock.UtcNow),
                CodTransaction.Create(returnedShipment.Id, new Money(100_000m), TestClock.UtcNow),
                CodTransaction.Create(cancelledShipment.Id, new Money(100_000m), TestClock.UtcNow)
            ]),
            CreateIdentityService(),
            TestClock.Provider);

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
        var codTransaction = CodTransaction.Create(shipment.Id, new Money(100_000m), TestClock.UtcNow);
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
        var assignResult = shipment.AssignShipper(_shipperId, _operatorId, TestClock.UtcNow, "Assign before cancel test.");
        var shipmentRepository = new FakeShipmentRepository([shipment]);
        var shopRepository = new FakeShopRepository([shop]);
        var service = new CancelShipmentForCurrentShopService(
            new CancelShipmentCommandValidator(),
            new ShopAccessService(CreateIdentityService(), shopRepository),
            shipmentRepository,
            TestClock.Provider);

        Assert.True(assignResult.IsSuccess);
        if (statusBeforeCancel == ShipmentStatus.PickingUp)
        {
            var pickupResult = shipment.UpdateStatus(ShipmentStatus.PickingUp, _shipperId, TestClock.UtcNow, "Pickup started.");
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
        var shopRepository = new FakeShopRepository([shop]);
        var feeRule = new FeeRule(
            RouteType.InterRegion,
            baseWeightKg: 1m,
            baseFee: new Money(35_000m),
            extraWeightStepKg: 0.5m,
            extraStepFee: new Money(8_000m),
            createdAtUtc: TestClock.UtcNow);
        var service = new CreateShipmentService(
            new CreateShipmentCommandValidator(),
            new ShippingFeeService(new FakeFeeRuleRepository([feeRule])),
            new RouteClassificationService(),
            shipmentRepository,
            new ShopAccessService(CreateIdentityService(), shopRepository),
            codTransactionRepository,
            CreateAutoAssignService(shipmentRepository, [], []),
            TestClock.Provider);

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
    public async Task CreateShipmentService_SelectedShopCreatesShipmentForSelectedShop()
    {
        var firstShop = CreateShop(_shopUserId);
        var secondShop = CreateShop(_shopUserId);
        secondShop.Rename("Second Shop", TestClock.UtcNow);
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var shopRepository = new FakeShopRepository([firstShop, secondShop]);
        var feeRule = new FeeRule(
            RouteType.InterRegion,
            baseWeightKg: 1m,
            baseFee: new Money(35_000m),
            extraWeightStepKg: 0.5m,
            extraStepFee: new Money(8_000m),
            createdAtUtc: TestClock.UtcNow);
        var service = new CreateShipmentService(
            new CreateShipmentCommandValidator(),
            new ShippingFeeService(new FakeFeeRuleRepository([feeRule])),
            new RouteClassificationService(),
            shipmentRepository,
            new ShopAccessService(CreateIdentityService(), shopRepository),
            codTransactionRepository,
            CreateAutoAssignService(shipmentRepository, [], []),
            TestClock.Provider);

        var result = await service.CreateAsync(new CreateShipmentCommand(
            _shopUserId,
            "Second Shop",
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
            Note: "Selected shop create service test.",
            ShopId: secondShop.Id));

        Assert.True(result.IsSuccess);
        Assert.Single(shipmentRepository.Shipments);
        Assert.Equal(secondShop.Id, shipmentRepository.Shipments.Single().ShopId);
        Assert.Single(codTransactionRepository.CodTransactions);
    }

    [Fact]
    public async Task GetShipmentsForCurrentShop_SelectedShopReturnsOnlyThatShop()
    {
        var firstShop = CreateShop(_shopUserId);
        var secondShop = CreateShop(_shopUserId);
        var firstShipment = CreateShipmentForShop(firstShop.Id, _shopUserId);
        var secondShipment = CreateShipmentForShop(secondShop.Id, _shopUserId);
        var shipmentRepository = new FakeShipmentRepository([firstShipment, secondShipment]);
        var shopRepository = new FakeShopRepository([firstShop, secondShop]);
        var service = new GetShipmentsForCurrentShopService(
            new ShopAccessService(CreateIdentityService(), shopRepository),
            shipmentRepository);

        var result = await service.GetAsync(_shopUserId, secondShop.Id);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(secondShipment.Id, result.Value.Single().ShipmentId);
    }

    [Fact]
    public async Task GetShipmentsForCurrentShop_SearchAppliesStatusFilterAndPagination()
    {
        var shop = CreateShop(_shopUserId);
        var assignedShipment = CreateShipmentForShop(shop.Id, _shopUserId);
        var secondAssignedShipment = CreateShipmentForShop(shop.Id, _shopUserId);
        var pendingShipment = CreateShipmentForShop(shop.Id, _shopUserId);
        var otherShopShipment = CreateShipmentForShop(Guid.NewGuid(), _shopUserId);
        ForceShipmentStatus(assignedShipment, ShipmentStatus.Assigned);
        ForceShipmentStatus(secondAssignedShipment, ShipmentStatus.Assigned);
        ForceShipmentStatus(pendingShipment, ShipmentStatus.PendingPickup);
        ForceShipmentStatus(otherShopShipment, ShipmentStatus.Assigned);
        var shipmentRepository = new FakeShipmentRepository([
            assignedShipment,
            secondAssignedShipment,
            pendingShipment,
            otherShopShipment
        ]);
        var shopRepository = new FakeShopRepository([shop]);
        var service = new GetShipmentsForCurrentShopService(
            new ShopAccessService(CreateIdentityService(), shopRepository),
            shipmentRepository);

        var result = await service.SearchAsync(new GetShipmentsForCurrentShopQuery(
            _shopUserId,
            shop.Id,
            ShipmentStatus.Assigned,
            PageNumber: 2,
            PageSize: 1));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal(secondAssignedShipment.Id, result.Value.Items.Single().ShipmentId);
        Assert.Equal(2, result.Value.PageNumber);
        Assert.Equal(1, result.Value.PageSize);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Equal(2, result.Value.TotalPages);
    }

    [Fact]
    public async Task GetShipmentsForCurrentShop_OtherOwnerShopIdIsRejected()
    {
        var ownShop = CreateShop(_shopUserId);
        var otherShop = CreateShop(Guid.NewGuid());
        var shipmentRepository = new FakeShipmentRepository([]);
        var shopRepository = new FakeShopRepository([ownShop, otherShop]);
        var service = new GetShipmentsForCurrentShopService(
            new ShopAccessService(CreateIdentityService(), shopRepository),
            shipmentRepository);

        var result = await service.GetAsync(_shopUserId, otherShop.Id);

        Assert.True(result.IsFailure);
        Assert.Equal("Application.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task ShipmentImportPreview_ValidCsv_ReturnsFeePreview()
    {
        var shop = CreateShop(_shopUserId);
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var service = CreateShipmentImportService(
            new FakeShopRepository([shop]),
            shipmentRepository,
            codTransactionRepository);

        var result = await service.PreviewAsync(new PreviewShipmentImportCommand(
            _shopUserId,
            shop.Id,
            BuildImportCsv(
                "ORD-1001,Demo Receiver,0911111111,9 Le Loi,Hoan Kiem,Thanh pho Ha Noi,Vietnam,1.2,20,15,10,2000000,150000,Imported order")));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalRows);
        Assert.Equal(1, result.Value.ValidRows);
        Assert.True(result.Value.Rows.Single().IsValid);
        Assert.Equal(RouteType.InterRegion, result.Value.Rows.Single().RouteType);
        Assert.Equal(53_000m, result.Value.Rows.Single().ShippingFeeAmount);
    }

    [Fact]
    public async Task ShipmentImportPreview_MissingRequiredField_ReturnsRowError()
    {
        var shop = CreateShop(_shopUserId);
        var service = CreateShipmentImportService(
            new FakeShopRepository([shop]),
            new FakeShipmentRepository([]),
            new FakeCodTransactionRepository([]));

        var result = await service.PreviewAsync(new PreviewShipmentImportCommand(
            _shopUserId,
            shop.Id,
            BuildImportCsv(
                "ORD-1001,,0911111111,9 Le Loi,Hoan Kiem,Thanh pho Ha Noi,Vietnam,1.2,20,15,10,2000000,150000,Imported order")));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.ValidRows);
        Assert.Contains(result.Value.Rows.Single().Errors, error => error.Contains("receiverName is required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShipmentImportPreview_UnsupportedProvince_ReturnsRowError()
    {
        var shop = CreateShop(_shopUserId);
        var service = CreateShipmentImportService(
            new FakeShopRepository([shop]),
            new FakeShipmentRepository([]),
            new FakeCodTransactionRepository([]));

        var result = await service.PreviewAsync(new PreviewShipmentImportCommand(
            _shopUserId,
            shop.Id,
            BuildImportCsv(
                "ORD-1001,Demo Receiver,0911111111,9 Le Loi,Central,Atlantis,Vietnam,1.2,20,15,10,2000000,150000,Imported order")));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Rows.Single().IsValid);
        Assert.Contains(result.Value.Rows.Single().Errors, error => error.Contains("Province is not supported", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShipmentImportPreview_DuplicateClientOrderCode_IsRejectedPerRow()
    {
        var shop = CreateShop(_shopUserId);
        var service = CreateShipmentImportService(
            new FakeShopRepository([shop]),
            new FakeShipmentRepository([]),
            new FakeCodTransactionRepository([]));

        var result = await service.PreviewAsync(new PreviewShipmentImportCommand(
            _shopUserId,
            shop.Id,
            BuildImportCsv(
                "ORD-1001,First Receiver,0911111111,9 Le Loi,Hoan Kiem,Thanh pho Ha Noi,Vietnam,1.2,20,15,10,2000000,150000,Imported order",
                "ORD-1001,Second Receiver,0922222222,10 Le Loi,Hoan Kiem,Thanh pho Ha Noi,Vietnam,1.2,20,15,10,2000000,150000,Imported order")));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.ValidRows);
        Assert.All(result.Value.Rows, row =>
            Assert.Contains(row.Errors, error => error.Contains("Duplicate clientOrderCode", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task ShipmentImportConfirm_CreatesOnlyValidRows()
    {
        var shop = CreateShop(_shopUserId);
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var service = CreateShipmentImportService(
            new FakeShopRepository([shop]),
            shipmentRepository,
            codTransactionRepository);
        var validRow = CreateImportRowDraft(2, "ORD-1001");
        var invalidRow = CreateImportRowDraft(3, "ORD-1002") with { ReceiverPhone = "bad-phone" };

        var result = await service.ConfirmAsync(new ConfirmShipmentImportCommand(
            _shopUserId,
            shop.Id,
            [validRow, invalidRow]));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.CreatedRows);
        Assert.Equal(1, result.Value.FailedRows);
        Assert.Single(shipmentRepository.Shipments);
        Assert.Single(codTransactionRepository.CodTransactions);
        Assert.Contains(result.Value.Rows, row => row.RowNumber == validRow.RowNumber && row.IsCreated);
        Assert.Contains(result.Value.Rows, row => row.RowNumber == invalidRow.RowNumber && !row.IsCreated);
    }

    [Fact]
    public async Task ShipmentImportPreview_InactiveShop_IsRejected()
    {
        var shop = CreateShop(_shopUserId);
        shop.Deactivate(TestClock.UtcNow);
        var service = CreateShipmentImportService(
            new FakeShopRepository([shop]),
            new FakeShipmentRepository([]),
            new FakeCodTransactionRepository([]));

        var result = await service.PreviewAsync(new PreviewShipmentImportCommand(
            _shopUserId,
            shop.Id,
            BuildImportCsv(
                "ORD-1001,Demo Receiver,0911111111,9 Le Loi,Hoan Kiem,Thanh pho Ha Noi,Vietnam,1.2,20,15,10,2000000,150000,Imported order")));

        Assert.True(result.IsFailure);
        Assert.Equal("Application.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task CreateShipmentService_MatchingWorkingAreaAutoAssignsShipment()
    {
        var shop = CreateShop(_shopUserId);
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var shopRepository = new FakeShopRepository([shop]);
        var hub = new Hub("SPX-HCM-HUB", "SPX Ho Chi Minh Province Hub", "Ho Chi Minh", TestClock.UtcNow);
        var feeRule = new FeeRule(
            RouteType.IntraProvince,
            baseWeightKg: 1m,
            baseFee: new Money(20_000m),
            extraWeightStepKg: 0.5m,
            extraStepFee: new Money(5_000m),
            createdAtUtc: TestClock.UtcNow);
        var service = new CreateShipmentService(
            new CreateShipmentCommandValidator(),
            new ShippingFeeService(new FakeFeeRuleRepository([feeRule])),
            new RouteClassificationService(),
            shipmentRepository,
            new ShopAccessService(CreateIdentityService(), shopRepository),
            codTransactionRepository,
            CreateAutoAssignService(
                shipmentRepository,
                [hub],
                [new ShipperWorkingArea(_shipperId, hub.Id, "Ho Chi Minh", TestClock.UtcNow)]),
            TestClock.Provider);

        var result = await service.CreateAsync(new CreateShipmentCommand(
            _shopUserId,
            "Demo Shop",
            "0900000000",
            "Demo Receiver",
            "0911111111",
            new ShipmentAddressDto("1 Nguyen Trai", "Ben Thanh", "Ho Chi Minh"),
            new ShipmentAddressDto("9 Le Loi", "Ben Nghe", "Ho Chi Minh"),
            WeightKg: 1m,
            LengthCm: 10m,
            WidthCm: 10m,
            HeightCm: 10m,
            GoodsValueAmount: 500_000m,
            CodAmount: 100_000m,
            Currency: "VND",
            Note: "Auto assign create service test."));

        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.Assigned, result.Value.Status);
        var shipment = shipmentRepository.Shipments.Single();
        Assert.Equal(ShipmentStatus.Assigned, shipment.Status);
        Assert.Contains(shipment.Assignments, assignment => assignment.IsActive && assignment.ShipperId == _shipperId);
        Assert.Equal(2, shipmentRepository.SaveChangesCount);
    }

    [Fact]
    public async Task CreateDraftShipment_DoesNotCreateCodOrAutoAssign()
    {
        var shop = CreateShop(_shopUserId);
        var shipmentRepository = new FakeShipmentRepository([]);
        var service = CreateDraftShipmentService(
            new FakeShopRepository([shop]),
            shipmentRepository);

        var result = await service.CreateAsync(BuildDraftCommand(shop.Id));

        Assert.True(result.IsSuccess);
        Assert.Single(shipmentRepository.Shipments);

        var shipment = shipmentRepository.Shipments.Single();
        Assert.Equal(ShipmentStatus.Draft, shipment.Status);
        Assert.Empty(shipment.Assignments);
        Assert.Equal(1, shipmentRepository.SaveChangesCount);
    }

    [Fact]
    public async Task SubmitDraftShipment_CreatesCodAndAutoAssignsWhenEligible()
    {
        var shop = CreateShop(_shopUserId);
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var createDraftService = CreateDraftShipmentService(
            new FakeShopRepository([shop]),
            shipmentRepository);
        var createResult = await createDraftService.CreateAsync(BuildDraftCommand(shop.Id));
        var hub = new Hub("SPX-HCM-HUB", "SPX Ho Chi Minh Province Hub", "Ho Chi Minh", TestClock.UtcNow);
        var submitService = CreateSubmitDraftShipmentService(
            new FakeShopRepository([shop]),
            shipmentRepository,
            codTransactionRepository,
            CreateAutoAssignService(
                shipmentRepository,
                [hub],
                [new ShipperWorkingArea(_shipperId, hub.Id, "Ho Chi Minh", TestClock.UtcNow)]));

        var result = await submitService.SubmitAsync(new SubmitDraftShipmentCommand(
            _shopUserId,
            createResult.Value.ShipmentId,
            shop.Id));

        Assert.True(createResult.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.Single(codTransactionRepository.CodTransactions);
        Assert.Equal(new Money(150_000m), codTransactionRepository.CodTransactions.Single().Amount);

        var shipment = shipmentRepository.Shipments.Single();
        Assert.Equal(ShipmentStatus.Assigned, shipment.Status);
        Assert.Contains(shipment.Assignments, assignment => assignment.IsActive && assignment.ShipperId == _shipperId);
    }

    [Fact]
    public async Task UpdateDraftShipment_RecalculatesFeeAndKeepsDraftStatus()
    {
        var shop = CreateShop(_shopUserId);
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var createDraftService = CreateDraftShipmentService(
            new FakeShopRepository([shop]),
            shipmentRepository);
        var createResult = await createDraftService.CreateAsync(BuildDraftCommand(shop.Id));
        var updateService = CreateUpdateShipmentBeforePickupService(
            new FakeShopRepository([shop]),
            shipmentRepository,
            codTransactionRepository);

        var result = await updateService.UpdateAsync(BuildUpdateBeforePickupCommand(
            shop.Id,
            createResult.Value.ShipmentId,
            deliveryProvince: "Ho Chi Minh",
            goodsValueAmount: 0m,
            codAmount: 0m));

        Assert.True(createResult.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.Draft, shipmentRepository.Shipments.Single().Status);
        Assert.Equal(RouteType.IntraProvince, shipmentRepository.Shipments.Single().RouteType);
        Assert.Equal(20_000m, shipmentRepository.Shipments.Single().ShippingFee.Amount);
        Assert.Empty(codTransactionRepository.CodTransactions);
    }

    [Fact]
    public async Task UpdatePendingPickupShipment_RecalculatesFeeAndUpdatesCod()
    {
        var shop = CreateShop(_shopUserId);
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var createService = new CreateShipmentService(
            new CreateShipmentCommandValidator(),
            CreateShippingFeeService(),
            new RouteClassificationService(),
            shipmentRepository,
            new ShopAccessService(CreateIdentityService(), new FakeShopRepository([shop])),
            codTransactionRepository,
            CreateAutoAssignService(shipmentRepository, [], []),
            TestClock.Provider);
        var createResult = await createService.CreateAsync(BuildCreateShipmentCommand(shop.Id));
        var updateService = CreateUpdateShipmentBeforePickupService(
            new FakeShopRepository([shop]),
            shipmentRepository,
            codTransactionRepository);

        var result = await updateService.UpdateAsync(BuildUpdateBeforePickupCommand(
            shop.Id,
            createResult.Value.ShipmentId,
            deliveryProvince: "Ho Chi Minh",
            goodsValueAmount: 0m,
            codAmount: 50_000m));

        Assert.True(createResult.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.PendingPickup, shipmentRepository.Shipments.Single().Status);
        Assert.Equal(RouteType.IntraProvince, shipmentRepository.Shipments.Single().RouteType);
        Assert.Equal(20_000m, shipmentRepository.Shipments.Single().ShippingFee.Amount);
        Assert.Equal(new Money(50_000m), codTransactionRepository.CodTransactions.Single().Amount);
        Assert.Equal(CodStatus.PendingCollection, codTransactionRepository.CodTransactions.Single().Status);
    }

    [Theory]
    [InlineData(ShipmentStatus.Assigned)]
    [InlineData(ShipmentStatus.PickedUp)]
    [InlineData(ShipmentStatus.InTransit)]
    [InlineData(ShipmentStatus.Delivered)]
    public async Task UpdateShipmentBeforePickup_NonEditableStatusesAreRejected(ShipmentStatus status)
    {
        var shop = CreateShop(_shopUserId);
        var shipment = CreateShipmentForShop(shop.Id, _shopUserId);
        var assignResult = shipment.AssignShipper(_shipperId, _operatorId, TestClock.UtcNow, "Assigned for edit rejection test.");
        Assert.True(assignResult.IsSuccess);
        if (status != ShipmentStatus.Assigned)
        {
            MoveShipmentToStatus(shipment, status);
        }
        var service = CreateUpdateShipmentBeforePickupService(
            new FakeShopRepository([shop]),
            new FakeShipmentRepository([shipment]),
            new FakeCodTransactionRepository([CodTransaction.Create(shipment.Id, shipment.CodAmount, TestClock.UtcNow)]));

        var result = await service.UpdateAsync(BuildUpdateBeforePickupCommand(
            shop.Id,
            shipment.Id));

        Assert.True(result.IsFailure);
        Assert.Equal("Shipment.CannotEditBeforePickup", result.Error.Code);
    }

    [Fact]
    public async Task GetPublicTracking_WithoutPhoneLast4_ReturnsSummaryWithoutPii()
    {
        var shipment = CreateAssignedShipment(_shipperId);
        var pickupResult = shipment.UpdateStatus(
            ShipmentStatus.PickingUp,
            _shipperId,
            TestClock.UtcNow,
            "Sensitive internal handoff note.");
        var service = new GetPublicTrackingService(new FakeShipmentRepository([shipment]));

        var result = await service.GetAsync(shipment.TrackingCode.Value);

        Assert.True(pickupResult.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.Equal(PublicTrackingAccessLevel.Summary, result.Value.AccessLevel);
        Assert.Equal(shipment.TrackingCode.Value, result.Value.TrackingCode);
        Assert.Equal(ShipmentStatus.PickingUp, result.Value.CurrentStatus);
        Assert.Equal("Ho Chi Minh", result.Value.PickupProvince);
        Assert.Equal("Ho Chi Minh", result.Value.DeliveryProvince);
        Assert.NotEqual("Shop Demo", result.Value.SenderName);
        Assert.NotEqual("Customer Demo", result.Value.ReceiverName);
        Assert.Null(result.Value.SenderPhone);
        Assert.Null(result.Value.ReceiverPhone);
        Assert.Null(result.Value.PickupAddress);
        Assert.Null(result.Value.DeliveryAddress);
        Assert.Contains(result.Value.Timeline, item => item.Status == ShipmentStatus.PickingUp);
    }

    [Fact]
    public async Task GetPublicTracking_ReceiverPhoneLast4_ReturnsVerifiedDetail()
    {
        var shipment = CreateAssignedShipment(_shipperId);
        var service = new GetPublicTrackingService(new FakeShipmentRepository([shipment]));

        var result = await service.GetAsync(shipment.TrackingCode.Value, "1111");

        Assert.True(result.IsSuccess);
        Assert.Equal(PublicTrackingAccessLevel.Verified, result.Value.AccessLevel);
        Assert.Equal("Customer Demo", result.Value.ReceiverName);
        Assert.Equal("0911111111", result.Value.ReceiverPhone);
        Assert.Equal("Shop Demo", result.Value.SenderName);
        Assert.Equal("0900000000", result.Value.SenderPhone);
        Assert.Equal("9 Le Loi, Ben Nghe, Ho Chi Minh, Vietnam", result.Value.DeliveryAddress?.FullAddress);
    }

    [Fact]
    public async Task GetPublicTracking_SenderPhoneLast4_ReturnsVerifiedDetail()
    {
        var shipment = CreateAssignedShipment(_shipperId);
        var service = new GetPublicTrackingService(new FakeShipmentRepository([shipment]));

        var result = await service.GetAsync(shipment.TrackingCode.Value, "0000");

        Assert.True(result.IsSuccess);
        Assert.Equal(PublicTrackingAccessLevel.Verified, result.Value.AccessLevel);
        Assert.Equal("Shop Demo", result.Value.SenderName);
        Assert.Equal("0900000000", result.Value.SenderPhone);
        Assert.Equal("1 Nguyen Trai, Ben Thanh, Ho Chi Minh, Vietnam", result.Value.PickupAddress?.FullAddress);
    }

    [Fact]
    public async Task GetPublicTracking_WrongPhoneLast4_KeepsSummaryAndDoesNotLeakPii()
    {
        var shipment = CreateAssignedShipment(_shipperId);
        var service = new GetPublicTrackingService(new FakeShipmentRepository([shipment]));

        var result = await service.GetAsync(shipment.TrackingCode.Value, "9999");

        Assert.True(result.IsSuccess);
        Assert.Equal(PublicTrackingAccessLevel.Summary, result.Value.AccessLevel);
        Assert.NotEqual("Shop Demo", result.Value.SenderName);
        Assert.NotEqual("Customer Demo", result.Value.ReceiverName);
        Assert.Null(result.Value.SenderPhone);
        Assert.Null(result.Value.ReceiverPhone);
        Assert.Null(result.Value.PickupAddress);
        Assert.Null(result.Value.DeliveryAddress);
    }

    [Fact]
    public async Task GetPublicTracking_InvalidPhoneLast4_IsRejected()
    {
        var shipment = CreateAssignedShipment(_shipperId);
        var service = new GetPublicTrackingService(new FakeShipmentRepository([shipment]));

        var result = await service.GetAsync(shipment.TrackingCode.Value, "12ab");

        Assert.True(result.IsFailure);
        Assert.Equal("Application.ValidationFailed", result.Error.Code);
    }

    [Fact]
    public async Task GetPublicTracking_DraftShipment_IsHidden()
    {
        var shipment = CreateShipment(_shopUserId);
        ForceShipmentStatus(shipment, ShipmentStatus.Draft);
        var service = new GetPublicTrackingService(new FakeShipmentRepository([shipment]));

        var result = await service.GetAsync(shipment.TrackingCode.Value);

        Assert.True(result.IsFailure);
        Assert.Equal("Application.NotFound", result.Error.Code);
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
            shipmentRepository,
            TestClock.Provider);
    }

    private UpdateShipmentStatusService CreateUpdateStatusService(IReadOnlyList<Shipment> shipments)
    {
        return new UpdateShipmentStatusService(
            new UpdateShipmentStatusCommandValidator(),
            CreateIdentityService(),
            new FakeShipmentRepository(shipments),
            TestClock.Provider);
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
            codTransactionRepository,
            TestClock.Provider);
    }

    private MarkCodSettledService CreateMarkCodSettledService(FakeCodTransactionRepository codTransactionRepository)
    {
        return new MarkCodSettledService(
            new MarkCodSettledCommandValidator(),
            CreateIdentityService(),
            codTransactionRepository,
            TestClock.Provider);
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

    private static GetAdminUsersService CreateGetAdminUsersService(FakeIdentityService identityService)
    {
        return new GetAdminUsersService(
            identityService,
            new FakeHubRepository([]),
            new FakeShipperWorkingAreaRepository([]));
    }

    private static GetActiveShippersService CreateGetActiveShippersService(FakeIdentityService identityService)
    {
        return new GetActiveShippersService(
            identityService,
            new FakeHubRepository([]),
            new FakeShipperWorkingAreaRepository([]));
    }

    private AutoAssignShipmentService CreateAutoAssignService(
        FakeShipmentRepository shipmentRepository,
        IReadOnlyList<Hub> hubs,
        IReadOnlyList<ShipperWorkingArea> workingAreas)
    {
        return new AutoAssignShipmentService(
            shipmentRepository,
            new ShipmentAssignmentSelector(
                CreateIdentityService(),
                new FakeHubRepository(hubs),
                new FakeShipperWorkingAreaRepository(workingAreas),
                shipmentRepository),
            TestClock.Provider);
    }

    private static ShippingFeeService CreateShippingFeeService()
    {
        return new ShippingFeeService(new FakeFeeRuleRepository([
            new FeeRule(
                RouteType.InterRegion,
                baseWeightKg: 1m,
                baseFee: new Money(35_000m),
                extraWeightStepKg: 0.5m,
                extraStepFee: new Money(8_000m),
                createdAtUtc: TestClock.UtcNow),
            new FeeRule(
                RouteType.IntraProvince,
                baseWeightKg: 1m,
                baseFee: new Money(20_000m),
                extraWeightStepKg: 0.5m,
                extraStepFee: new Money(5_000m),
                createdAtUtc: TestClock.UtcNow)
        ]));
    }

    private CreateDraftShipmentService CreateDraftShipmentService(
        FakeShopRepository shopRepository,
        FakeShipmentRepository shipmentRepository)
    {
        return new CreateDraftShipmentService(
            new CreateDraftShipmentCommandValidator(),
            CreateShippingFeeService(),
            new RouteClassificationService(),
            shipmentRepository,
            new ShopAccessService(CreateIdentityService(), shopRepository),
            TestClock.Provider);
    }

    private UpdateShipmentBeforePickupService CreateUpdateShipmentBeforePickupService(
        FakeShopRepository shopRepository,
        FakeShipmentRepository shipmentRepository,
        FakeCodTransactionRepository codTransactionRepository)
    {
        return new UpdateShipmentBeforePickupService(
            new UpdateShipmentBeforePickupCommandValidator(),
            CreateShippingFeeService(),
            new RouteClassificationService(),
            shipmentRepository,
            new ShopAccessService(CreateIdentityService(), shopRepository),
            codTransactionRepository,
            TestClock.Provider);
    }

    private SubmitDraftShipmentService CreateSubmitDraftShipmentService(
        FakeShopRepository shopRepository,
        FakeShipmentRepository shipmentRepository,
        FakeCodTransactionRepository codTransactionRepository,
        AutoAssignShipmentService autoAssignShipmentService)
    {
        return new SubmitDraftShipmentService(
            new SubmitDraftShipmentCommandValidator(),
            CreateShippingFeeService(),
            new RouteClassificationService(),
            shipmentRepository,
            new ShopAccessService(CreateIdentityService(), shopRepository),
            codTransactionRepository,
            autoAssignShipmentService,
            TestClock.Provider);
    }

    private ShipmentImportService CreateShipmentImportService(
        FakeShopRepository shopRepository,
        FakeShipmentRepository shipmentRepository,
        FakeCodTransactionRepository codTransactionRepository)
    {
        var feeService = new ShippingFeeService(new FakeFeeRuleRepository([
            new FeeRule(
                RouteType.InterRegion,
                baseWeightKg: 1m,
                baseFee: new Money(35_000m),
                extraWeightStepKg: 0.5m,
                extraStepFee: new Money(8_000m),
                createdAtUtc: TestClock.UtcNow),
            new FeeRule(
                RouteType.IntraProvince,
                baseWeightKg: 1m,
                baseFee: new Money(20_000m),
                extraWeightStepKg: 0.5m,
                extraStepFee: new Money(5_000m),
                createdAtUtc: TestClock.UtcNow)
        ]));
        var routeClassificationService = new RouteClassificationService();
        var shopAccessService = new ShopAccessService(CreateIdentityService(), shopRepository);
        var createShipmentService = new CreateShipmentService(
            new CreateShipmentCommandValidator(),
            feeService,
            routeClassificationService,
            shipmentRepository,
            shopAccessService,
            codTransactionRepository,
            CreateAutoAssignService(shipmentRepository, [], []),
            TestClock.Provider);

        return new ShipmentImportService(
            new PreviewShipmentImportCommandValidator(),
            new ConfirmShipmentImportCommandValidator(),
            new CreateShipmentCommandValidator(),
            shopAccessService,
            routeClassificationService,
            feeService,
            createShipmentService);
    }

    private static string BuildImportCsv(params string[] rows)
    {
        const string header = "clientOrderCode,receiverName,receiverPhone,deliveryStreet,deliveryWard,deliveryProvince,deliveryCountry,weightKg,lengthCm,widthCm,heightCm,goodsValueAmount,codAmount,note";
        return header + Environment.NewLine + string.Join(Environment.NewLine, rows);
    }

    private CreateShipmentCommand BuildCreateShipmentCommand(Guid shopId)
    {
        return new CreateShipmentCommand(
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
            Note: "Draft/edit test shipment.",
            ShopId: shopId);
    }

    private CreateDraftShipmentCommand BuildDraftCommand(Guid shopId)
    {
        var createCommand = BuildCreateShipmentCommand(shopId);
        return new CreateDraftShipmentCommand(
            createCommand.CreatedByUserId,
            createCommand.SenderName,
            createCommand.SenderPhone,
            createCommand.ReceiverName,
            createCommand.ReceiverPhone,
            createCommand.PickupAddress,
            createCommand.DeliveryAddress,
            createCommand.WeightKg,
            createCommand.LengthCm,
            createCommand.WidthCm,
            createCommand.HeightCm,
            createCommand.GoodsValueAmount,
            createCommand.CodAmount,
            createCommand.Currency,
            createCommand.Note,
            createCommand.ShopId);
    }

    private UpdateShipmentBeforePickupCommand BuildUpdateBeforePickupCommand(
        Guid shopId,
        Guid shipmentId,
        string deliveryProvince = "Thanh pho Ha Noi",
        decimal goodsValueAmount = 2_000_000m,
        decimal codAmount = 150_000m)
    {
        return new UpdateShipmentBeforePickupCommand(
            _shopUserId,
            shipmentId,
            "Demo Shop",
            "0900000000",
            "Updated Receiver",
            "0911111111",
            new ShipmentAddressDto("1 Nguyen Trai", "Ben Thanh", "Ho Chi Minh"),
            new ShipmentAddressDto("9 Le Loi", "Ben Nghe", deliveryProvince),
            WeightKg: 1m,
            LengthCm: 10m,
            WidthCm: 10m,
            HeightCm: 10m,
            GoodsValueAmount: goodsValueAmount,
            CodAmount: codAmount,
            Currency: "VND",
            Note: "Updated before pickup.",
            ShopId: shopId);
    }

    private static ShipmentImportRowDraft CreateImportRowDraft(int rowNumber, string clientOrderCode)
    {
        return new ShipmentImportRowDraft(
            rowNumber,
            clientOrderCode,
            "Demo Receiver",
            "0911111111",
            "9 Le Loi",
            "Hoan Kiem",
            "Thanh pho Ha Noi",
            "Vietnam",
            1.2m,
            20m,
            15m,
            10m,
            2_000_000m,
            150_000m,
            "Imported order");
    }

    private Shipment CreateAssignedShipment(Guid shipperId, decimal codAmount = 100_000m)
    {
        var shipment = CreateShipment(_shopUserId, codAmount);
        var assignResult = shipment.AssignShipper(shipperId, _operatorId, TestClock.UtcNow, "Assigned for test.");
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

            var result = shipment.UpdateStatus(status, _shipperId, TestClock.UtcNow, $"Move to {status}.");
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
            TestClock.UtcNow,
            "Test shipment.");
    }

    private static void ForceShipmentStatus(Shipment shipment, ShipmentStatus status)
    {
        typeof(Shipment)
            .GetProperty(nameof(Shipment.Status))!
            .SetValue(shipment, status);
    }

    private static Shop CreateShop(Guid ownerUserId)
    {
        return new Shop(
            ownerUserId,
            "Demo Shop",
            new PhoneNumber("0900000000"),
            new Address("1 Nguyen Trai", "Ben Thanh", "Ho Chi Minh"),
            TestClock.UtcNow);
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
                true,
                30,
                roles.ToHashSet(),
                TestClock.UtcNow);
        }

        public void SetShipperCapacity(Guid userId, bool isAvailableForAssignment, int maxActiveShipments)
        {
            var user = _users[userId];
            user.IsAvailableForAssignment = isAvailableForAssignment;
            user.MaxActiveShipments = maxActiveShipments;
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
                true,
                30,
                [],
                TestClock.UtcNow);

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

        public Task<Result> SetShipperCapacityAsync(
            Guid userId,
            bool isAvailableForAssignment,
            int maxActiveShipments,
            CancellationToken cancellationToken = default)
        {
            if (!_users.TryGetValue(userId, out var user))
            {
                return Task.FromResult(Result.Failure(ApplicationErrors.NotFound("User was not found.")));
            }

            user.IsAvailableForAssignment = isAvailableForAssignment;
            user.MaxActiveShipments = maxActiveShipments;
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
                .Select(user => new ActiveShipperResponse(
                    user.UserId,
                    user.FullName,
                    user.Email,
                    user.PhoneNumber,
                    user.IsAvailableForAssignment,
                    user.MaxActiveShipments))
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
                    user.IsAvailableForAssignment,
                    user.MaxActiveShipments,
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
                .Select(user => new IdentityUserSummaryResponse(
                    user.UserId,
                    user.FullName,
                    user.Email,
                    user.PhoneNumber,
                    user.IsActive,
                    user.IsAvailableForAssignment,
                    user.MaxActiveShipments))
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
                bool isAvailableForAssignment,
                int maxActiveShipments,
                HashSet<string> roles,
                DateTimeOffset createdAtUtc)
            {
                UserId = userId;
                FullName = fullName;
                Email = email;
                PhoneNumber = phoneNumber;
                IsActive = isActive;
                IsAvailableForAssignment = isAvailableForAssignment;
                MaxActiveShipments = maxActiveShipments;
                Roles = roles;
                CreatedAtUtc = createdAtUtc;
            }

            public Guid UserId { get; }

            public string FullName { get; }

            public string Email { get; }

            public string? PhoneNumber { get; }

            public bool IsActive { get; set; }

            public bool IsAvailableForAssignment { get; set; }

            public int MaxActiveShipments { get; set; }

            public HashSet<string> Roles { get; }

            public DateTimeOffset CreatedAtUtc { get; }
        }
    }

    private sealed class FakeHubRepository : IHubRepository
    {
        private readonly List<Hub> _hubs;

        public FakeHubRepository(IReadOnlyList<Hub> hubs)
        {
            _hubs = hubs.ToList();
        }

        public Task<IReadOnlyList<Hub>> GetAllAsync(
            bool activeOnly = false,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Hub> hubs = _hubs
                .Where(hub => !activeOnly || hub.IsActive)
                .ToList();

            return Task.FromResult(hubs);
        }

        public Task<IReadOnlyList<Hub>> GetByIdsAsync(
            IReadOnlyCollection<Guid> hubIds,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Hub> hubs = _hubs
                .Where(hub => hubIds.Contains(hub.Id))
                .ToList();

            return Task.FromResult(hubs);
        }

        public Task<Hub?> GetByIdAsync(
            Guid hubId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_hubs.FirstOrDefault(hub => hub.Id == hubId));
        }

        public Task<Hub?> GetByCodeAsync(
            string code,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_hubs.FirstOrDefault(hub =>
                string.Equals(hub.Code, code, StringComparison.OrdinalIgnoreCase)));
        }

        public Task AddAsync(Hub hub, CancellationToken cancellationToken = default)
        {
            _hubs.Add(hub);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeShipperWorkingAreaRepository : IShipperWorkingAreaRepository
    {
        private readonly List<ShipperWorkingArea> _workingAreas;

        public FakeShipperWorkingAreaRepository(IReadOnlyList<ShipperWorkingArea> workingAreas)
        {
            _workingAreas = workingAreas.ToList();
        }

        public int SaveChangesCount { get; private set; }

        public IReadOnlyList<ShipperWorkingArea> WorkingAreas => _workingAreas.AsReadOnly();

        public Task<IReadOnlyList<ShipperWorkingArea>> GetByShipperIdAsync(
            Guid shipperId,
            bool activeOnly = false,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ShipperWorkingArea> workingAreas = _workingAreas
                .Where(area => area.ShipperId == shipperId)
                .Where(area => !activeOnly || area.IsActive)
                .ToList();

            return Task.FromResult(workingAreas);
        }

        public Task<IReadOnlyList<ShipperWorkingArea>> GetActiveByShipperIdsAsync(
            IReadOnlyCollection<Guid> shipperIds,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ShipperWorkingArea> workingAreas = _workingAreas
                .Where(area => area.IsActive && shipperIds.Contains(area.ShipperId))
                .ToList();

            return Task.FromResult(workingAreas);
        }

        public Task<IReadOnlyList<ShipperWorkingArea>> GetActiveByHubOrProvinceAsync(
            Guid? hubId,
            string province,
            string? ward = null,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ShipperWorkingArea> workingAreas = _workingAreas
                .Where(area => area.IsActive)
                .Where(area => area.Province == province || (hubId.HasValue && area.HubId == hubId.Value))
                .ToList();

            return Task.FromResult(workingAreas);
        }

        public Task<int> CountActiveByHubIdAsync(
            Guid hubId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_workingAreas.Count(area => area.IsActive && area.HubId == hubId));
        }

        public Task AddAsync(ShipperWorkingArea workingArea, CancellationToken cancellationToken = default)
        {
            _workingAreas.Add(workingArea);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWebhookEventPublisher : IWebhookEventPublisher
    {
        public int PublishCount { get; private set; }

        public string? LastEventType { get; private set; }

        public Task PublishShipmentAsync(
            Shipment shipment,
            string eventType,
            CancellationToken cancellationToken = default)
        {
            PublishCount++;
            LastEventType = eventType;
            return Task.CompletedTask;
        }

        public Task PublishShipmentAsync(
            Shipment shipment,
            ExternalShipmentReference reference,
            string eventType,
            CancellationToken cancellationToken = default)
        {
            return PublishShipmentAsync(shipment, eventType, cancellationToken);
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
