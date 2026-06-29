using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.PartnerApi;

public interface IPartnerIntegrationManagementService
{
    Task<Result<PartnerIntegrationDashboardResponse>> GetDashboardAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<Result<PartnerApiClientSecretResponse>> CreateApiClientAsync(
        CreatePartnerApiClientCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<PartnerApiClientSecretResponse>> RotateApiClientKeyAsync(
        RotatePartnerApiClientKeyCommand command,
        CancellationToken cancellationToken = default);

    Task<Result> SetApiClientActiveStatusAsync(
        SetPartnerApiClientActiveStatusCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<PartnerWebhookEndpointResponse>> UpsertWebhookEndpointAsync(
        UpsertPartnerWebhookEndpointCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<PartnerWebhookTestResponse>> TestWebhookAsync(
        TestPartnerWebhookCommand command,
        CancellationToken cancellationToken = default);
}
