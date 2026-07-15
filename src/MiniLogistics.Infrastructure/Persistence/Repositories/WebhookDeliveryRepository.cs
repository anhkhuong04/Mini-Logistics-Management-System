using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class WebhookDeliveryRepository : IWebhookDeliveryRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public WebhookDeliveryRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.WebhookDeliveries
            .AnyAsync(delivery => delivery.Id == deliveryId, cancellationToken);
    }

    public async Task<IReadOnlyList<WebhookDelivery>> GetDueAsync(
        DateTimeOffset dueAtUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.WebhookDeliveries
            .Where(delivery =>
                delivery.Status != WebhookDeliveryStatus.Succeeded
                && delivery.NextAttemptAtUtc != null
                && delivery.NextAttemptAtUtc <= dueAtUtc)
            .OrderBy(delivery => delivery.NextAttemptAtUtc)
            .ThenBy(delivery => delivery.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WebhookDelivery>> GetRecentByApiClientIdsAsync(
        IReadOnlyCollection<Guid> apiClientIds,
        int takePerClient,
        CancellationToken cancellationToken = default)
    {
        if (apiClientIds.Count == 0 || takePerClient <= 0)
        {
            return [];
        }

        return await _dbContext.WebhookDeliveries
            .AsNoTracking()
            .Where(delivery => apiClientIds.Contains(delivery.ApiClientId))
            .GroupBy(delivery => delivery.ApiClientId)
            .SelectMany(group => group
                .OrderByDescending(delivery => delivery.CreatedAtUtc)
                .Take(takePerClient))
            .OrderByDescending(delivery => delivery.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default)
    {
        await _dbContext.WebhookDeliveries.AddAsync(delivery, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
