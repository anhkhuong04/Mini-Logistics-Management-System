using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

public interface IIntegrationManagementScopeRepository
{
    Task<bool> AnyActiveScopeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IntegrationManagementScope>> GetActiveByActorUserIdAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
