using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.PartnerApi;
using Microsoft.EntityFrameworkCore;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class PartnerApiRequestAuditRepository : IPartnerApiRequestAuditRepository
{
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
        var result = new Dictionary<Guid, PartnerApiUsageMetricsResponse>();
        var fromUtc = nowUtc.AddDays(-7);
        var hourlyFromUtc = nowUtc.AddHours(-24);

        foreach (var apiClientId in apiClientIds)
        {
            var query = _dbContext.PartnerApiRequestAudits
                .AsNoTracking()
                .Where(audit => audit.ApiClientId == apiClientId && audit.CreatedAtUtc >= fromUtc);
            var total = await query.CountAsync(cancellationToken);
            var successful = await query.CountAsync(audit => audit.IsSuccess, cancellationToken);
            var dailyRaw = await query
                .GroupBy(audit => audit.CreatedAtUtc.Date)
                .Select(group => new
                {
                    Date = group.Key,
                    Total = group.Count(),
                    Success = group.Count(audit => audit.IsSuccess)
                })
                .OrderBy(item => item.Date)
                .ToListAsync(cancellationToken);
            var hourlyRaw = await query
                .Where(audit => audit.CreatedAtUtc >= hourlyFromUtc)
                .GroupBy(audit => new
                {
                    audit.CreatedAtUtc.Year,
                    audit.CreatedAtUtc.Month,
                    audit.CreatedAtUtc.Day,
                    audit.CreatedAtUtc.Hour
                })
                .Select(group => new
                {
                    group.Key.Year,
                    group.Key.Month,
                    group.Key.Day,
                    group.Key.Hour,
                    Total = group.Count(),
                    Success = group.Count(audit => audit.IsSuccess)
                })
                .OrderBy(item => item.Year)
                .ThenBy(item => item.Month)
                .ThenBy(item => item.Day)
                .ThenBy(item => item.Hour)
                .ToListAsync(cancellationToken);
            var failures = await query
                .Where(audit => !audit.IsSuccess)
                .OrderByDescending(audit => audit.CreatedAtUtc)
                .Take(10)
                .Select(audit => new PartnerApiFailedRequestResponse(
                    audit.Method,
                    audit.Path,
                    audit.StatusCode,
                    audit.ErrorCode,
                    audit.ErrorMessage,
                    audit.CreatedAtUtc))
                .ToListAsync(cancellationToken);

            result[apiClientId] = new PartnerApiUsageMetricsResponse(
                total,
                successful,
                total - successful,
                dailyRaw.Select(item => new PartnerApiUsageBucketResponse(
                    new DateTimeOffset(item.Date, TimeSpan.Zero),
                    item.Total,
                    item.Success,
                    item.Total - item.Success)).ToList(),
                hourlyRaw.Select(item => new PartnerApiUsageBucketResponse(
                    new DateTimeOffset(item.Year, item.Month, item.Day, item.Hour, 0, 0, TimeSpan.Zero),
                    item.Total,
                    item.Success,
                    item.Total - item.Success)).ToList(),
                failures);
        }

        return result;
    }
}
