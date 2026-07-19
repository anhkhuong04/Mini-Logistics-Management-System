using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminDashboard;

/// <summary>
/// Defines the application use case contract for Get Admin Dashboard.
/// </summary>
public interface IGetAdminDashboardService
{
    Task<Result<AdminDashboardResponse>> GetAsync(
        AdminDashboardQuery query,
        CancellationToken cancellationToken = default);
}
