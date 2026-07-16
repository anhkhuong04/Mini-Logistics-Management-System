using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.AdminHubs.CreateHub;
using MiniLogistics.Application.AdminHubs.SetHubActiveStatus;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Operations;
using MiniLogistics.Domain.Users;
using Xunit;

namespace MiniLogistics.Application.Tests;

public sealed class AdminHubManagementTests
{
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _shipperId = Guid.NewGuid();

    [Fact]
    public async Task CreateHub_DuplicateCode_ReturnsConflict()
    {
        var existingHub = new Hub("HCM-01", "Existing", "Ho Chi Minh");
        var service = new CreateHubService(
            new CreateHubCommandValidator(),
            CreateIdentityService(),
            new FakeHubRepository([existingHub]),
            new FakeAdminAuditService());

        var result = await service.CreateAsync(new CreateHubCommand(
            _adminId,
            " hcm-01 ",
            "Duplicate",
            "Ho Chi Minh",
            null,
            null,
            "Vietnam",
            false));

        Assert.True(result.IsFailure);
        Assert.Equal("Application.Conflict", result.Error.Code);
    }

    [Fact]
    public async Task CreateHub_WritesAuditLog()
    {
        var auditService = new FakeAdminAuditService();
        var service = new CreateHubService(
            new CreateHubCommandValidator(),
            CreateIdentityService(),
            new FakeHubRepository([]),
            auditService);

        var result = await service.CreateAsync(new CreateHubCommand(
            _adminId,
            "HN-01",
            "Ha Noi Hub",
            "Ha Noi",
            "Hoan Kiem",
            "1 Hang Bai",
            "Vietnam",
            true));

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal("HN-01", result.Value.Code);
        var auditEntry = Assert.Single(auditService.Entries);
        Assert.Equal(AdminAuditActions.HubCreated, auditEntry.Action);
        Assert.Equal(AdminAuditTargetTypes.Hub, auditEntry.TargetType);
        Assert.Equal(result.Value.HubId, auditEntry.TargetId);
    }

    [Fact]
    public async Task SetHubInactive_ReferencedHubKeepsWorkingAreasAndWritesAudit()
    {
        var hub = new Hub("HCM-01", "Ho Chi Minh Hub", "Ho Chi Minh");
        var workingArea = new ShipperWorkingArea(_shipperId, hub.Id, hub.Province);
        var workingAreaRepository = new FakeShipperWorkingAreaRepository([workingArea]);
        var hubRepository = new FakeHubRepository([hub]);
        var auditService = new FakeAdminAuditService();
        var service = new SetHubActiveStatusService(
            new SetHubActiveStatusCommandValidator(),
            CreateIdentityService(),
            hubRepository,
            workingAreaRepository,
            auditService);

        var result = await service.SetAsync(new SetHubActiveStatusCommand(
            _adminId,
            hub.Id,
            false,
            "Hub closed for relocation."));

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.False(hub.IsActive);
        Assert.True(workingArea.IsActive);
        Assert.Equal(1, result.Value.ActiveWorkingAreaCount);
        Assert.Equal(1, hubRepository.SaveChangesCount);

        var auditEntry = Assert.Single(auditService.Entries);
        Assert.Equal(AdminAuditActions.HubActiveStatusChanged, auditEntry.Action);
        Assert.Equal(AdminAuditTargetTypes.Hub, auditEntry.TargetType);
        Assert.Equal("Hub closed for relocation.", auditEntry.Reason);
    }

    private FakeIdentityService CreateIdentityService()
    {
        return new FakeIdentityService([
            new FakeIdentityService.FakeUser(_adminId, true, [nameof(UserRole.Admin)]),
            new FakeIdentityService.FakeUser(_shipperId, true, [nameof(UserRole.Shipper)])
        ]);
    }

    private sealed class FakeAdminAuditService : IAdminAuditService
    {
        public List<AdminAuditEntry> Entries { get; } = [];

        public Task RecordAsync(AdminAuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
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

    private sealed class FakeHubRepository : IHubRepository
    {
        private readonly List<Hub> _hubs;

        public FakeHubRepository(IReadOnlyList<Hub> hubs)
        {
            _hubs = hubs.ToList();
        }

        public int SaveChangesCount { get; private set; }

        public Task<IReadOnlyList<Hub>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Hub>>(_hubs
                .Where(hub => !activeOnly || hub.IsActive)
                .ToList());
        }

        public Task<IReadOnlyList<Hub>> GetByIdsAsync(
            IReadOnlyCollection<Guid> hubIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Hub>>(_hubs
                .Where(hub => hubIds.Contains(hub.Id))
                .ToList());
        }

        public Task<Hub?> GetByIdAsync(Guid hubId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_hubs.FirstOrDefault(hub => hub.Id == hubId));
        }

        public Task<Hub?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            var normalizedCode = code.Trim().ToUpperInvariant();
            return Task.FromResult(_hubs.FirstOrDefault(hub => hub.Code == normalizedCode));
        }

        public Task AddAsync(Hub hub, CancellationToken cancellationToken = default)
        {
            _hubs.Add(hub);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
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

        public Task<IReadOnlyList<ShipperWorkingArea>> GetByShipperIdAsync(
            Guid shipperId,
            bool activeOnly = false,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ShipperWorkingArea>>(_workingAreas
                .Where(area => area.ShipperId == shipperId)
                .Where(area => !activeOnly || area.IsActive)
                .ToList());
        }

        public Task<IReadOnlyList<ShipperWorkingArea>> GetActiveByShipperIdsAsync(
            IReadOnlyCollection<Guid> shipperIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ShipperWorkingArea>>(_workingAreas
                .Where(area => area.IsActive && shipperIds.Contains(area.ShipperId))
                .ToList());
        }

        public Task<IReadOnlyList<ShipperWorkingArea>> GetActiveByHubOrProvinceAsync(
            Guid? hubId,
            string province,
            string? ward = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ShipperWorkingArea>>(_workingAreas
                .Where(area => area.IsActive)
                .Where(area => area.Province == province || (hubId.HasValue && area.HubId == hubId.Value))
                .ToList());
        }

        public Task<int> CountActiveByHubIdAsync(Guid hubId, CancellationToken cancellationToken = default)
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
}
