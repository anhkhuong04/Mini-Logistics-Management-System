using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminCod;

/// <summary>
/// Defines the application use case contract for Get Admin Cod Report.
/// </summary>
public interface IGetAdminCodReportService
{
    Task<Result<AdminCodReportResponse>> GetAsync(
        AdminCodReportQuery query,
        CancellationToken cancellationToken = default);
}
