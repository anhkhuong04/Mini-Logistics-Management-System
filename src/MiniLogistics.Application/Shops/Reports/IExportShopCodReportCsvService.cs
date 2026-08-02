using MiniLogistics.Application.Common;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.Reports;

public interface IExportShopCodReportCsvService
{
    Task<Result<ExportShopCodReportCsvResponse>> ExportAsync(
        ExportShopCodReportCsvCommand command,
        CancellationToken cancellationToken = default);
}

