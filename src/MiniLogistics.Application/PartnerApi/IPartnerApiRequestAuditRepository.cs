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

    Task<IReadOnlyDictionary<Guid, PartnerApiUsageMetricsResponse>> GetUsageByApiClientIdsAsync(
        IReadOnlyCollection<Guid> apiClientIds,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<Guid, PartnerApiUsageMetricsResponse> empty =
            new Dictionary<Guid, PartnerApiUsageMetricsResponse>();
        return Task.FromResult(empty);
    }
}
