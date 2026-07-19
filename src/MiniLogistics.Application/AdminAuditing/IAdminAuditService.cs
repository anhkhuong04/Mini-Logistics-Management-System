using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminAuditing;

/// <summary>
/// Defines the application use case contract for Admin Audit.
/// </summary>
public interface IAdminAuditService
{
    Task RecordAsync(
        AdminAuditEntry entry,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class NullAdminAuditService : IAdminAuditService
{
    public static readonly NullAdminAuditService Instance = new();

    private NullAdminAuditService()
    {
    }

    public Task RecordAsync(AdminAuditEntry entry, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
