using Microsoft.Extensions.Logging;
using MiniLogistics.Domain.Common;
using MiniLogistics.Application.Shops;

namespace MiniLogistics.Application.PartnerApi;

public sealed class PartnerApiAuthenticationService : IPartnerApiAuthenticationService
{
    private const string BearerPrefix = "Bearer ";
    private static readonly TimeSpan UsageWriteInterval = TimeSpan.FromMinutes(15);

    private readonly IApiClientRepository _apiClientRepository;
    private readonly IShopRepository _shopRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PartnerApiAuthenticationService>? _logger;
    private readonly IApiCredentialPolicy? _apiCredentialPolicy;

    public PartnerApiAuthenticationService(
        IApiClientRepository apiClientRepository,
        IShopRepository shopRepository,
        TimeProvider timeProvider,
        ILogger<PartnerApiAuthenticationService>? logger = null,
        IApiCredentialPolicy? apiCredentialPolicy = null)
    {
        _apiClientRepository = apiClientRepository;
        _shopRepository = shopRepository;
        _timeProvider = timeProvider;
        _logger = logger;
        _apiCredentialPolicy = apiCredentialPolicy;
    }

    public async Task<Result<PartnerApiClientContext>> AuthenticateAsync(
        string? authorizationHeader,
        CancellationToken cancellationToken = default)
    {
        return await AuthenticateAsync(authorizationHeader, remoteIpAddress: null, cancellationToken);
    }

    public async Task<Result<PartnerApiClientContext>> AuthenticateAsync(
        string? authorizationHeader,
        string? remoteIpAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            _logger?.LogDebug("Partner API authentication failed because Authorization header is missing");
            return Result<PartnerApiClientContext>.Failure(PartnerApiErrors.MissingApiKey);
        }

        if (!authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogDebug("Partner API authentication failed because Authorization scheme is invalid");
            return Result<PartnerApiClientContext>.Failure(PartnerApiErrors.InvalidApiKey);
        }

        var apiKey = authorizationHeader[BearerPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger?.LogDebug("Partner API authentication failed because bearer token is empty");
            return Result<PartnerApiClientContext>.Failure(PartnerApiErrors.InvalidApiKey);
        }

        if (_apiCredentialPolicy is not null && !_apiCredentialPolicy.IsAllowed(apiKey))
        {
            _logger?.LogDebug("Partner API authentication failed because API key environment prefix is invalid");
            return Result<PartnerApiClientContext>.Failure(PartnerApiErrors.InvalidApiKey);
        }

        var apiKeyHash = ApiKeyHasher.Hash(apiKey);
        var apiClient = await _apiClientRepository.GetByApiKeyHashAsync(apiKeyHash, cancellationToken);
        if (apiClient is null)
        {
            _logger?.LogDebug("Partner API authentication failed because API key hash was not found");
            return Result<PartnerApiClientContext>.Failure(PartnerApiErrors.InvalidApiKey);
        }

        if (!apiClient.IsActive)
        {
            _logger?.LogWarning(
                "Partner API authentication failed because API client {ApiClientId} is inactive",
                apiClient.Id);
            return Result<PartnerApiClientContext>.Failure(PartnerApiErrors.ApiClientInactive);
        }

        var now = _timeProvider.GetUtcNow();
        if (apiClient.IsExpired(now))
        {
            _logger?.LogWarning("Partner API client {ApiClientId} is expired", apiClient.Id);
            return Result<PartnerApiClientContext>.Failure(PartnerApiErrors.ApiClientExpired);
        }

        if (!apiClient.IsIpAllowed(remoteIpAddress))
        {
            _logger?.LogWarning(
                "Partner API client {ApiClientId} rejected the resolved request IP.",
                apiClient.Id);
            return Result<PartnerApiClientContext>.Failure(PartnerApiErrors.IpNotAllowed);
        }

        var shop = await _shopRepository.GetByIdAsync(apiClient.ShopId, cancellationToken);
        if (shop is null)
        {
            _logger?.LogWarning(
                "Partner API authentication failed because shop {ShopId} for API client {ApiClientId} was not found",
                apiClient.ShopId,
                apiClient.Id);
            return Result<PartnerApiClientContext>.Failure(PartnerApiErrors.InvalidApiKey);
        }

        if (!shop.IsActive)
        {
            _logger?.LogWarning(
                "Partner API authentication failed because shop {ShopId} for API client {ApiClientId} is inactive",
                shop.Id,
                apiClient.Id);
            return Result<PartnerApiClientContext>.Failure(PartnerApiErrors.ShopInactive);
        }

        await _apiClientRepository.MarkUsedIfStaleAsync(
            apiClient.Id,
            now,
            UsageWriteInterval,
            cancellationToken);

        _logger?.LogDebug(
            "Partner API client {ApiClientId} authenticated for shop {ShopId}",
            apiClient.Id,
            apiClient.ShopId);

        return Result<PartnerApiClientContext>.Success(new PartnerApiClientContext(
            apiClient.Id,
            apiClient.ShopId,
            apiClient.Name,
            apiClient.Scopes));
    }
}
