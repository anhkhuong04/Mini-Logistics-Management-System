using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.PartnerApi;

/// <summary>
/// Resolves the shops and API clients that an admin user may manage.
/// </summary>
public interface IIntegrationScopeService
{
    /// <summary>
    /// Returns shops accessible to the current admin user for partner integration management.
    /// </summary>
    /// <param name="currentUserId">The authenticated admin user identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Result<IntegrationShopAccessResult>> GetAccessibleShopsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the current admin user can manage integrations for a shop.
    /// </summary>
    /// <param name="currentUserId">The authenticated admin user identifier.</param>
    /// <param name="shopId">The shop that must be manageable.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Result> EnsureCanManageShopAsync(
        Guid currentUserId,
        Guid shopId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a manageable API client for the current admin user.
    /// </summary>
    /// <param name="currentUserId">The authenticated admin user identifier.</param>
    /// <param name="apiClientId">The API client identifier to load.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Result<ApiClient>> GetManageableApiClientAsync(
        Guid currentUserId,
        Guid apiClientId,
        CancellationToken cancellationToken = default);
}

public sealed record IntegrationShopAccessResult(
    IReadOnlyList<Shop> Shops,
    bool GranularPermissionEnabled);
