using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminAuditing;

public interface IGetAdminAuditLogsService
{
    Task<Result<IReadOnlyList<AdminAuditLogResponse>>> GetAsync(
        AdminAuditLogQuery query,
        CancellationToken cancellationToken = default);
}
