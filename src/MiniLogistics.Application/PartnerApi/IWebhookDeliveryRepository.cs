using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

/// <summary>
/// Defines persistence operations for Webhook Delivery data.
/// </summary>
public interface IWebhookDeliveryRepository
{
    Task<bool> ExistsAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default);

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
