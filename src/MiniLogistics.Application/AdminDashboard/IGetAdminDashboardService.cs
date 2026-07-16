using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminDashboard;

public interface IGetAdminDashboardService
{
    Task<Result<AdminDashboardResponse>> GetAsync(
        AdminDashboardQuery query,
        CancellationToken cancellationToken = default);
}
