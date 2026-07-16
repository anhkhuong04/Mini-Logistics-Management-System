using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.AdminSystemConfiguration;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Routing;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Operations;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Users;
using MiniLogistics.Domain.ValueObjects;
using Xunit;

namespace MiniLogistics.Application.Tests;

public sealed class SystemConfigurationP3Tests
{
    private readonly Guid _adminId = Guid.NewGuid();

    [Fact]
    public void RouteClassification_UsesInjectedConfigSource()
    {
        var service = new RouteClassificationService(new FakeRouteRegionConfigSource(new Dictionary<string, string>
        {
            ["Province A"] = "Region One",
            ["Province B"] = "Region One",
            ["Province C"] = "Region Two"
        }));

        var intraRegion = service.Classify("Province A", "Province B");
        var interRegion = service.Classify("Province A", "Province C");

        Assert.True(intraRegion.IsSuccess, intraRegion.Error.Description);
        Assert.Equal(RouteType.IntraRegion, intraRegion.Value.RouteType);
        Assert.True(interRegion.IsSuccess, interRegion.Error.Description);
        Assert.Equal(RouteType.InterRegion, interRegion.Value.RouteType);
    }

    [Fact]
    public async Task CreateFeeRuleVersion_DeactivatesOldRulesAndWritesAudit()
    {
        var oldRule = new FeeRule(
            RouteType.InterRegion,
            0.5m,
            new Money(35_000m),
            0.5m,
            new Money(8_000m));
        var feeRepository = new FakeFeeConfigurationRepository([oldRule]);
        var auditService = new FakeAdminAuditService();
        var service = new AdminSystemConfigurationService(
            new FakeIdentityService(_adminId),
            new FakeRouteRegionConfigRepository([]),
            feeRepository,
            new UpsertRouteRegionConfigCommandValidator(),
            new CreateFeeRuleVersionCommandValidator(),
            auditService);

        var result = await service.CreateFeeRuleVersionAsync(new CreateFeeRuleVersionCommand(
            _adminId,
            RouteType.InterRegion,
            1m,
            40_000m,
            0.5m,
            9_000m,
            null,
            null,
            InsuranceFeePolicy.FreeInsuranceThreshold,
            InsuranceFeePolicy.MaximumInsuredValue,
            InsuranceFeePolicy.InsuranceRate,
            0.6m,
            "Peak season tariff."));

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.False(oldRule.IsActive);
        Assert.Equal(2, result.Value.Version);
        Assert.Equal(0.6m, result.Value.ReturnFeeRate);
        Assert.Equal(2, feeRepository.Rules.Count);
        var auditEntry = Assert.Single(auditService.Entries);
        Assert.Equal(AdminAuditActions.FeeRuleVersionCreated, auditEntry.Action);
        Assert.Equal(AdminAuditTargetTypes.FeeRule, auditEntry.TargetType);
        Assert.Equal("Peak season tariff.", auditEntry.Reason);
    }

    private sealed class FakeRouteRegionConfigSource : IRouteRegionConfigSource
    {
        private readonly IReadOnlyDictionary<string, string> _provinceRegions;

        public FakeRouteRegionConfigSource(IReadOnlyDictionary<string, string> provinceRegions)
        {
            _provinceRegions = provinceRegions;
        }

        public IReadOnlyDictionary<string, string> GetProvinceRegions()
        {
            return _provinceRegions;
        }
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        private readonly Guid _adminUserId;

        public FakeIdentityService(Guid adminUserId)
        {
            _adminUserId = adminUserId;
        }

        public Task<Result<Guid>> CreateUserAsync(string fullName, string email, string phoneNumber, string password, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result> AddToRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<Guid>> CreateInternalUserAsync(string fullName, string email, string phoneNumber, string password, string role, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result> SetUserActiveStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result> SetShipperCapacityAsync(Guid userId, bool isAvailableForAssignment, int maxActiveShipments, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IdentityUserRoleCheckResponse> CheckUserRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new IdentityUserRoleCheckResponse(
                userId,
                userId == _adminUserId,
                userId == _adminUserId,
                userId == _adminUserId && role == nameof(UserRole.Admin)));
        }

        public Task<IReadOnlyList<IdentityUserWithRolesResponse>> ListUsersWithRolesAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ActiveShipperResponse>> GetActiveShippersAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<IdentityUserSummaryResponse>> GetUsersByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeRouteRegionConfigRepository : IRouteRegionConfigRepository
    {
        private readonly List<RouteRegionConfig> _configs;

        public FakeRouteRegionConfigRepository(IReadOnlyList<RouteRegionConfig> configs)
        {
            _configs = configs.ToList();
        }

        public IReadOnlyDictionary<string, string> GetProvinceRegions()
        {
            return _configs
                .Where(config => config.IsActive)
                .ToDictionary(config => config.Province, config => config.Region);
        }

        public Task<IReadOnlyList<RouteRegionConfig>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RouteRegionConfig>>(_configs
                .Where(config => !activeOnly || config.IsActive)
                .ToList());
        }

        public Task<IReadOnlyList<RouteRegionConfig>> GetActiveByProvinceAsync(string province, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RouteRegionConfig>>(_configs
                .Where(config => config.IsActive && config.Province == province)
                .ToList());
        }

        public Task<int> GetLatestVersionAsync(string province, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_configs
                .Where(config => config.Province == province)
                .Select(config => config.Version)
                .DefaultIfEmpty()
                .Max());
        }

        public Task AddAsync(RouteRegionConfig config, CancellationToken cancellationToken = default)
        {
            _configs.Add(config);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFeeConfigurationRepository : IFeeConfigurationRepository
    {
        public List<FeeRule> Rules { get; }

        public FakeFeeConfigurationRepository(IReadOnlyList<FeeRule> rules)
        {
            Rules = rules.ToList();
        }

        public Task<IReadOnlyList<FeeRule>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<FeeRule>>(Rules);
        }

        public Task<IReadOnlyList<FeeRule>> GetActiveRulesForUpdateAsync(RouteType routeType, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<FeeRule>>(Rules
                .Where(rule => rule.RouteType == routeType && rule.IsActive)
                .ToList());
        }

        public Task<int> GetLatestVersionAsync(RouteType routeType, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Rules
                .Where(rule => rule.RouteType == routeType)
                .Select(rule => rule.Version)
                .DefaultIfEmpty()
                .Max());
        }

        public Task AddAsync(FeeRule feeRule, CancellationToken cancellationToken = default)
        {
            Rules.Add(feeRule);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
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
}
