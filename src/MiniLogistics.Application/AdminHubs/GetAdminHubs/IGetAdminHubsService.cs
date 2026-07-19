using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminHubs.GetAdminHubs;

/// <summary>
/// Defines the application use case contract for Get Admin Hubs.
/// </summary>
public interface IGetAdminHubsService
{
    Task<Result<IReadOnlyList<AdminHubResponse>>> GetAsync(
        AdminHubQuery query,
        CancellationToken cancellationToken = default);
}
