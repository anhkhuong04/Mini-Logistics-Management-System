using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

public interface IWebhookEndpointRepository
{
    Task<IReadOnlyList<WebhookEndpoint>> GetActiveByApiClientIdAsync(
        Guid apiClientId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WebhookEndpoint>> GetByApiClientIdsAsync(
        IReadOnlyCollection<Guid> apiClientIds,
        CancellationToken cancellationToken = default);

    Task<WebhookEndpoint?> GetByIdAsync(
        Guid endpointId,
        CancellationToken cancellationToken = default);

    Task<WebhookEndpoint?> GetLatestByApiClientIdAsync(
        Guid apiClientId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        WebhookEndpoint endpoint,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
