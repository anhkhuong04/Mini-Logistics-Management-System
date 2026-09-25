using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shops;
using MiniLogistics.Application.Shops.RegisterShop;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class ConcurrencyAndRegistrationTransactionTests : IClassFixture<LocalDbIntegrationFixture>
{
    private static readonly Guid DemoShopUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TestActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private readonly LocalDbIntegrationFixture _fixture;

    public ConcurrencyAndRegistrationTransactionTests(LocalDbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConcurrentShipmentAndCodUpdates_OnlyOneContextCanCommit()
    {
        var shipmentId = await CreateShipmentWithCodAsync();

        await using var shipmentScopeA = _fixture.ServiceProvider.CreateAsyncScope();
        await using var shipmentScopeB = _fixture.ServiceProvider.CreateAsyncScope();
        var shipmentRepositoryA = shipmentScopeA.ServiceProvider.GetRequiredService<IShipmentRepository>();
        var shipmentRepositoryB = shipmentScopeB.ServiceProvider.GetRequiredService<IShipmentRepository>();
        var shipmentA = await shipmentRepositoryA.GetTrackedByIdAsync(shipmentId);
        var shipmentB = await shipmentRepositoryB.GetTrackedByIdAsync(shipmentId);
        Assert.NotNull(shipmentA);
        Assert.NotNull(shipmentB);

        Assert.True(shipmentA.UpdateStatus(ShipmentStatus.Cancelled, TestActorId, DateTimeOffset.UtcNow, "A").IsSuccess);
        Assert.True(shipmentB.UpdateStatus(ShipmentStatus.Assigned, TestActorId, DateTimeOffset.UtcNow, "B").IsSuccess);
        await shipmentRepositoryA.SaveChangesAsync();

        var shipmentConflict = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => shipmentRepositoryB.SaveChangesAsync());
        Assert.IsType<DbUpdateConcurrencyException>(shipmentConflict.InnerException);

        await using var codScopeA = _fixture.ServiceProvider.CreateAsyncScope();
        await using var codScopeB = _fixture.ServiceProvider.CreateAsyncScope();
        var codRepositoryA = codScopeA.ServiceProvider.GetRequiredService<MiniLogistics.Application.CashOnDelivery.ICodTransactionRepository>();
        var codRepositoryB = codScopeB.ServiceProvider.GetRequiredService<MiniLogistics.Application.CashOnDelivery.ICodTransactionRepository>();
        var codA = await codRepositoryA.GetTrackedByShipmentIdAsync(shipmentId);
        var codB = await codRepositoryB.GetTrackedByShipmentIdAsync(shipmentId);
        Assert.NotNull(codA);
        Assert.NotNull(codB);

        Assert.True(codA.UpdateAmount(new Money(200_000m), DateTimeOffset.UtcNow).IsSuccess);
        Assert.True(codB.UpdateAmount(new Money(300_000m), DateTimeOffset.UtcNow).IsSuccess);
        await codRepositoryA.SaveChangesAsync();

        var codConflict = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => codRepositoryB.SaveChangesAsync());
        Assert.IsType<DbUpdateConcurrencyException>(codConflict.InnerException);

        await _fixture.ExecuteAsync(async services =>
        {
            var dbContext = services.GetRequiredService<MiniLogisticsDbContext>();
            var persistedShipment = await dbContext.Shipments
                .Include(shipment => shipment.StatusHistory)
                .SingleAsync(shipment => shipment.Id == shipmentId);
            var persistedCod = await dbContext.CodTransactions
                .SingleAsync(cod => cod.ShipmentId == shipmentId);

            Assert.Equal(ShipmentStatus.Cancelled, persistedShipment.Status);
            Assert.Equal(2, persistedShipment.StatusHistory.Count);
            Assert.Equal(200_000m, persistedCod.Amount.Amount);
            Assert.NotNull(dbContext.Entry(persistedShipment).Property<byte[]>("RowVersion").CurrentValue);
            Assert.NotNull(dbContext.Entry(persistedCod).Property<byte[]>("RowVersion").CurrentValue);
        });
    }

    [Fact]
    public async Task RegisterShop_WhenRoleAssignmentFails_RollsBackCreatedIdentityUser()
    {
        var email = $"shop-{Guid.NewGuid():N}@example.test";
        var result = await _fixture.ExecuteAsync(async services =>
        {
            var service = new RegisterShopService(
                services.GetRequiredService<IValidator<RegisterShopCommand>>(),
                new RoleFailureIdentityService(services.GetRequiredService<IIdentityService>()),
                services.GetRequiredService<IShopRepository>(),
                services.GetRequiredService<TimeProvider>(),
                services.GetRequiredService<IApplicationDbTransactionManager>(),
                services.GetRequiredService<ILogger<RegisterShopService>>());

            return await service.RegisterAsync(new RegisterShopCommand(
                "Atomic Registration Test",
                email,
                "P@ssword12345!",
                "Atomic Registration Shop",
                "09876543210",
                "1 Test Street",
                "Test Ward",
                "Ho Chi Minh City",
                "Vietnam"));
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Test.RoleAssignmentFailed", result.Error.Code);

        await _fixture.ExecuteAsync(async services =>
        {
            var dbContext = services.GetRequiredService<MiniLogisticsDbContext>();
            Assert.False(await dbContext.Users.AnyAsync(user => user.Email == email));
            Assert.False(await dbContext.Shops.AnyAsync(shop => shop.OwnerUserId != DemoShopUserId
                && dbContext.Users.Any(user => user.Id == shop.OwnerUserId && user.Email == email)));
        });
    }

    [Fact]
    public async Task RegisterShop_WhenUserCreationFails_PersistsNoIdentityOrShopState()
    {
        var email = $"shop-{Guid.NewGuid():N}@example.test";
        var result = await _fixture.ExecuteAsync(async services =>
        {
            var service = new RegisterShopService(
                services.GetRequiredService<IValidator<RegisterShopCommand>>(),
                new UserFailureIdentityService(),
                services.GetRequiredService<IShopRepository>(),
                services.GetRequiredService<TimeProvider>(),
                services.GetRequiredService<IApplicationDbTransactionManager>(),
                services.GetRequiredService<ILogger<RegisterShopService>>());

            return await service.RegisterAsync(CreateRegistrationCommand(email));
        });

        Assert.True(result.IsFailure);
        await AssertRegistrationAbsentAsync(email);
    }

    [Fact]
    public async Task RegisterShop_WhenShopSaveFails_RollsBackUserAndRole()
    {
        var email = $"shop-{Guid.NewGuid():N}@example.test";
        var result = await _fixture.ExecuteAsync(async services =>
        {
            var service = new RegisterShopService(
                services.GetRequiredService<IValidator<RegisterShopCommand>>(),
                services.GetRequiredService<IIdentityService>(),
                new ShopSaveFailureRepository(services.GetRequiredService<IShopRepository>()),
                services.GetRequiredService<TimeProvider>(),
                services.GetRequiredService<IApplicationDbTransactionManager>(),
                services.GetRequiredService<ILogger<RegisterShopService>>());

            return await service.RegisterAsync(CreateRegistrationCommand(email));
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Application.RegistrationFailed", result.Error.Code);
        await AssertRegistrationAbsentAsync(email);
    }

    [Fact]
    public async Task RegisterShop_SuccessPersistsUserRoleAndShop()
    {
        var email = $"shop-{Guid.NewGuid():N}@example.test";
        var result = await _fixture.ExecuteAsync(async services =>
        {
            var service = services.GetRequiredService<IRegisterShopService>();
            return await service.RegisterAsync(CreateRegistrationCommand(email));
        });

        Assert.True(result.IsSuccess);
        await _fixture.ExecuteAsync(async services =>
        {
            var dbContext = services.GetRequiredService<MiniLogisticsDbContext>();
            Assert.True(await dbContext.Users.AnyAsync(user => user.Id == result.Value.UserId && user.Email == email));
            Assert.True(await dbContext.UserRoles.AnyAsync(role => role.UserId == result.Value.UserId));
            Assert.True(await dbContext.Shops.AnyAsync(shop => shop.Id == result.Value.ShopId
                && shop.OwnerUserId == result.Value.UserId));
        });
    }

    private static RegisterShopCommand CreateRegistrationCommand(string email) => new(
        "Atomic Registration Test",
        email,
        "P@ssword12345!",
        "Atomic Registration Shop",
        "09876543210",
        "1 Test Street",
        "Test Ward",
        "Ho Chi Minh City",
        "Vietnam");

    private async Task AssertRegistrationAbsentAsync(string email)
    {
        await _fixture.ExecuteAsync(async services =>
        {
            var dbContext = services.GetRequiredService<MiniLogisticsDbContext>();
            Assert.False(await dbContext.Users.AnyAsync(user => user.Email == email));
            Assert.False(await dbContext.Shops.AnyAsync(shop =>
                dbContext.Users.Any(user => user.Id == shop.OwnerUserId && user.Email == email)));
        });
    }

    private async Task<Guid> CreateShipmentWithCodAsync()
    {
        var shopId = await _fixture.ExecuteAsync(async services =>
            await services.GetRequiredService<MiniLogisticsDbContext>().Shops
                .Where(shop => shop.OwnerUserId == DemoShopUserId)
                .Select(shop => shop.Id)
                .SingleAsync());

        var shipment = Shipment.Create(
            shopId,
            "Concurrency Sender",
            new PhoneNumber("0900000001"),
            "Concurrency Receiver",
            new PhoneNumber("0900000002"),
            new Address("1 Test Street", "Test Ward", "Ho Chi Minh City"),
            new Address("2 Test Street", "Test Ward", "Ho Chi Minh City"),
            new Weight(1m),
            new ParcelDimensions(10m, 10m, 10m),
            new Weight(1m),
            new Money(100_000m),
            new Money(150_000m),
            new ShippingFeeBreakdown(new Money(25_000m), Money.Zero, Money.Zero, Money.Zero),
            RouteType.IntraRegion,
            TestActorId,
            DateTimeOffset.UtcNow,
            trackingCode: TrackingCode.Generate(DateTimeOffset.UtcNow));

        return await _fixture.ExecuteAsync(async services =>
        {
            var dbContext = services.GetRequiredService<MiniLogisticsDbContext>();
            dbContext.Shipments.Add(shipment);
            dbContext.CodTransactions.Add(MiniLogistics.Domain.CashOnDelivery.CodTransaction.Create(
                shipment.Id,
                shipment.CodAmount,
                DateTimeOffset.UtcNow));
            await dbContext.SaveChangesAsync();
            return shipment.Id;
        });
    }

    private sealed class RoleFailureIdentityService(IIdentityService inner) : IIdentityService
    {
        public Task<Result<Guid>> CreateUserAsync(
            string fullName,
            string email,
            string phoneNumber,
            string password,
            CancellationToken cancellationToken = default) =>
            inner.CreateUserAsync(fullName, email, phoneNumber, password, cancellationToken);

        public Task<Result> AddToRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure(new Error("Test.RoleAssignmentFailed", "Injected role assignment failure.")));

        public Task<Result<Guid>> CreateInternalUserAsync(string fullName, string email, string phoneNumber, string password, string role, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result> SetUserActiveStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result> SetShipperCapacityAsync(Guid userId, bool isAvailableForAssignment, int maxActiveShipments, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IdentityUserRoleCheckResponse> CheckUserRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<IdentityUserWithRolesResponse>> ListUsersWithRolesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ActiveShipperResponse>> GetActiveShippersAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<IdentityUserSummaryResponse>> GetUsersByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class UserFailureIdentityService : IIdentityService
    {
        public Task<Result<Guid>> CreateUserAsync(string fullName, string email, string phoneNumber, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<Guid>.Failure(new Error("Test.UserCreationFailed", "Injected user creation failure.")));

        public Task<Result> AddToRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<Guid>> CreateInternalUserAsync(string fullName, string email, string phoneNumber, string password, string role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> SetUserActiveStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> SetShipperCapacityAsync(Guid userId, bool isAvailableForAssignment, int maxActiveShipments, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IdentityUserRoleCheckResponse> CheckUserRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<IdentityUserWithRolesResponse>> ListUsersWithRolesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ActiveShipperResponse>> GetActiveShippersAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<IdentityUserSummaryResponse>> GetUsersByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ShopSaveFailureRepository(IShopRepository inner) : IShopRepository
    {
        public Task<MiniLogistics.Domain.Shops.Shop?> GetByIdAsync(Guid shopId, CancellationToken cancellationToken = default) => inner.GetByIdAsync(shopId, cancellationToken);
        public Task<MiniLogistics.Domain.Shops.Shop?> GetByOwnerUserIdAsync(Guid ownerUserId, CancellationToken cancellationToken = default) => inner.GetByOwnerUserIdAsync(ownerUserId, cancellationToken);
        public Task<IReadOnlyList<MiniLogistics.Domain.Shops.Shop>> GetAllByOwnerUserIdAsync(Guid ownerUserId, CancellationToken cancellationToken = default) => inner.GetAllByOwnerUserIdAsync(ownerUserId, cancellationToken);
        public Task<IReadOnlyList<MiniLogistics.Domain.Shops.Shop>> GetByIdsAsync(IReadOnlyCollection<Guid> shopIds, CancellationToken cancellationToken = default) => inner.GetByIdsAsync(shopIds, cancellationToken);
        public Task<IReadOnlyList<MiniLogistics.Domain.Shops.Shop>> GetAllAsync(CancellationToken cancellationToken = default) => inner.GetAllAsync(cancellationToken);
        public Task<bool> ExistsByOwnerUserIdAsync(Guid ownerUserId, CancellationToken cancellationToken = default) => inner.ExistsByOwnerUserIdAsync(ownerUserId, cancellationToken);
        public Task AddAsync(MiniLogistics.Domain.Shops.Shop shop, CancellationToken cancellationToken = default) => inner.AddAsync(shop, cancellationToken);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromException(new InvalidOperationException("Injected shop persistence failure."));
    }
}
