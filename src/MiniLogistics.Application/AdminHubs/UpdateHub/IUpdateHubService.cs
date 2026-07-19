using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminHubs.UpdateHub;

/// <summary>
/// Defines the application use case contract for Update Hub.
/// </summary>
public interface IUpdateHubService
{
    Task<Result<AdminHubResponse>> UpdateAsync(
        UpdateHubCommand command,
        CancellationToken cancellationToken = default);
}
