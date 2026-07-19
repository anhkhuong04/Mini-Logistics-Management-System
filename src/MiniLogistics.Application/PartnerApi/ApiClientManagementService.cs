using System.Security.Cryptography;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Common;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

public sealed class ApiClientManagementService : IApiClientManagementService
{
    private readonly IIntegrationScopeService _scopeService;
    private readonly IApiClientRepository _apiClientRepository;
    private readonly PartnerCredentialAuditWriter _credentialAuditWriter;
    private readonly IAdminAuditService _adminAuditService;
    private readonly TimeProvider _timeProvider;

    public ApiClientManagementService(
        IIntegrationScopeService scopeService,
        IApiClientRepository apiClientRepository,
        PartnerCredentialAuditWriter credentialAuditWriter,
        TimeProvider timeProvider,
        IAdminAuditService? adminAuditService = null)
    {
        _scopeService = scopeService;
        _apiClientRepository = apiClientRepository;
        _credentialAuditWriter = credentialAuditWriter;
        _timeProvider = timeProvider;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
    }

    public async Task<Result<PartnerApiClientSecretResponse>> CreateApiClientAsync(
        CreatePartnerApiClientCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            var validationError = ApplicationErrors.ValidationFailed("API client name is required.");
            await _credentialAuditWriter.SaveAsync(
                command.CurrentUserId,
                command.ShopId,
                apiClientId: null,
                PartnerApiCredentialAuditActions.ApiClientCreated,
                isSuccess: false,
                validationError,
                cancellationToken);

            return Result<PartnerApiClientSecretResponse>.Failure(validationError);
        }

        var accessResult = await _scopeService.EnsureCanManageShopAsync(
            command.CurrentUserId,
            command.ShopId,
            cancellationToken);
        if (accessResult.IsFailure)
        {
            await _credentialAuditWriter.SaveAsync(
                command.CurrentUserId,
                command.ShopId,
                apiClientId: null,
                PartnerApiCredentialAuditActions.ApiClientCreated,
                isSuccess: false,
                accessResult.Error,
                cancellationToken);

            return Result<PartnerApiClientSecretResponse>.Failure(accessResult.Error);
        }

        var apiKey = GenerateApiKey();
        var now = _timeProvider.GetUtcNow();
        var apiClient = new ApiClient(
            command.ShopId,
            command.Name,
            ApiKeyHasher.GetPrefix(apiKey),
            ApiKeyHasher.Hash(apiKey),
            now);

        await _apiClientRepository.AddAsync(apiClient, cancellationToken);
        await _credentialAuditWriter.AddAsync(
            command.CurrentUserId,
            apiClient.ShopId,
            apiClient.Id,
            PartnerApiCredentialAuditActions.ApiClientCreated,
            isSuccess: true,
            error: null,
            cancellationToken);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.CurrentUserId,
                AdminAuditActions.PartnerApiClientCreated,
                AdminAuditTargetTypes.PartnerApiClient,
                apiClient.Id,
                NewValue: new
                {
                    apiClient.ShopId,
                    apiClient.Name,
                    apiClient.ApiKeyPrefix,
                    apiClient.IsActive
                }),
            cancellationToken);
        await _apiClientRepository.SaveChangesAsync(cancellationToken);

        return Result<PartnerApiClientSecretResponse>.Success(new PartnerApiClientSecretResponse(
            apiClient.Id,
            apiKey,
            apiClient.ApiKeyPrefix));
    }

    public async Task<Result<PartnerApiClientSecretResponse>> RotateApiClientKeyAsync(
        RotatePartnerApiClientKeyCommand command,
        CancellationToken cancellationToken = default)
    {
        var apiClientResult = await _scopeService.GetManageableApiClientAsync(
            command.CurrentUserId,
            command.ApiClientId,
            cancellationToken);
        if (apiClientResult.IsFailure)
        {
            return Result<PartnerApiClientSecretResponse>.Failure(apiClientResult.Error);
        }

        var apiClient = apiClientResult.Value;
        var oldApiKeyPrefix = apiClient.ApiKeyPrefix;
        var oldIsActive = apiClient.IsActive;
        var apiKey = GenerateApiKey();
        var now = _timeProvider.GetUtcNow();
        apiClient.RotateKey(ApiKeyHasher.GetPrefix(apiKey), ApiKeyHasher.Hash(apiKey), now);
        apiClient.Activate(now);

        await _credentialAuditWriter.AddAsync(
            command.CurrentUserId,
            apiClient.ShopId,
            apiClient.Id,
            PartnerApiCredentialAuditActions.ApiClientKeyRotated,
            isSuccess: true,
            error: null,
            cancellationToken);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.CurrentUserId,
                AdminAuditActions.PartnerApiClientKeyRotated,
                AdminAuditTargetTypes.PartnerApiClient,
                apiClient.Id,
                OldValue: new
                {
                    ApiKeyPrefix = oldApiKeyPrefix,
                    IsActive = oldIsActive
                },
                NewValue: new
                {
                    apiClient.ApiKeyPrefix,
                    apiClient.IsActive
                }),
            cancellationToken);
        await _apiClientRepository.SaveChangesAsync(cancellationToken);

        return Result<PartnerApiClientSecretResponse>.Success(new PartnerApiClientSecretResponse(
            apiClient.Id,
            apiKey,
            apiClient.ApiKeyPrefix));
    }

    public async Task<Result> SetApiClientActiveStatusAsync(
        SetPartnerApiClientActiveStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        var apiClientResult = await _scopeService.GetManageableApiClientAsync(
            command.CurrentUserId,
            command.ApiClientId,
            cancellationToken);
        if (apiClientResult.IsFailure)
        {
            return Result.Failure(apiClientResult.Error);
        }

        var oldIsActive = apiClientResult.Value.IsActive;
        var now = _timeProvider.GetUtcNow();
        if (command.IsActive)
        {
            apiClientResult.Value.Activate(now);
        }
        else
        {
            apiClientResult.Value.Deactivate(now);
        }

        await _credentialAuditWriter.AddAsync(
            command.CurrentUserId,
            apiClientResult.Value.ShopId,
            apiClientResult.Value.Id,
            command.IsActive
                ? PartnerApiCredentialAuditActions.ApiClientActivated
                : PartnerApiCredentialAuditActions.ApiClientDeactivated,
            isSuccess: true,
            error: null,
            cancellationToken);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.CurrentUserId,
                AdminAuditActions.PartnerApiClientActiveStatusChanged,
                AdminAuditTargetTypes.PartnerApiClient,
                apiClientResult.Value.Id,
                OldValue: new
                {
                    IsActive = oldIsActive
                },
                NewValue: new
                {
                    apiClientResult.Value.IsActive
                }),
            cancellationToken);
        await _apiClientRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return "ml_live_" + Convert.ToBase64String(bytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }
}
