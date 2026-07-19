using MiniLogistics.Domain.AdminAuditing;

namespace MiniLogistics.Application.AdminAuditing;

/// <summary>
/// Defines persistence operations for Admin Audit Log data.
/// </summary>
public interface IAdminAuditLogRepository
{
    Task AddAsync(AdminAuditLog auditLog, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminAuditLog>> QueryAsync(
        AdminAuditLogQuery query,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
