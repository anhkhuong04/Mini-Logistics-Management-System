using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class PartnerApiCredentialAuditRepository : IPartnerApiCredentialAuditRepository
{
    private const string RecentAuditIdsSql = """
        ;WITH ranked AS
        (
            SELECT audit.[Id],
                   ROW_NUMBER() OVER (
                       PARTITION BY audit.[ApiClientId]
                       ORDER BY audit.[CreatedAtUtc] DESC, audit.[Id] DESC) AS [RowNumber]
            FROM [PartnerApiCredentialAudits] AS audit
            INNER JOIN OPENJSON(@ApiClientIds)
                WITH ([ApiClientId] uniqueidentifier '$') AS requested
                ON requested.[ApiClientId] = audit.[ApiClientId]
        )
        SELECT [Id]
        FROM ranked
        WHERE [RowNumber] <= @TakePerClient;
        """;

    private readonly MiniLogisticsDbContext _dbContext;

    public PartnerApiCredentialAuditRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PartnerApiCredentialAudit>> GetRecentByApiClientIdsAsync(
        IReadOnlyCollection<Guid> apiClientIds,
        int takePerClient,
        CancellationToken cancellationToken = default)
    {
        if (apiClientIds.Count == 0 || takePerClient <= 0)
        {
            return [];
        }

        var distinctApiClientIds = apiClientIds.Distinct().ToArray();
        if (_dbContext.Database.IsSqlServer())
        {
            var selectedIds = await SqlServerQueryExecutor.QueryIdsAsync(
                _dbContext,
                RecentAuditIdsSql,
                [
                    new SqlQueryParameter(
                        "@ApiClientIds",
                        JsonSerializer.Serialize(distinctApiClientIds),
                        DbType.String,
                        Size: -1),
                    new SqlQueryParameter("@TakePerClient", takePerClient, DbType.Int32)
                ],
                cancellationToken);

            return await _dbContext.PartnerApiCredentialAudits
                .AsNoTracking()
                .Where(audit => selectedIds.Contains(audit.Id))
                .OrderByDescending(audit => audit.CreatedAtUtc)
                .ThenByDescending(audit => audit.Id)
                .ToListAsync(cancellationToken);
        }

        var audits = new List<PartnerApiCredentialAudit>(distinctApiClientIds.Length * takePerClient);
        foreach (var apiClientId in distinctApiClientIds)
        {
            audits.AddRange(await _dbContext.PartnerApiCredentialAudits
                .AsNoTracking()
                .Where(audit => audit.ApiClientId == apiClientId)
                .OrderByDescending(audit => audit.CreatedAtUtc)
                .ThenByDescending(audit => audit.Id)
                .Take(takePerClient)
                .ToListAsync(cancellationToken));
        }

        return audits
            .OrderByDescending(audit => audit.CreatedAtUtc)
            .ThenByDescending(audit => audit.Id)
            .ToList();
    }

    public async Task AddAsync(
        PartnerApiCredentialAudit audit,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.PartnerApiCredentialAudits.AddAsync(audit, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
