namespace MiniLogistics.Application.AdminCod;

public interface IAdminCodReportRepository
{
    Task<AdminCodReportResponse> GetAsync(
        AdminCodReportQuery query,
        CancellationToken cancellationToken = default);
}
