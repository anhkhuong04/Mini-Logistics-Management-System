using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Domain.AdminAuditing;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class AdminAuditLogRepository : IAdminAuditLogRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public AdminAuditLogRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AdminAuditLog auditLog, CancellationToken cancellationToken = default)
    {
        await _dbContext.AdminAuditLogs.AddAsync(auditLog, cancellationToken);
    }

    public async Task<IReadOnlyList<AdminAuditLog>> QueryAsync(
        AdminAuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var auditLogs = _dbContext.AdminAuditLogs.AsNoTracking().AsQueryable();

        if (query.ActorUserId.HasValue)
        {
            auditLogs = auditLogs.Where(auditLog => auditLog.ActorUserId == query.ActorUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            auditLogs = auditLogs.Where(auditLog => auditLog.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(query.TargetType))
        {
            var targetType = query.TargetType.Trim();
            auditLogs = auditLogs.Where(auditLog => auditLog.TargetType == targetType);
        }

        if (query.FromUtc.HasValue)
        {
            auditLogs = auditLogs.Where(auditLog => auditLog.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            auditLogs = auditLogs.Where(auditLog => auditLog.CreatedAtUtc <= query.ToUtc.Value);
        }

        return await auditLogs
            .OrderByDescending(auditLog => auditLog.CreatedAtUtc)
            .ThenByDescending(auditLog => auditLog.Id)
            .Take(query.Limit)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
