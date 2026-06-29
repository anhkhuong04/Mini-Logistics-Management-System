using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.PartnerApi;

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
}
