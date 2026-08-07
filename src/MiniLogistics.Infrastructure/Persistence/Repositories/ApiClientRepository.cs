using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class ApiClientRepository : IApiClientRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public ApiClientRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ApiClient?> GetByIdAsync(
        Guid apiClientId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ApiClients
            .FirstOrDefaultAsync(apiClient => apiClient.Id == apiClientId, cancellationToken);
    }

    public Task<ApiClient?> GetByApiKeyHashAsync(
        string apiKeyHash,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ApiClients
            .AsNoTracking()
            .FirstOrDefaultAsync(apiClient => apiClient.ApiKeyHash == apiKeyHash, cancellationToken);
    }

    public async Task<IReadOnlyList<ApiClient>> GetByShopIdsAsync(
        IReadOnlyCollection<Guid> shopIds,
        CancellationToken cancellationToken = default)
    {
        if (shopIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.ApiClients
            .AsNoTracking()
            .Where(apiClient => shopIds.Contains(apiClient.ShopId))
            .OrderBy(apiClient => apiClient.Name)
            .ThenBy(apiClient => apiClient.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ApiClient apiClient, CancellationToken cancellationToken = default)
    {
        await _dbContext.ApiClients.AddAsync(apiClient, cancellationToken);
    }

    public async Task MarkUsedIfStaleAsync(
        Guid apiClientId,
        DateTimeOffset usedAtUtc,
        TimeSpan minimumInterval,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(
                _dbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.SqlServer",
                StringComparison.Ordinal))
        {
            var apiClient = await _dbContext.ApiClients.FirstOrDefaultAsync(
                client => client.Id == apiClientId,
                cancellationToken);
            if (apiClient is null
                || (apiClient.LastUsedAtUtc.HasValue
                    && usedAtUtc - apiClient.LastUsedAtUtc.Value < minimumInterval))
            {
                return;
            }

            apiClient.MarkUsed(usedAtUtc);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var staleBeforeUtc = usedAtUtc - minimumInterval;
        await _dbContext.ApiClients
            .Where(apiClient =>
                apiClient.Id == apiClientId
                && (apiClient.LastUsedAtUtc == null || apiClient.LastUsedAtUtc <= staleBeforeUtc))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(apiClient => apiClient.LastUsedAtUtc, usedAtUtc)
                    .SetProperty(apiClient => apiClient.UpdatedAtUtc, usedAtUtc),
                cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
