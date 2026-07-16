using System.Text.Json;
using Microsoft.AspNetCore.Http;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.AdminAuditing;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Infrastructure.Persistence;

public sealed class AdminAuditService : IAdminAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAdminAuditLogRepository _auditLogRepository;
    private readonly IIdentityService _identityService;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public AdminAuditService(
        IAdminAuditLogRepository auditLogRepository,
        IIdentityService identityService,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _auditLogRepository = auditLogRepository;
        _identityService = identityService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task RecordAsync(
        AdminAuditEntry entry,
        CancellationToken cancellationToken = default)
    {
        if (entry.ActorUserId == Guid.Empty || entry.TargetId == Guid.Empty)
        {
            return;
        }

        var actorRole = string.IsNullOrWhiteSpace(entry.ActorRole)
            ? await ResolvePrimaryRoleAsync(entry.ActorUserId, cancellationToken)
            : entry.ActorRole.Trim();
        var httpContext = _httpContextAccessor?.HttpContext;

        await _auditLogRepository.AddAsync(
            new AdminAuditLog(
                entry.ActorUserId,
                actorRole,
                entry.Action,
                entry.TargetType,
                entry.TargetId,
                Serialize(entry.OldValue),
                Serialize(entry.NewValue),
                entry.Reason,
                httpContext?.Connection.RemoteIpAddress?.ToString(),
                httpContext?.Request.Headers.UserAgent.ToString()),
            cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _auditLogRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> ResolvePrimaryRoleAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        foreach (var role in new[]
                 {
                     nameof(UserRole.Admin),
                     nameof(UserRole.Operator),
                     nameof(UserRole.Shipper),
                     nameof(UserRole.Shop)
                 })
        {
            var roleCheck = await _identityService.CheckUserRoleAsync(actorUserId, role, cancellationToken);
            if (!roleCheck.Exists)
            {
                return "Unknown";
            }

            if (roleCheck.IsInRole)
            {
                return role;
            }
        }

        return "Unknown";
    }

    private static string? Serialize(object? value)
    {
        return value is null
            ? null
            : JsonSerializer.Serialize(value, JsonOptions);
    }
}
