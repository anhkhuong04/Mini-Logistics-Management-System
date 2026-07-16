using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminHubs.CreateHub;

public interface ICreateHubService
{
    Task<Result<AdminHubResponse>> CreateAsync(
        CreateHubCommand command,
        CancellationToken cancellationToken = default);
}
