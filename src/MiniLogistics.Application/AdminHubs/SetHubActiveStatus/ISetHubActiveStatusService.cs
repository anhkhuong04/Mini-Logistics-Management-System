using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminHubs.SetHubActiveStatus;

/// <summary>
/// Defines the application use case contract for Set Hub Active Status.
/// </summary>
public interface ISetHubActiveStatusService
{
    Task<Result<AdminHubResponse>> SetAsync(
        SetHubActiveStatusCommand command,
        CancellationToken cancellationToken = default);
}
