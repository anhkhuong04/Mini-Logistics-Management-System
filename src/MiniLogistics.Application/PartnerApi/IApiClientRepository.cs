using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

/// <summary>
/// Defines persistence operations for Api Client data.
/// </summary>
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

    async Task MarkUsedIfStaleAsync(
        Guid apiClientId,
        DateTimeOffset usedAtUtc,
        TimeSpan minimumInterval,
        CancellationToken cancellationToken = default)
    {
        var apiClient = await GetByIdAsync(apiClientId, cancellationToken);
        if (apiClient is null
            || (apiClient.LastUsedAtUtc.HasValue
                && usedAtUtc - apiClient.LastUsedAtUtc.Value < minimumInterval))
        {
            return;
        }

        apiClient.MarkUsed(usedAtUtc);
        await SaveChangesAsync(cancellationToken);
    }

    Task AddAsync(
        ApiClient apiClient,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
