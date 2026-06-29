using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

public interface IWebhookDeliveryRepository
{
    Task<IReadOnlyList<WebhookDelivery>> GetDueAsync(
        DateTimeOffset dueAtUtc,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WebhookDelivery>> GetRecentByApiClientIdsAsync(
        IReadOnlyCollection<Guid> apiClientIds,
        int takePerClient,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        WebhookDelivery delivery,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
