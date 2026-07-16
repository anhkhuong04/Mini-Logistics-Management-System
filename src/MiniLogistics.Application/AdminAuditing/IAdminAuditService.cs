using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminAuditing;

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
