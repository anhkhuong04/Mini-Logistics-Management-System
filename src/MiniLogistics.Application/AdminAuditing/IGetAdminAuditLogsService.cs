using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminAuditing;

/// <summary>
/// Defines the application use case contract for Get Admin Audit Logs.
/// </summary>
public interface IGetAdminAuditLogsService
{
    Task<Result<IReadOnlyList<AdminAuditLogResponse>>> GetAsync(
        AdminAuditLogQuery query,
        CancellationToken cancellationToken = default);
}
