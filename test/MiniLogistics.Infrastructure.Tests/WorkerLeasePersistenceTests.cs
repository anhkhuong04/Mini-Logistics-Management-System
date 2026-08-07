using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Application.Outbox;
using MiniLogistics.Domain.Outbox;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class WorkerLeasePersistenceTests : IClassFixture<LocalDbIntegrationFixture>
{
    private readonly LocalDbIntegrationFixture _fixture;

    public WorkerLeasePersistenceTests(LocalDbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ClaimDueAsync_TwoDatabaseWorkersClaimMessageOnlyOnce()
    {
        var now = DateTimeOffset.UtcNow;
        var message = new OutboxMessage(
            Guid.NewGuid(),
            "test.worker-lease",
            Guid.NewGuid(),
            "{}",
            now);
        await _fixture.ExecuteAsync(async services =>
        {
            var repository = services.GetRequiredService<IOutboxMessageRepository>();
            await repository.AddAsync(message);
            await repository.SaveChangesAsync();
        });

        await using var firstScope = _fixture.ServiceProvider.CreateAsyncScope();
        await using var secondScope = _fixture.ServiceProvider.CreateAsyncScope();
        var firstRepository = firstScope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var secondRepository = secondScope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();

        var claims = await Task.WhenAll(
            firstRepository.ClaimDueAsync("worker-a", now, now.AddMinutes(5), 10),
            secondRepository.ClaimDueAsync("worker-b", now, now.AddMinutes(5), 10));

        Assert.Equal(1, claims.Sum(claim => claim.Count));
        Assert.NotNull(claims.SelectMany(claim => claim).Single().AttemptId);
    }

    [Fact]
    public async Task ClaimDueAsync_ExpiredLeaseCanBeReclaimedAfterWorkerCrash()
    {
        var now = DateTimeOffset.UtcNow;
        var message = new OutboxMessage(
            Guid.NewGuid(),
            "test.lease-reclaim",
            Guid.NewGuid(),
            "{}",
            now);
        await _fixture.ExecuteAsync(async services =>
        {
            var repository = services.GetRequiredService<IOutboxMessageRepository>();
            await repository.AddAsync(message);
            await repository.SaveChangesAsync();
            var firstClaim = await repository.ClaimDueAsync(
                "crashed-worker",
                now,
                now.AddSeconds(1),
                1);
            Assert.Single(firstClaim);
        });

        var reclaimed = await _fixture.ExecuteAsync(async services =>
        {
            var repository = services.GetRequiredService<IOutboxMessageRepository>();
            return await repository.ClaimDueAsync(
                "replacement-worker",
                now.AddMinutes(1),
                now.AddMinutes(6),
                1);
        });

        var claimed = Assert.Single(reclaimed);
        Assert.Equal("replacement-worker", claimed.LockedBy);
    }
}
