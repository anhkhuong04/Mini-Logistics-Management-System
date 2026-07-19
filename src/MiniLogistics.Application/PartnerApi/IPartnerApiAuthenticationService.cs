using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.PartnerApi;

/// <summary>
/// Authenticates Partner API requests from the HTTP Authorization header.
/// </summary>
public interface IPartnerApiAuthenticationService
{
    /// <summary>
    /// Validates a bearer API key and returns the partner client/shop context when authorized.
    /// </summary>
    /// <param name="authorizationHeader">The raw Authorization header value from the request.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Result<PartnerApiClientContext>> AuthenticateAsync(
        string? authorizationHeader,
        CancellationToken cancellationToken = default);
}
