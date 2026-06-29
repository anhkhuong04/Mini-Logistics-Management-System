using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.PartnerApi;

public interface IPartnerApiAuthenticationService
{
    Task<Result<PartnerApiClientContext>> AuthenticateAsync(
        string? authorizationHeader,
        CancellationToken cancellationToken = default);
}
