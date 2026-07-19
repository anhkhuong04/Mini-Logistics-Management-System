namespace MiniLogistics.Application.AdminCod;

/// <summary>
/// Defines persistence operations for Admin Cod Report data.
/// </summary>
public interface IAdminCodReportRepository
{
    Task<AdminCodReportResponse> GetAsync(
        AdminCodReportQuery query,
        CancellationToken cancellationToken = default);
}
