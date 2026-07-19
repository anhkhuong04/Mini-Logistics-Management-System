using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.PartnerApi;

/// <summary>
/// Manages partner webhook endpoints and dashboard data.
/// </summary>
public interface IWebhookManagementService
{
    /// <summary>
    /// Returns dashboard data for the current admin user's manageable partner integrations.
    /// </summary>
    /// <param name="currentUserId">The authenticated admin user identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Result<PartnerIntegrationDashboardResponse>> GetDashboardAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a webhook endpoint for an API client.
    /// </summary>
    /// <param name="command">The requested webhook endpoint configuration.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Result<PartnerWebhookEndpointResponse>> UpsertWebhookEndpointAsync(
        UpsertPartnerWebhookEndpointCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queues a signed test webhook delivery for a configured endpoint.
    /// </summary>
    /// <param name="command">The webhook test request.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Result<PartnerWebhookTestResponse>> TestWebhookAsync(
        TestPartnerWebhookCommand command,
        CancellationToken cancellationToken = default);
}
