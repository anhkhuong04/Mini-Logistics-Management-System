using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminHubs.CreateHub;

/// <summary>
/// Defines the application use case contract for Create Hub.
/// </summary>
public interface ICreateHubService
{
    Task<Result<AdminHubResponse>> CreateAsync(
        CreateHubCommand command,
        CancellationToken cancellationToken = default);
}
