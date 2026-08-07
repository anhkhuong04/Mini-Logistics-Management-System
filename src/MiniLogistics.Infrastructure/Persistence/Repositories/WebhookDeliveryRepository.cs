using Microsoft.EntityFrameworkCore;
using System.Data;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class WebhookDeliveryRepository : IWebhookDeliveryRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public WebhookDeliveryRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.WebhookDeliveries
            .AnyAsync(delivery => delivery.Id == deliveryId, cancellationToken);
    }

    public async Task<IReadOnlyList<WebhookDelivery>> GetDueAsync(
        DateTimeOffset dueAtUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.WebhookDeliveries
            .Where(delivery =>
                (delivery.Status == WebhookDeliveryStatus.Pending
                    || delivery.Status == WebhookDeliveryStatus.Failed)
                && delivery.NextAttemptAtUtc != null
                && delivery.NextAttemptAtUtc <= dueAtUtc
                && (delivery.LockedUntilUtc == null || delivery.LockedUntilUtc <= dueAtUtc))
            .OrderBy(delivery => delivery.NextAttemptAtUtc)
            .ThenBy(delivery => delivery.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WebhookDelivery>> ClaimDueAsync(
        string workerId,
        DateTimeOffset nowUtc,
        DateTimeOffset lockedUntilUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (!_dbContext.Database.IsSqlServer())
        {
            var due = await GetDueAsync(nowUtc, batchSize, cancellationToken);
            var claimed = due
                .Where(delivery => delivery.TryAcquireLease(workerId, Guid.NewGuid(), nowUtc, lockedUntilUtc))
                .ToList();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return claimed;
        }

        const string sql = """
            SET NOCOUNT ON;
            ;WITH candidates AS
            (
                SELECT TOP (@BatchSize) *
                FROM [WebhookDeliveries] WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE [Status] IN (N'Pending', N'Failed')
                  AND [NextAttemptAtUtc] IS NOT NULL
                  AND [NextAttemptAtUtc] <= @NowUtc
                  AND ([LockedUntilUtc] IS NULL OR [LockedUntilUtc] <= @NowUtc)
                ORDER BY [NextAttemptAtUtc], [CreatedAtUtc]
            )
            UPDATE candidates
            SET [LockedBy] = @WorkerId,
                [LockedUntilUtc] = @LockedUntilUtc,
                [AttemptId] = NEWID(),
                [UpdatedAtUtc] = @NowUtc
            OUTPUT INSERTED.[Id];
            """;
        var claimedIds = await ExecuteClaimAsync(
            sql,
            workerId,
            nowUtc,
            lockedUntilUtc,
            batchSize,
            cancellationToken);
        return await _dbContext.WebhookDeliveries
            .Where(delivery => claimedIds.Contains(delivery.Id))
            .OrderBy(delivery => delivery.NextAttemptAtUtc)
            .ThenBy(delivery => delivery.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WebhookDelivery>> GetRecentByApiClientIdsAsync(
        IReadOnlyCollection<Guid> apiClientIds,
        int takePerClient,
        CancellationToken cancellationToken = default)
    {
        if (apiClientIds.Count == 0 || takePerClient <= 0)
        {
            return [];
        }

        return await _dbContext.WebhookDeliveries
            .AsNoTracking()
            .Where(delivery => apiClientIds.Contains(delivery.ApiClientId))
            .GroupBy(delivery => delivery.ApiClientId)
            .SelectMany(group => group
                .OrderByDescending(delivery => delivery.CreatedAtUtc)
                .Take(takePerClient))
            .OrderByDescending(delivery => delivery.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<WebhookDelivery?> GetByIdAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.WebhookDeliveries
            .FirstOrDefaultAsync(delivery => delivery.Id == deliveryId, cancellationToken);
    }

    public async Task AddAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default)
    {
        await _dbContext.WebhookDeliveries.AddAsync(delivery, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> ExecuteClaimAsync(
        string sql,
        string workerId,
        DateTimeOffset nowUtc,
        DateTimeOffset lockedUntilUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            AddParameter(command, "@WorkerId", workerId, DbType.String);
            AddParameter(command, "@NowUtc", nowUtc, DbType.DateTimeOffset);
            AddParameter(command, "@LockedUntilUtc", lockedUntilUtc, DbType.DateTimeOffset);
            AddParameter(command, "@BatchSize", batchSize, DbType.Int32);
            var ids = new List<Guid>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetGuid(0));
            }

            return ids;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        object value,
        DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        parameter.DbType = dbType;
        command.Parameters.Add(parameter);
    }
}
