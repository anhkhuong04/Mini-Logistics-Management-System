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

    public Task<ApiClient?> GetByApiKeyHashAsync(
        string apiKeyHash,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ApiClients
            .FirstOrDefaultAsync(apiClient => apiClient.ApiKeyHash == apiKeyHash, cancellationToken);
    }

    public async Task AddAsync(ApiClient apiClient, CancellationToken cancellationToken = default)
    {
        await _dbContext.ApiClients.AddAsync(apiClient, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
