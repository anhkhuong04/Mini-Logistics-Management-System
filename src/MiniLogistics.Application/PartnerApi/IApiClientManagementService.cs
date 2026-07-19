using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.PartnerApi;

/// <summary>
/// Manages Partner API client credentials and activation state from the admin dashboard.
/// </summary>
public interface IApiClientManagementService
{
    /// <summary>
    /// Creates an API client and returns the one-time plain-text secret.
    /// </summary>
    /// <param name="command">The requested API client name and shop scope.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Result<PartnerApiClientSecretResponse>> CreateApiClientAsync(
        CreatePartnerApiClientCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates an API client's secret and returns the replacement one-time plain-text secret.
    /// </summary>
    /// <param name="command">The API client rotation request.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Result<PartnerApiClientSecretResponse>> RotateApiClientKeyAsync(
        RotatePartnerApiClientKeyCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates or revokes an API client.
    /// </summary>
    /// <param name="command">The requested API client active state change.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Result> SetApiClientActiveStatusAsync(
        SetPartnerApiClientActiveStatusCommand command,
        CancellationToken cancellationToken = default);
}
