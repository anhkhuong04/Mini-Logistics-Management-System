using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shipments.GetShipmentsForCurrentShop;
using MiniLogistics.Application.Shops;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Application.Shops.Staff;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;
using Xunit;

namespace MiniLogistics.Application.Tests;

public sealed class ShopStaffPermissionTests
{
    [Fact]
    public async Task ShopAccess_StaffMembershipGrantsOnlyAssignedPermissions()
    {
        var fixture = CreateViewerFixture();

        var viewResult = await fixture.AccessService.GetShopAccessAsync(
            fixture.StaffUserId,
            fixture.Shop.Id,
            requireActiveShop: false,
            ShopPermission.ViewShipments);
        var manageResult = await fixture.AccessService.GetShopAccessAsync(
            fixture.StaffUserId,
            fixture.Shop.Id,
            requireActiveShop: false,
            ShopPermission.ManageShipments);

        Assert.True(viewResult.IsSuccess);
        Assert.False(viewResult.Value.IsOwner);
        Assert.Equal(ShopStaffRole.Viewer, viewResult.Value.StaffRole);
        Assert.Equal(ShopPermission.ViewShipments, viewResult.Value.Permissions);
        Assert.True(manageResult.IsFailure);
        Assert.Equal("Application.Forbidden", manageResult.Error.Code);
    }

    [Fact]
    public async Task ShipmentList_ViewerReceivesMaskedPiiAndNoCod()
    {
        var fixture = CreateViewerFixture();
        var shipment = CreateShipment(fixture.Shop, fixture.OwnerUserId);
        var service = new GetShipmentsForCurrentShopService(
            fixture.AccessService,
            new SingleShipmentReadRepository(shipment),
            new PiiMaskingService());

        var result = await service.GetAsync(fixture.StaffUserId, fixture.Shop.Id);
        var receiverFilterResult = await service.SearchAsync(new GetShipmentsForCurrentShopQuery(
            fixture.StaffUserId,
            fixture.Shop.Id,
            ReceiverNameSearch: "Receiver"));
        var receiverSortResult = await service.SearchAsync(new GetShipmentsForCurrentShopQuery(
            fixture.StaffUserId,
            fixture.Shop.Id,
            SortBy: ShopShipmentSortBy.ReceiverName));

        Assert.True(result.IsSuccess);
        Assert.Equal("R*** P***", result.Value.Single().ReceiverName);
        Assert.Equal(0m, result.Value.Single().CodAmount);
        Assert.True(receiverFilterResult.IsFailure);
        Assert.Equal("Application.Forbidden", receiverFilterResult.Error.Code);
        Assert.True(receiverSortResult.IsFailure);
        Assert.Equal("Application.Forbidden", receiverSortResult.Error.Code);
    }

    [Fact]
    public async Task StaffManagement_RejectsNonOwnerMembership()
    {
        var fixture = CreateViewerFixture();
        var service = new ShopStaffManagementService(
            fixture.AccessService,
            fixture.MembershipRepository,
            fixture.IdentityService,
            NullAdminAuditService.Instance,
            TestClock.Provider);

        var result = await service.GetAsync(fixture.StaffUserId, fixture.Shop.Id);

        Assert.True(result.IsFailure);
        Assert.Equal("Application.Forbidden", result.Error.Code);
    }

    private static ViewerFixture CreateViewerFixture()
    {
        var ownerUserId = Guid.NewGuid();
        var staffUserId = Guid.NewGuid();
        var shop = new Shop(
            ownerUserId,
            "Permission Shop",
            new PhoneNumber("0900000000"),
            new Address("1 Le Loi", "Ben Thanh", "Ho Chi Minh"),
            TestClock.UtcNow);
        var membership = new ShopStaffMembership(
            shop.Id,
            staffUserId,
            ownerUserId,
            ShopStaffRole.Viewer,
            ShopPermission.ViewShipments,
            TestClock.UtcNow);
        var identityService = new ActiveShopIdentityService(staffUserId);
        var membershipRepository = new FakeMembershipRepository(membership);
        var accessService = new ShopAccessService(
            identityService,
            new FakeShopRepository(shop),
            membershipRepository);

        return new ViewerFixture(
            ownerUserId,
            staffUserId,
            shop,
            identityService,
            membershipRepository,
            accessService);
    }

    private static Shipment CreateShipment(Shop shop, Guid ownerUserId) => Shipment.Create(
        shop.Id,
        "Sender Person",
        new PhoneNumber("0912345678"),
        "Receiver Person",
        new PhoneNumber("0987654321"),
        shop.Address,
        new Address("2 Le Loi", "Ben Thanh", "Ho Chi Minh"),
        new Weight(1),
        new ParcelDimensions(10, 10, 10),
        new Weight(1),
        new Money(100_000, "VND"),
        new Money(50_000, "VND"),
        new ShippingFeeBreakdown(Money.Zero, Money.Zero, Money.Zero, Money.Zero),
        RouteType.IntraProvince,
        ownerUserId,
        TestClock.UtcNow);

    private sealed record ViewerFixture(
        Guid OwnerUserId,
        Guid StaffUserId,
        Shop Shop,
        ActiveShopIdentityService IdentityService,
        FakeMembershipRepository MembershipRepository,
        ShopAccessService AccessService);

    private sealed class ActiveShopIdentityService(Guid staffUserId) : IIdentityService
    {
        public Task<Result<Guid>> CreateUserAsync(string fullName, string email, string phoneNumber, string password, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result> AddToRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<Guid>> CreateInternalUserAsync(string fullName, string email, string phoneNumber, string password, string role, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result> SetUserActiveStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result> SetShipperCapacityAsync(Guid userId, bool isAvailableForAssignment, int maxActiveShipments, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IdentityUserRoleCheckResponse> CheckUserRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default) =>
            Task.FromResult(new IdentityUserRoleCheckResponse(
                userId,
                userId == staffUserId,
                userId == staffUserId,
                userId == staffUserId && role == "Shop"));

        public Task<IReadOnlyList<IdentityUserWithRolesResponse>> ListUsersWithRolesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ActiveShipperResponse>> GetActiveShippersAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<IdentityUserSummaryResponse>> GetUsersByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IdentityUserSummaryResponse>>([]);
    }

    private sealed class FakeShopRepository(Shop shop) : IShopRepository
    {
        public Task<Shop?> GetByIdAsync(Guid shopId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Shop?>(shop.Id == shopId ? shop : null);

        public Task<Shop?> GetByOwnerUserIdAsync(Guid ownerUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Shop?>(shop.OwnerUserId == ownerUserId ? shop : null);

        public Task<IReadOnlyList<Shop>> GetAllByOwnerUserIdAsync(Guid ownerUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Shop>>(shop.OwnerUserId == ownerUserId ? [shop] : []);

        public Task<IReadOnlyList<Shop>> GetByIdsAsync(IReadOnlyCollection<Guid> shopIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Shop>>(shopIds.Contains(shop.Id) ? [shop] : []);

        public Task<IReadOnlyList<Shop>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Shop>>([shop]);

        public Task<bool> ExistsByOwnerUserIdAsync(Guid ownerUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(shop.OwnerUserId == ownerUserId);

        public Task AddAsync(Shop value, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeMembershipRepository(ShopStaffMembership membership) : IShopStaffMembershipRepository
    {
        public Task<IReadOnlyList<ShopStaffMembership>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ShopStaffMembership>>(
                membership.UserId == userId && membership.IsActive ? [membership] : []);

        public Task<IReadOnlyList<ShopStaffMembership>> GetByShopIdAsync(Guid shopId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ShopStaffMembership>>(membership.ShopId == shopId ? [membership] : []);

        public Task<ShopStaffMembership?> GetByShopAndUserAsync(Guid shopId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ShopStaffMembership?>(
                membership.ShopId == shopId && membership.UserId == userId ? membership : null);

        public Task AddAsync(ShopStaffMembership value, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class SingleShipmentReadRepository(Shipment shipment) : IShipmentReadRepository
    {
        public Task<bool> ExistsByTrackingCodeAsync(TrackingCode trackingCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(shipment.TrackingCode == trackingCode);

        public Task<IReadOnlyList<Shipment>> GetByShopIdAsync(Guid shopId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Shipment>>(shipment.ShopId == shopId ? [shipment] : []);

        public Task<IReadOnlyList<Shipment>> GetByStatusAsync(ShipmentStatus status, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Shipment>>(shipment.Status == status ? [shipment] : []);

        public Task<IReadOnlyList<Shipment>> GetByStatusesAsync(IReadOnlyCollection<ShipmentStatus> statuses, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Shipment>>(statuses.Contains(shipment.Status) ? [shipment] : []);

        public Task<IReadOnlyList<Shipment>> GetByIdsAsync(IReadOnlyCollection<Guid> shipmentIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Shipment>>(shipmentIds.Contains(shipment.Id) ? [shipment] : []);

        public Task<IReadOnlyList<Shipment>> GetAssignedToShipperAsync(Guid shipperId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Shipment>>([]);

        public Task<IReadOnlyDictionary<Guid, int>> GetActiveAssignmentCountsByShipperIdsAsync(IReadOnlyCollection<Guid> shipperIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());

        public Task<Shipment?> GetByIdAndShopIdAsync(Guid shipmentId, Guid shopId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Shipment?>(shipment.Id == shipmentId && shipment.ShopId == shopId ? shipment : null);

        public Task<Shipment?> GetTrackedByIdAndShopIdAsync(Guid shipmentId, Guid shopId, CancellationToken cancellationToken = default) =>
            GetByIdAndShopIdAsync(shipmentId, shopId, cancellationToken);

        public Task<Shipment?> GetTrackedByIdAsync(Guid shipmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Shipment?>(shipment.Id == shipmentId ? shipment : null);

        public Task<Shipment?> GetByTrackingCodeAsync(TrackingCode trackingCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<Shipment?>(shipment.TrackingCode == trackingCode ? shipment : null);

        public Task<Shipment?> GetByTrackingCodeAndShopIdAsync(TrackingCode trackingCode, Guid shopId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Shipment?>(shipment.TrackingCode == trackingCode && shipment.ShopId == shopId ? shipment : null);

        public Task<Shipment?> GetTrackedByTrackingCodeAndShopIdAsync(TrackingCode trackingCode, Guid shopId, CancellationToken cancellationToken = default) =>
            GetByTrackingCodeAndShopIdAsync(trackingCode, shopId, cancellationToken);
    }
}
