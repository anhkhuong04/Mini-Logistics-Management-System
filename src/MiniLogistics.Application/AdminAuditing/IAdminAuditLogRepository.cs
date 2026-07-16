using MiniLogistics.Domain.AdminAuditing;

namespace MiniLogistics.Application.AdminAuditing;

public interface IAdminAuditLogRepository
{
    Task AddAsync(AdminAuditLog auditLog, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminAuditLog>> QueryAsync(
        AdminAuditLogQuery query,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
