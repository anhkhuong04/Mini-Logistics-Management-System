using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.PartnerApi;

public sealed class PartnerApiAuthenticationService : IPartnerApiAuthenticationService
{
    private const string BearerPrefix = "Bearer ";

    private readonly IApiClientRepository _apiClientRepository;

    public PartnerApiAuthenticationService(IApiClientRepository apiClientRepository)
    {
        _apiClientRepository = apiClientRepository;
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

        apiClient.MarkUsed();
        await _apiClientRepository.SaveChangesAsync(cancellationToken);

        return Result<PartnerApiClientContext>.Success(new PartnerApiClientContext(
            apiClient.Id,
            apiClient.ShopId,
            apiClient.Name));
    }
}
