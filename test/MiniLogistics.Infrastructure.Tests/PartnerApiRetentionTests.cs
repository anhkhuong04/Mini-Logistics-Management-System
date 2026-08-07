using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Domain.Outbox;
using MiniLogistics.Infrastructure.PartnerApi;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class PartnerApiRetentionTests : IClassFixture<LocalDbIntegrationFixture>
{
    private readonly LocalDbIntegrationFixture _fixture;

    public PartnerApiRetentionTests(LocalDbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DeleteExpiredAsync_RemovesOnlySucceededRowsBeyondRetention()
    {
        var now = DateTimeOffset.UtcNow;
        var expired = CreateSucceededOutbox(now.AddDays(-31));
        var retained = CreateSucceededOutbox(now.AddDays(-29));
        var deadLettered = new OutboxMessage(
            Guid.NewGuid(),
            "test.retention.dead-letter",
            Guid.NewGuid(),
            "{}",
            now.AddDays(-90));
        deadLettered.MarkFailed("still requires operator review", null, now.AddDays(-90));

        await _fixture.ExecuteAsync(async services =>
        {
            var dbContext = services.GetRequiredService<MiniLogisticsDbContext>();
            dbContext.OutboxMessages.AddRange(expired, retained, deadLettered);
            await dbContext.SaveChangesAsync();

            var retention = services.GetRequiredService<PartnerApiRetentionService>();
            await retention.DeleteExpiredAsync(now);
        });

        await _fixture.ExecuteAsync(async services =>
        {
            var dbContext = services.GetRequiredService<MiniLogisticsDbContext>();
            Assert.False(await dbContext.OutboxMessages.AnyAsync(message => message.Id == expired.Id));
            Assert.True(await dbContext.OutboxMessages.AnyAsync(message => message.Id == retained.Id));
            Assert.True(await dbContext.OutboxMessages.AnyAsync(message => message.Id == deadLettered.Id));
        });
    }

    private static OutboxMessage CreateSucceededOutbox(DateTimeOffset processedAtUtc)
    {
        var message = new OutboxMessage(
            Guid.NewGuid(),
            "test.retention.succeeded",
            Guid.NewGuid(),
            "{}",
            processedAtUtc);
        message.MarkSucceeded(processedAtUtc);
        return message;
    }
}
