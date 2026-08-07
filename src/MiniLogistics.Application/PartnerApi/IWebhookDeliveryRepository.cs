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

    async Task<IReadOnlyList<WebhookDelivery>> ClaimDueAsync(
        string workerId,
        DateTimeOffset nowUtc,
        DateTimeOffset lockedUntilUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var due = await GetDueAsync(nowUtc, batchSize, cancellationToken);
        return due
            .Where(delivery => delivery.TryAcquireLease(workerId, Guid.NewGuid(), nowUtc, lockedUntilUtc))
            .ToList();
    }

    Task<WebhookDelivery?> GetByIdAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    Task AddAsync(
        WebhookDelivery delivery,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
