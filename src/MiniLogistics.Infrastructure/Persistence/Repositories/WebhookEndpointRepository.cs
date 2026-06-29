using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class WebhookEndpointRepository : IWebhookEndpointRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public WebhookEndpointRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<WebhookEndpoint>> GetActiveByApiClientIdAsync(
        Guid apiClientId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.WebhookEndpoints
            .AsNoTracking()
            .Where(endpoint => endpoint.ApiClientId == apiClientId && endpoint.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WebhookEndpoint>> GetByApiClientIdsAsync(
        IReadOnlyCollection<Guid> apiClientIds,
        CancellationToken cancellationToken = default)
    {
        if (apiClientIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.WebhookEndpoints
            .AsNoTracking()
            .Where(endpoint => apiClientIds.Contains(endpoint.ApiClientId))
            .OrderBy(endpoint => endpoint.ApiClientId)
            .ThenByDescending(endpoint => endpoint.IsActive)
            .ThenByDescending(endpoint => endpoint.UpdatedAtUtc ?? endpoint.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<WebhookEndpoint?> GetByIdAsync(
        Guid endpointId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.WebhookEndpoints
            .AsNoTracking()
            .FirstOrDefaultAsync(endpoint => endpoint.Id == endpointId, cancellationToken);
    }

    public Task<WebhookEndpoint?> GetLatestByApiClientIdAsync(
        Guid apiClientId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.WebhookEndpoints
            .Where(endpoint => endpoint.ApiClientId == apiClientId)
            .OrderByDescending(endpoint => endpoint.IsActive)
            .ThenByDescending(endpoint => endpoint.UpdatedAtUtc ?? endpoint.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(WebhookEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        await _dbContext.WebhookEndpoints.AddAsync(endpoint, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
