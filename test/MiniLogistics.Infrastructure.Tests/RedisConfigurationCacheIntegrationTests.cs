using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Application;
using MiniLogistics.Application.AdminHubs.CreateHub;
using MiniLogistics.Application.AdminHubs.SetHubActiveStatus;
using MiniLogistics.Application.AdminSystemConfiguration;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Users;
using MiniLogistics.Infrastructure;
using MiniLogistics.Infrastructure.Persistence;
using StackExchange.Redis;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class RedisConfigurationCacheIntegrationTests : IClassFixture<LocalDbIntegrationFixture>
{
    private readonly LocalDbIntegrationFixture _fixture;

    public RedisConfigurationCacheIntegrationTests(LocalDbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [RedisFact]
    public async Task SeparateNodes_ObserveCommittedRouteFeeAndHubChangesThroughRedis()
    {
        var redisConnectionString = Environment.GetEnvironmentVariable("MINILOGISTICS_TEST_REDIS")!;
        var keyPrefix = $"mini-logistics:ci09:{Guid.NewGuid():N}";
        await using var nodeA = CreateNodeProvider(redisConnectionString, keyPrefix);
        await using var nodeB = CreateNodeProvider(redisConnectionString, keyPrefix);
        var adminId = await GetAdminIdAsync();
        var adminConfigService = await GetScopedServiceAsync<IAdminSystemConfigurationService>(nodeA);
        var province = $"Redis-{Guid.NewGuid():N}";
        var hubCode = $"R{Guid.NewGuid():N}".ToUpperInvariant();

        await using (var scope = nodeB.CreateAsyncScope())
        {
            _ = scope.ServiceProvider.GetRequiredService<IRouteRegionConfigSource>().GetProvinceRegions();
            _ = await scope.ServiceProvider.GetRequiredService<IFeeRuleRepository>()
                .GetActiveRulesAsync(RouteType.IntraProvince);
            _ = await scope.ServiceProvider.GetRequiredService<IHubRepository>().GetAllAsync(activeOnly: true);
        }

        var routeResult = await adminConfigService.UpsertRouteRegionAsync(
            new UpsertRouteRegionConfigCommand(adminId, province, "Redis Region"));
        Assert.True(routeResult.IsSuccess, routeResult.Error.Description);

        await using (var scope = nodeB.CreateAsyncScope())
        {
            var routeSnapshot = scope.ServiceProvider.GetRequiredService<IRouteRegionConfigSource>()
                .GetProvinceRegionSnapshot();
            var route = Assert.Single(routeSnapshot, entry => entry.Province == province);
            Assert.Equal(routeResult.Value.ConfigId, route.ConfigurationId);
            Assert.Equal(routeResult.Value.Version, route.Version);
        }

        var feeResult = await adminConfigService.CreateFeeRuleVersionAsync(CreateFeeCommand(adminId));
        Assert.True(feeResult.IsSuccess, feeResult.Error.Description);

        await using (var scope = nodeB.CreateAsyncScope())
        {
            var activeFee = Assert.Single(await scope.ServiceProvider.GetRequiredService<IFeeRuleRepository>()
                .GetActiveRulesAsync(RouteType.IntraProvince));
            Assert.Equal(feeResult.Value.FeeRuleId, activeFee.Id);
        }

        var hubService = await GetScopedServiceAsync<ICreateHubService>(nodeA);
        var hubResult = await hubService.CreateAsync(new CreateHubCommand(
            adminId,
            hubCode,
            "Redis Test Hub",
            province,
            null,
            null,
            "Vietnam",
            false));
        Assert.True(hubResult.IsSuccess, hubResult.Error.Description);

        await using (var scope = nodeB.CreateAsyncScope())
        {
            var activeHubs = await scope.ServiceProvider.GetRequiredService<IHubRepository>()
                .GetAllAsync(activeOnly: true);
            Assert.Contains(activeHubs, hub => hub.Code == hubCode);
            var versionStore = scope.ServiceProvider.GetRequiredService<IConfigurationCacheVersionStore>();
            var hubVersionBeforeRollback = versionStore.GetVersion(ConfigurationCacheScope.Hubs);

            await using (var transaction = await scope.ServiceProvider
                .GetRequiredService<IApplicationDbTransactionManager>()
                .BeginTransactionAsync())
            {
                var hubRepository = scope.ServiceProvider.GetRequiredService<IHubRepository>();
                var trackedHub = await hubRepository.GetByCodeAsync(hubCode);
                Assert.NotNull(trackedHub);
                trackedHub.Deactivate(DateTimeOffset.UtcNow);
                await hubRepository.SaveChangesAsync();
            }

            Assert.Equal(hubVersionBeforeRollback, versionStore.GetVersion(ConfigurationCacheScope.Hubs));
            var afterRollback = await scope.ServiceProvider.GetRequiredService<IHubRepository>()
                .GetAllAsync(activeOnly: true);
            Assert.Contains(afterRollback, hub => hub.Code == hubCode);
        }

        var statusService = await GetScopedServiceAsync<ISetHubActiveStatusService>(nodeA);
        var inactiveResult = await statusService.SetAsync(new SetHubActiveStatusCommand(
            adminId,
            hubResult.Value.HubId,
            false,
            "Redis cache integration check."));
        Assert.True(inactiveResult.IsSuccess, inactiveResult.Error.Description);

        await using (var scope = nodeB.CreateAsyncScope())
        {
            var activeHubs = await scope.ServiceProvider.GetRequiredService<IHubRepository>()
                .GetAllAsync(activeOnly: true);
            Assert.DoesNotContain(activeHubs, hub => hub.Code == hubCode);
        }
    }

    private ServiceProvider CreateNodeProvider(string redisConnectionString, string keyPrefix)
    {
        var configuration = new ConfigurationManager();
        configuration["ConnectionStrings:DefaultConnection"] = _fixture.ConnectionString;
        configuration["ConnectionStrings:Redis"] = redisConnectionString;
        configuration["PartnerApi:RateLimiting:Mode"] = "Redis";
        configuration["ConfigurationCache:KeyPrefix"] = keyPrefix;
        configuration["ConfigurationCache:ConsistencyWindowSeconds"] = "15";
        configuration["Seeding:Enabled"] = "false";

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = false
        });
    }

    private static async Task<TService> GetScopedServiceAsync<TService>(ServiceProvider provider)
        where TService : notnull
    {
        await using var scope = provider.CreateAsyncScope();
        return scope.ServiceProvider.GetRequiredService<TService>();
    }

    private async Task<Guid> GetAdminIdAsync()
    {
        return await _fixture.ExecuteAsync(async provider =>
        {
            var dbContext = provider.GetRequiredService<MiniLogisticsDbContext>();
            var adminRoleId = await dbContext.Roles
                .Where(role => role.Name == nameof(UserRole.Admin))
                .Select(role => role.Id)
                .SingleAsync();
            return await dbContext.UserRoles
                .Where(userRole => userRole.RoleId == adminRoleId)
                .Select(userRole => userRole.UserId)
                .FirstAsync();
        });
    }

    private static CreateFeeRuleVersionCommand CreateFeeCommand(Guid adminId) => new(
        adminId,
        RouteType.IntraProvince,
        2m,
        47_000m,
        0.5m,
        3_500m,
        null,
        null,
        300_000m,
        30_000_000m,
        0.005m,
        0.5m,
        "Redis cache integration check.");
}

public sealed class RedisFactAttribute : FactAttribute
{
    public RedisFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MINILOGISTICS_TEST_REDIS")))
        {
            Skip = "Set MINILOGISTICS_TEST_REDIS to a reachable Redis connection to run the multi-node cache test.";
        }
    }
}
