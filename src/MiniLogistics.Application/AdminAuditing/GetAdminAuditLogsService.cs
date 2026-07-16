using MiniLogistics.Application.AdminUsers;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminAuditing;

public sealed class GetAdminAuditLogsService : IGetAdminAuditLogsService
{
    private readonly IIdentityService _identityService;
    private readonly IAdminAuditLogRepository _auditLogRepository;

    public GetAdminAuditLogsService(
        IIdentityService identityService,
        IAdminAuditLogRepository auditLogRepository)
    {
        _identityService = identityService;
        _auditLogRepository = auditLogRepository;
    }

    public async Task<Result<IReadOnlyList<AdminAuditLogResponse>>> GetAsync(
        AdminAuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.RequestedByUserId == Guid.Empty)
        {
            return Result<IReadOnlyList<AdminAuditLogResponse>>.Failure(
                ApplicationErrors.ValidationFailed("Requested by user id is required."));
        }

        var authorizationResult = await AdminUserAuthorization.EnsureActiveAdminAsync(
            _identityService,
            query.RequestedByUserId,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyList<AdminAuditLogResponse>>.Failure(authorizationResult.Error);
        }

        var auditLogs = await _auditLogRepository.QueryAsync(
            query with { Limit = Math.Clamp(query.Limit, 1, 500) },
            cancellationToken);

        return Result<IReadOnlyList<AdminAuditLogResponse>>.Success(auditLogs
            .Select(auditLog => new AdminAuditLogResponse(
                auditLog.Id,
                auditLog.ActorUserId,
                auditLog.ActorRole,
                auditLog.Action,
                auditLog.TargetType,
                auditLog.TargetId,
                auditLog.OldValueJson,
                auditLog.NewValueJson,
                auditLog.Reason,
                auditLog.IpAddress,
                auditLog.UserAgent,
                auditLog.CreatedAtUtc))
            .ToList());
    }
}
