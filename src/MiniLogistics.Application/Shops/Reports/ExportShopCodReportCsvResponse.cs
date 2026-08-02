namespace MiniLogistics.Application.Shops.Reports;

public sealed record ExportShopCodReportCsvResponse(
    string FileName,
    string ContentType,
    byte[] Content);

