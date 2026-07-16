using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminHubs.UpdateHub;

public interface IUpdateHubService
{
    Task<Result<AdminHubResponse>> UpdateAsync(
        UpdateHubCommand command,
        CancellationToken cancellationToken = default);
}
