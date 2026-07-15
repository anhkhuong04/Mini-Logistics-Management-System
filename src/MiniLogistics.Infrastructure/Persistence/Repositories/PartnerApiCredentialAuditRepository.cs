using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class PartnerApiCredentialAuditRepository : IPartnerApiCredentialAuditRepository
{
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

        var audits = await _dbContext.PartnerApiCredentialAudits
            .AsNoTracking()
            .Where(audit => audit.ApiClientId.HasValue && apiClientIds.Contains(audit.ApiClientId.Value))
            .OrderByDescending(audit => audit.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return audits
            .GroupBy(audit => audit.ApiClientId)
            .SelectMany(group => group.Take(takePerClient))
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
