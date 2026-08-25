using System.Data;
using System.Text.Json;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.PartnerApi;
using Microsoft.EntityFrameworkCore;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class PartnerApiRequestAuditRepository : IPartnerApiRequestAuditRepository
{
    private const int RecentFailureCountPerClient = 10;
    private const string RecentFailureIdsSql = """
        ;WITH ranked AS
        (
            SELECT audit.[Id],
                   ROW_NUMBER() OVER (
                       PARTITION BY audit.[ApiClientId]
                       ORDER BY audit.[CreatedAtUtc] DESC, audit.[Id] DESC) AS [RowNumber]
            FROM [PartnerApiRequestAudits] AS audit
            INNER JOIN OPENJSON(@ApiClientIds)
                WITH ([ApiClientId] uniqueidentifier '$') AS requested
                ON requested.[ApiClientId] = audit.[ApiClientId]
            WHERE audit.[CreatedAtUtc] >= @FromUtc
              AND audit.[IsSuccess] = CAST(0 AS bit)
        )
        SELECT [Id]
        FROM ranked
        WHERE [RowNumber] <= @TakePerClient;
        """;

    private readonly MiniLogisticsDbContext _dbContext;

    public PartnerApiRequestAuditRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        PartnerApiRequestAudit audit,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.PartnerApiRequestAudits.AddAsync(audit, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, PartnerApiUsageMetricsResponse>> GetUsageByApiClientIdsAsync(
        IReadOnlyCollection<Guid> apiClientIds,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var distinctApiClientIds = apiClientIds.Distinct().ToArray();
        if (distinctApiClientIds.Length == 0)
        {
            return new Dictionary<Guid, PartnerApiUsageMetricsResponse>();
        }

        var fromUtc = nowUtc.AddDays(-7);
        var hourlyFromUtc = nowUtc.AddHours(-24);
        var query = _dbContext.PartnerApiRequestAudits
            .AsNoTracking()
            .Where(audit => distinctApiClientIds.Contains(audit.ApiClientId) && audit.CreatedAtUtc >= fromUtc);

        // Keep the number of database round-trips constant as the number of API
        // clients grows. The previous implementation executed five queries per client.
        var summaryRaw = await query
            .GroupBy(audit => audit.ApiClientId)
            .Select(group => new
            {
                ApiClientId = group.Key,
                Total = group.Count(),
                Success = group.Count(audit => audit.IsSuccess)
            })
            .ToListAsync(cancellationToken);
        var dailyRaw = await query
            .GroupBy(audit => new { audit.ApiClientId, Date = audit.CreatedAtUtc.Date })
            .Select(group => new
            {
                group.Key.ApiClientId,
                group.Key.Date,
                Total = group.Count(),
                Success = group.Count(audit => audit.IsSuccess)
            })
            .OrderBy(item => item.ApiClientId)
            .ThenBy(item => item.Date)
            .ToListAsync(cancellationToken);
        var hourlyRaw = await query
            .Where(audit => audit.CreatedAtUtc >= hourlyFromUtc)
            .GroupBy(audit => new
            {
                audit.ApiClientId,
                audit.CreatedAtUtc.Year,
                audit.CreatedAtUtc.Month,
                audit.CreatedAtUtc.Day,
                audit.CreatedAtUtc.Hour
            })
            .Select(group => new
            {
                group.Key.ApiClientId,
                group.Key.Year,
                group.Key.Month,
                group.Key.Day,
                group.Key.Hour,
                Total = group.Count(),
                Success = group.Count(audit => audit.IsSuccess)
            })
            .OrderBy(item => item.ApiClientId)
            .ThenBy(item => item.Year)
            .ThenBy(item => item.Month)
            .ThenBy(item => item.Day)
            .ThenBy(item => item.Hour)
            .ToListAsync(cancellationToken);
        var failureRows = await GetRecentFailuresAsync(
            distinctApiClientIds,
            fromUtc,
            cancellationToken);

        var summaries = summaryRaw.ToDictionary(item => item.ApiClientId);
        var dailyByClient = dailyRaw.ToLookup(item => item.ApiClientId);
        var hourlyByClient = hourlyRaw.ToLookup(item => item.ApiClientId);
        var failuresByClient = failureRows.ToLookup(item => item.ApiClientId);
        var result = new Dictionary<Guid, PartnerApiUsageMetricsResponse>(distinctApiClientIds.Length);

        foreach (var apiClientId in distinctApiClientIds)
        {
            summaries.TryGetValue(apiClientId, out var summary);
            var total = summary?.Total ?? 0;
            var successful = summary?.Success ?? 0;

            result[apiClientId] = new PartnerApiUsageMetricsResponse(
                total,
                successful,
                total - successful,
                dailyByClient[apiClientId].Select(item => new PartnerApiUsageBucketResponse(
                    new DateTimeOffset(item.Date, TimeSpan.Zero),
                    item.Total,
                    item.Success,
                    item.Total - item.Success)).ToList(),
                hourlyByClient[apiClientId].Select(item => new PartnerApiUsageBucketResponse(
                    new DateTimeOffset(item.Year, item.Month, item.Day, item.Hour, 0, 0, TimeSpan.Zero),
                    item.Total,
                    item.Success,
                    item.Total - item.Success)).ToList(),
                failuresByClient[apiClientId]
                    .OrderByDescending(item => item.CreatedAtUtc)
                    .ThenByDescending(item => item.Id)
                    .Select(item => new PartnerApiFailedRequestResponse(
                        item.Method,
                        item.Path,
                        item.StatusCode,
                        item.ErrorCode,
                        item.ErrorMessage,
                        item.CreatedAtUtc))
                    .ToList());
        }

        return result;
    }

    private async Task<IReadOnlyList<FailureRow>> GetRecentFailuresAsync(
        IReadOnlyCollection<Guid> apiClientIds,
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken)
    {
        if (_dbContext.Database.IsSqlServer())
        {
            var selectedIds = await SqlServerQueryExecutor.QueryIdsAsync(
                _dbContext,
                RecentFailureIdsSql,
                [
                    new SqlQueryParameter(
                        "@ApiClientIds",
                        JsonSerializer.Serialize(apiClientIds),
                        DbType.String,
                        Size: -1),
                    new SqlQueryParameter("@FromUtc", fromUtc, DbType.DateTimeOffset),
                    new SqlQueryParameter("@TakePerClient", RecentFailureCountPerClient, DbType.Int32)
                ],
                cancellationToken);

            return await ProjectFailures(selectedIds).ToListAsync(cancellationToken);
        }

        var failures = new List<FailureRow>(apiClientIds.Count * RecentFailureCountPerClient);
        foreach (var apiClientId in apiClientIds)
        {
            failures.AddRange(await ProjectFailuresForClient(apiClientId, fromUtc)
                .Take(RecentFailureCountPerClient)
                .ToListAsync(cancellationToken));
        }

        return failures;
    }

    private IQueryable<FailureRow> ProjectFailures(IReadOnlyCollection<Guid> auditIds)
    {
        return _dbContext.PartnerApiRequestAudits
            .AsNoTracking()
            .Where(audit => auditIds.Contains(audit.Id))
            .Select(audit => new FailureRow(
                audit.Id,
                audit.ApiClientId,
                audit.Method,
                audit.Path,
                audit.StatusCode,
                audit.ErrorCode,
                audit.ErrorMessage,
                audit.CreatedAtUtc));
    }

    private IQueryable<FailureRow> ProjectFailuresForClient(Guid apiClientId, DateTimeOffset fromUtc)
    {
        return _dbContext.PartnerApiRequestAudits
            .AsNoTracking()
            .Where(audit =>
                audit.ApiClientId == apiClientId
                && audit.CreatedAtUtc >= fromUtc
                && !audit.IsSuccess)
            .OrderByDescending(audit => audit.CreatedAtUtc)
            .ThenByDescending(audit => audit.Id)
            .Select(audit => new FailureRow(
                audit.Id,
                audit.ApiClientId,
                audit.Method,
                audit.Path,
                audit.StatusCode,
                audit.ErrorCode,
                audit.ErrorMessage,
                audit.CreatedAtUtc));
    }

    private sealed record FailureRow(
        Guid Id,
        Guid ApiClientId,
        string Method,
        string Path,
        int StatusCode,
        string? ErrorCode,
        string? ErrorMessage,
        DateTimeOffset CreatedAtUtc);
}
