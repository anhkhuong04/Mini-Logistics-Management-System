using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

/// <summary>
/// Defines persistence operations for Integration Management Scope data.
/// </summary>
public interface IIntegrationManagementScopeRepository
{
    Task<bool> AnyActiveScopeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IntegrationManagementScope>> GetActiveByActorUserIdAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
