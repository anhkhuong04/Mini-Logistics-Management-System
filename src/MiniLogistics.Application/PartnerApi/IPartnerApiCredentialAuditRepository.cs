using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

/// <summary>
/// Defines persistence operations for Partner Api Credential Audit data.
/// </summary>
public interface IPartnerApiCredentialAuditRepository
{
    Task<IReadOnlyList<PartnerApiCredentialAudit>> GetRecentByApiClientIdsAsync(
        IReadOnlyCollection<Guid> apiClientIds,
        int takePerClient,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        PartnerApiCredentialAudit audit,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
