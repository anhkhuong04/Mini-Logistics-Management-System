using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

/// <summary>
/// Defines persistence operations for Partner Api Request Audit data.
/// </summary>
public interface IPartnerApiRequestAuditRepository
{
    Task AddAsync(
        PartnerApiRequestAudit audit,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
