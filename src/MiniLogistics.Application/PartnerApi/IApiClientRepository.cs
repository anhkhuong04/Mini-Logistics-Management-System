using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

public interface IApiClientRepository
{
    Task<ApiClient?> GetByIdAsync(
        Guid apiClientId,
        CancellationToken cancellationToken = default);

    Task<ApiClient?> GetByApiKeyHashAsync(
        string apiKeyHash,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApiClient>> GetByShopIdsAsync(
        IReadOnlyCollection<Guid> shopIds,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        ApiClient apiClient,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
