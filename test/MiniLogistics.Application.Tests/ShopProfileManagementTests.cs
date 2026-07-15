using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shops;
using MiniLogistics.Application.Shops.GetShopProfile;
using MiniLogistics.Application.Shops.SetShopActiveStatus;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Application.Shops.UpdateShopProfile;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.Users;
using MiniLogistics.Domain.ValueObjects;
using Xunit;

namespace MiniLogistics.Application.Tests;

public sealed class ShopProfileManagementTests
{
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _operatorId = Guid.NewGuid();
    private readonly Guid _shopOwnerId = Guid.NewGuid();

    [Fact]
    public async Task GetShopProfile_InactiveShop_ReturnsReadOnlyProfile()
    {
        var identityService = CreateIdentityService();
        var shop = CreateShop();
        var repository = new FakeShopRepository([shop]);
        shop.Deactivate();
        var service = new GetShopProfileService(new ShopAccessService(identityService, repository));

        var result = await service.GetAsync(_shopOwnerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(shop.Id, result.Value.ShopId);
        Assert.False(result.Value.IsActive);
    }

    [Fact]
    public async Task UpdateShopProfile_ActiveShopOwner_UpdatesProfile()
    {
        var shop = CreateShop();
        var repository = new FakeShopRepository([shop]);
        var service = new UpdateShopProfileService(
            new UpdateShopProfileCommandValidator(),
            new ShopAccessService(CreateIdentityService(), repository),
            repository);

        var result = await service.UpdateAsync(new UpdateShopProfileCommand(
            _shopOwnerId,
            "Updated Shop",
            "0987654321",
            "99 Nguyen Hue",
            "Ben Nghe",
            "Ho Chi Minh"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Shop", shop.Name);
        Assert.Equal("0987654321", shop.PhoneNumber.Value);
        Assert.Equal("99 Nguyen Hue", shop.Address.Street);
        Assert.NotNull(shop.UpdatedAtUtc);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task UpdateShopProfile_InactiveShop_IsRejected()
    {
        var shop = CreateShop();
        shop.Deactivate();
        var repository = new FakeShopRepository([shop]);
        var service = new UpdateShopProfileService(
            new UpdateShopProfileCommandValidator(),
            new ShopAccessService(CreateIdentityService(), repository),
            repository);

        var result = await service.UpdateAsync(new UpdateShopProfileCommand(
            _shopOwnerId,
            "Blocked Update",
            "0987654321",
            "99 Nguyen Hue",
            "Ben Nghe",
            "Ho Chi Minh"));

        Assert.True(result.IsFailure);
        Assert.Equal("Application.Forbidden", result.Error.Code);
        Assert.Equal("Demo Shop", shop.Name);
        Assert.Equal(0, repository.SaveChangesCount);
    }

    [Fact]
    public async Task UpdateShopProfile_NonShopUser_IsRejected()
    {
        var shop = CreateShop();
        var repository = new FakeShopRepository([shop]);
        var service = new UpdateShopProfileService(
            new UpdateShopProfileCommandValidator(),
            new ShopAccessService(CreateIdentityService(), repository),
            repository);

        var result = await service.UpdateAsync(new UpdateShopProfileCommand(
            _operatorId,
            "Blocked Update",
            "0987654321",
            "99 Nguyen Hue",
            "Ben Nghe",
            "Ho Chi Minh"));

        Assert.True(result.IsFailure);
        Assert.Equal("Application.Forbidden", result.Error.Code);
        Assert.Equal(0, repository.SaveChangesCount);
    }

    [Fact]
    public async Task SetShopActiveStatus_ActiveAdmin_DeactivatesAndReactivatesShop()
    {
        var shop = CreateShop();
        var repository = new FakeShopRepository([shop]);
        var service = new SetShopActiveStatusService(
            new SetShopActiveStatusCommandValidator(),
            CreateIdentityService(),
            repository);

        var deactivateResult = await service.SetAsync(new SetShopActiveStatusCommand(
            _adminId,
            shop.Id,
            false));
        var reactivateResult = await service.SetAsync(new SetShopActiveStatusCommand(
            _adminId,
            shop.Id,
            true));

        Assert.True(deactivateResult.IsSuccess);
        Assert.True(reactivateResult.IsSuccess);
        Assert.True(shop.IsActive);
        Assert.Equal(2, repository.SaveChangesCount);
    }

    [Fact]
    public async Task SetShopActiveStatus_NonAdmin_IsRejected()
    {
        var shop = CreateShop();
        var repository = new FakeShopRepository([shop]);
        var service = new SetShopActiveStatusService(
            new SetShopActiveStatusCommandValidator(),
            CreateIdentityService(),
            repository);

        var result = await service.SetAsync(new SetShopActiveStatusCommand(
            _operatorId,
            shop.Id,
            false));

        Assert.True(result.IsFailure);
        Assert.Equal("Application.Forbidden", result.Error.Code);
        Assert.True(shop.IsActive);
        Assert.Equal(0, repository.SaveChangesCount);
    }

    private Shop CreateShop()
    {
        return new Shop(
            _shopOwnerId,
            "Demo Shop",
            new PhoneNumber("0912345678"),
            new Address(
                "1 Le Loi",
                "Ben Thanh",
                "Ho Chi Minh"));
    }

    private FakeIdentityService CreateIdentityService()
    {
        return new FakeIdentityService([
            new FakeIdentityService.FakeUser(_adminId, true, [nameof(UserRole.Admin)]),
            new FakeIdentityService.FakeUser(_operatorId, true, [nameof(UserRole.Operator)]),
            new FakeIdentityService.FakeUser(_shopOwnerId, true, [nameof(UserRole.Shop)])
        ]);
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

        public Task<Result> AddToRoleAsync(
            Guid userId,
            string role,
            CancellationToken cancellationToken = default)
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
            IReadOnlyList<IdentityUserSummaryResponse> users = userIds
                .Where(_users.ContainsKey)
                .Select(userId => _users[userId])
                .Select(user => new IdentityUserSummaryResponse(
                    user.UserId,
                    $"User {user.UserId:N}"[..12],
                    $"{user.UserId:N}@example.test",
                    "0900000000",
                    user.IsActive,
                    true,
                    30))
                .ToList();

            return Task.FromResult(users);
        }

        public sealed record FakeUser(
            Guid UserId,
            bool IsActive,
            HashSet<string> Roles);
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
}
