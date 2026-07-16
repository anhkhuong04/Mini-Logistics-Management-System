using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminHubs.GetAdminHubs;

public interface IGetAdminHubsService
{
    Task<Result<IReadOnlyList<AdminHubResponse>>> GetAsync(
        AdminHubQuery query,
        CancellationToken cancellationToken = default);
}
