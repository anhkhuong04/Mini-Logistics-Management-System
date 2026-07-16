using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class AdminAuditPersistenceTests : IClassFixture<LocalDbIntegrationFixture>
{
    private static readonly Guid DemoAdminUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly LocalDbIntegrationFixture _fixture;

    public AdminAuditPersistenceTests(LocalDbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AdminAuditService_PersistsAndQueriesAuditLog()
    {
        var targetId = Guid.NewGuid();

        await _fixture.ExecuteAsync(async services =>
        {
            var auditService = services.GetRequiredService<IAdminAuditService>();

            await auditService.RecordAsync(new AdminAuditEntry(
                DemoAdminUserId,
                AdminAuditActions.ShopActiveStatusChanged,
                AdminAuditTargetTypes.Shop,
                targetId,
                OldValue: new { IsActive = true },
                NewValue: new { IsActive = false },
                Reason: "Integration audit test."));
            await auditService.SaveChangesAsync();
        });

        await _fixture.ExecuteAsync(async services =>
        {
            var dbContext = services.GetRequiredService<MiniLogisticsDbContext>();
            var persistedLog = await dbContext.AdminAuditLogs.SingleAsync(auditLog =>
                auditLog.TargetId == targetId);

            Assert.Equal(DemoAdminUserId, persistedLog.ActorUserId);
            Assert.Equal("Admin", persistedLog.ActorRole);
            Assert.Equal(AdminAuditActions.ShopActiveStatusChanged, persistedLog.Action);
            Assert.Equal(AdminAuditTargetTypes.Shop, persistedLog.TargetType);
            Assert.DoesNotContain("ml_live_", persistedLog.NewValueJson ?? string.Empty);
        });

        var queryResult = await _fixture.ExecuteAsync(services =>
            services.GetRequiredService<IGetAdminAuditLogsService>().GetAsync(new AdminAuditLogQuery(
                DemoAdminUserId,
                Action: AdminAuditActions.ShopActiveStatusChanged,
                TargetType: AdminAuditTargetTypes.Shop)));

        Assert.True(queryResult.IsSuccess, queryResult.Error.Description);
        Assert.Contains(queryResult.Value, auditLog => auditLog.TargetId == targetId);
    }
}
