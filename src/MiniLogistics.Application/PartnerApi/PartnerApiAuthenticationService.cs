using MiniLogistics.Domain.Common;
using MiniLogistics.Application.Shops;

namespace MiniLogistics.Application.PartnerApi;

public sealed class PartnerApiAuthenticationService : IPartnerApiAuthenticationService
{
    private const string BearerPrefix = "Bearer ";

    private readonly IApiClientRepository _apiClientRepository;
    private readonly IShopRepository _shopRepository;

    public PartnerApiAuthenticationService(
        IApiClientRepository apiClientRepository,
        IShopRepository shopRepository)
    {
        _apiClientRepository = apiClientRepository;
        _shopRepository = shopRepository;
    }

    public async Task<Result<PartnerApiClientContext>> AuthenticateAsync(
        string? authorizationHeader,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return Result<PartnerApiClientContext>.Failure(PartnerApiErrors.MissingApiKey);
        }

        if (!authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Result<PartnerApiClientContext>.Failure(PartnerApiErrors.InvalidApiKey);
        }

        var apiKey = authorizationHeader[BearerPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Result<PartnerApiClientContext>.Failure(PartnerApiErrors.InvalidApiKey);
        }

        var apiKeyHash = ApiKeyHasher.Hash(apiKey);
        var apiClient = await _apiClientRepository.GetByApiKeyHashAsync(apiKeyHash, cancellationToken);
        if (apiClient is null)
        {
            return Result<PartnerApiClientContext>.Failure(PartnerApiErrors.InvalidApiKey);
        }

        if (!apiClient.IsActive)
        {
            return Result<PartnerApiClientContext>.Failure(PartnerApiErrors.ApiClientInactive);
        }

        var shop = await _shopRepository.GetByIdAsync(apiClient.ShopId, cancellationToken);
        if (shop is null)
        {
            return Result<PartnerApiClientContext>.Failure(PartnerApiErrors.InvalidApiKey);
        }

        if (!shop.IsActive)
        {
            return Result<PartnerApiClientContext>.Failure(PartnerApiErrors.ShopInactive);
        }

        apiClient.MarkUsed();
        await _apiClientRepository.SaveChangesAsync(cancellationToken);

        return Result<PartnerApiClientContext>.Success(new PartnerApiClientContext(
            apiClient.Id,
            apiClient.ShopId,
            apiClient.Name));
    }
}
