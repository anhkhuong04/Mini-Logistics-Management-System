using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminCod;

public interface IGetAdminCodReportService
{
    Task<Result<AdminCodReportResponse>> GetAsync(
        AdminCodReportQuery query,
        CancellationToken cancellationToken = default);
}
