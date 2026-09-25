using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Application.AdminSystemConfiguration;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Routing;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Users;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class SystemConfigurationConcurrencyTests : IClassFixture<LocalDbIntegrationFixture>
{
    private readonly LocalDbIntegrationFixture _fixture;

    public SystemConfigurationConcurrencyTests(LocalDbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConcurrentRouteAndFeeUpdates_AreSerializedAndInvalidateCommittedCaches()
    {
        var adminId = await GetAdminIdAsync();
        var adminService = await _fixture.ExecuteAsync(provider =>
            Task.FromResult(provider.GetRequiredService<IAdminSystemConfigurationService>()));
        var province = $"Concurrency-{Guid.NewGuid():N}";

        var initialRouteResult = await adminService.UpsertRouteRegionAsync(
            new UpsertRouteRegionConfigCommand(adminId, province, "Initial Region"));
        Assert.True(initialRouteResult.IsSuccess, initialRouteResult.Error.Description);

        var cachedRouteBeforeUpdate = await _fixture.ExecuteAsync(provider =>
            Task.FromResult(provider.GetRequiredService<IRouteRegionConfigSource>().GetProvinceRegions()[province]));
        Assert.Equal("Initial Region", cachedRouteBeforeUpdate);

        var routeResults = await RunTogetherAsync(
            () => adminService.UpsertRouteRegionAsync(
                new UpsertRouteRegionConfigCommand(adminId, province, "Concurrent Region A")),
            () => adminService.UpsertRouteRegionAsync(
                new UpsertRouteRegionConfigCommand(adminId, province, "Concurrent Region B")));

        Assert.True(routeResults.Item1.IsSuccess, routeResults.Item1.Error.Description);
        Assert.True(routeResults.Item2.IsSuccess, routeResults.Item2.Error.Description);

        var routeVersions = await _fixture.ExecuteAsync(async provider => await provider
            .GetRequiredService<MiniLogisticsDbContext>()
            .RouteRegionConfigs
            .AsNoTracking()
            .Where(config => config.Province == province)
            .OrderBy(config => config.Version)
            .ToListAsync());
        Assert.Equal(new[] { 1, 2, 3 }, routeVersions.Select(config => config.Version));
        var activeRoute = Assert.Single(routeVersions, config => config.IsActive);

        var cachedRouteAfterUpdate = await _fixture.ExecuteAsync(provider =>
            Task.FromResult(provider.GetRequiredService<IRouteRegionConfigSource>().GetProvinceRegions()[province]));
        Assert.Equal(activeRoute.Region, cachedRouteAfterUpdate);

        var cachedFeeBeforeUpdate = await _fixture.ExecuteAsync(provider =>
            provider.GetRequiredService<IFeeRuleRepository>().GetActiveRulesAsync(RouteType.IntraProvince));
        var originalFeeVersion = Assert.Single(cachedFeeBeforeUpdate).Version;

        var feeResults = await RunTogetherAsync(
            () => adminService.CreateFeeRuleVersionAsync(CreateFeeCommand(adminId, 42_001m)),
            () => adminService.CreateFeeRuleVersionAsync(CreateFeeCommand(adminId, 42_002m)));

        Assert.True(feeResults.Item1.IsSuccess, feeResults.Item1.Error.Description);
        Assert.True(feeResults.Item2.IsSuccess, feeResults.Item2.Error.Description);

        var feeVersions = await _fixture.ExecuteAsync(async provider => await provider
            .GetRequiredService<MiniLogisticsDbContext>()
            .FeeRules
            .AsNoTracking()
            .Where(rule => rule.RouteType == RouteType.IntraProvince)
            .OrderBy(rule => rule.Version)
            .ToListAsync());
        Assert.Equal(new[] { originalFeeVersion, originalFeeVersion + 1, originalFeeVersion + 2 },
            feeVersions.Select(rule => rule.Version));
        var activeFee = Assert.Single(feeVersions, rule => rule.IsActive);

        var cachedFeeAfterUpdate = await _fixture.ExecuteAsync(provider =>
            provider.GetRequiredService<IFeeRuleRepository>().GetActiveRulesAsync(RouteType.IntraProvince));
        Assert.Equal(activeFee.Id, Assert.Single(cachedFeeAfterUpdate).Id);
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

    private static CreateFeeRuleVersionCommand CreateFeeCommand(Guid adminId, decimal baseFeeAmount) => new(
        adminId,
        RouteType.IntraProvince,
        2m,
        baseFeeAmount,
        0.5m,
        3_000m,
        null,
        null,
        300_000m,
        30_000_000m,
        0.005m,
        0.5m,
        "Concurrent configuration integration test.");

    private static async Task<(TFirst, TSecond)> RunTogetherAsync<TFirst, TSecond>(
        Func<Task<TFirst>> firstAction,
        Func<Task<TSecond>> secondAction)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Task.Run(async () =>
        {
            await start.Task;
            return await firstAction();
        });
        var second = Task.Run(async () =>
        {
            await start.Task;
            return await secondAction();
        });

        start.SetResult();
        await Task.WhenAll(first, second);
        return (await first, await second);
    }
}
