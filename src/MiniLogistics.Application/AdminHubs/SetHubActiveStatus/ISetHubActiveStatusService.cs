using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminHubs.SetHubActiveStatus;

public interface ISetHubActiveStatusService
{
    Task<Result<AdminHubResponse>> SetAsync(
        SetHubActiveStatusCommand command,
        CancellationToken cancellationToken = default);
}
