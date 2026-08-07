using Microsoft.EntityFrameworkCore;
using System.Data;
using MiniLogistics.Application.Outbox;
using MiniLogistics.Domain.Outbox;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class OutboxMessageRepository : IOutboxMessageRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public OutboxMessageRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<OutboxMessage?> GetByIdAsync(
        Guid outboxMessageId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.OutboxMessages.FirstOrDefaultAsync(
            message => message.Id == outboxMessageId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetRecentFailuresAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.OutboxMessages
            .AsNoTracking()
            .Where(message => message.Status == OutboxMessageStatus.Failed
                || message.Status == OutboxMessageStatus.DeadLettered)
            .OrderByDescending(message => message.UpdatedAtUtc ?? message.CreatedAtUtc)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetDueAsync(
        DateTimeOffset dueAtUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.OutboxMessages
            .Where(message =>
                (message.Status == OutboxMessageStatus.Pending
                    || message.Status == OutboxMessageStatus.Failed
                    || message.Status == OutboxMessageStatus.Processing)
                && message.NextAttemptAtUtc != null
                && message.NextAttemptAtUtc <= dueAtUtc
                && (message.LockedUntilUtc == null || message.LockedUntilUtc <= dueAtUtc))
            .OrderBy(message => message.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(
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
                .Where(message => message.TryAcquireLease(workerId, Guid.NewGuid(), nowUtc, lockedUntilUtc))
                .ToList();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return claimed;
        }

        const string sql = """
            SET NOCOUNT ON;
            ;WITH candidates AS
            (
                SELECT TOP (@BatchSize) *
                FROM [OutboxMessages] WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE [Status] IN (N'Pending', N'Failed', N'Processing')
                  AND [NextAttemptAtUtc] IS NOT NULL
                  AND [NextAttemptAtUtc] <= @NowUtc
                  AND ([LockedUntilUtc] IS NULL OR [LockedUntilUtc] <= @NowUtc)
                ORDER BY [CreatedAtUtc]
            )
            UPDATE candidates
            SET [Status] = N'Processing',
                [LockedBy] = @WorkerId,
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
        return await _dbContext.OutboxMessages
            .Where(message => claimedIds.Contains(message.Id))
            .OrderBy(message => message.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.OutboxMessages.AddAsync(message, cancellationToken);
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
