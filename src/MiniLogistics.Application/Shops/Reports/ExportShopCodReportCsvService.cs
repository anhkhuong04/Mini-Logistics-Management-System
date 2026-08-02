using System.Globalization;
using System.Text;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Domain.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shops.Reports;

public sealed class ExportShopCodReportCsvService : IExportShopCodReportCsvService
{
    private readonly IGetShopCodReportService _codReportService;
    private readonly IAdminAuditService _auditService;
    private readonly IShopAccessService _shopAccessService;

    public ExportShopCodReportCsvService(
        IGetShopCodReportService codReportService,
        IAdminAuditService auditService,
        IShopAccessService shopAccessService)
    {
        _codReportService = codReportService;
        _auditService = auditService;
        _shopAccessService = shopAccessService;
    }

    public async Task<Result<ExportShopCodReportCsvResponse>> ExportAsync(
        ExportShopCodReportCsvCommand command,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await _shopAccessService.GetShopAccessAsync(
            command.OwnerUserId,
            command.ShopId,
            requireActiveShop: false,
            ShopPermission.ViewCod | ShopPermission.ExportData,
            cancellationToken);
        if (accessResult.IsFailure)
        {
            return Result<ExportShopCodReportCsvResponse>.Failure(accessResult.Error);
        }

        var reportResult = await _codReportService.GetAsync(
            new GetShopCodReportQuery(
                command.OwnerUserId,
                command.ShopId,
                command.FromUtc,
                command.ToUtc),
            cancellationToken);
        if (reportResult.IsFailure)
        {
            return Result<ExportShopCodReportCsvResponse>.Failure(reportResult.Error);
        }

        var report = reportResult.Value;
        var builder = new StringBuilder();
        builder.AppendLine("TrackingCode,CreatedAtUtc,ShipmentStatus,CodStatus,DeclaredAmount,CollectedAmount,DiscrepancyAmount,CollectedAtUtc");

        foreach (var row in report.Rows)
        {
            builder
                .Append(ToCsvField(row.TrackingCode)).Append(',')
                .Append(ToCsvField(row.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture))).Append(',')
                .Append(ToCsvField(row.ShipmentStatus.ToString())).Append(',')
                .Append(ToCsvField(row.CodStatus.ToString())).Append(',')
                .Append(row.DeclaredAmount.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append((row.CollectedAmount ?? 0m).ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(row.DiscrepancyAmount.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(ToCsvField(row.CollectedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty))
                .AppendLine();
        }

        await _auditService.RecordAsync(
            new AdminAuditEntry(
                command.OwnerUserId,
                AdminAuditActions.ShopCodReportExported,
                AdminAuditTargetTypes.Shop,
                report.ShopId,
                NewValue: new
                {
                    command.FromUtc,
                    command.ToUtc,
                    RowCount = report.Rows.Count,
                    report.PendingCollectionAmount,
                    report.CollectedAmount,
                    report.SettledAmount,
                    report.DiscrepancyAmount
                },
                ActorRole: "Shop"),
            cancellationToken);
        await _auditService.SaveChangesAsync(cancellationToken);

        var fileName = $"cod-report-{report.ShopId:N}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return Result<ExportShopCodReportCsvResponse>.Success(new ExportShopCodReportCsvResponse(
            fileName,
            "text/csv; charset=utf-8",
            Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray()));
    }

    private static string ToCsvField(string value)
    {
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
