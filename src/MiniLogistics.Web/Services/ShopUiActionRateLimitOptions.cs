namespace MiniLogistics.Web.Services;

public sealed class ShopUiActionRateLimitOptions
{
    public const string SectionName = "ShopUi:RateLimiting";

    public int CreateShipmentLimitPerMinute { get; set; } = 30;

    public int ImportPreviewLimitPerMinute { get; set; } = 10;

    public int ImportConfirmLimitPerMinute { get; set; } = 5;

    public int ExportShipmentsLimitPerMinute { get; set; } = 10;

    public int ExportCodReportLimitPerMinute { get; set; } = 10;

    public int GenerateLabelLimitPerMinute { get; set; } = 30;

    public int GetLimit(ShopUiActionKind kind)
    {
        return kind switch
        {
            ShopUiActionKind.CreateShipment => CreateShipmentLimitPerMinute,
            ShopUiActionKind.ImportPreview => ImportPreviewLimitPerMinute,
            ShopUiActionKind.ImportConfirm => ImportConfirmLimitPerMinute,
            ShopUiActionKind.ExportShipments => ExportShipmentsLimitPerMinute,
            ShopUiActionKind.ExportCodReport => ExportCodReportLimitPerMinute,
            ShopUiActionKind.GenerateLabel => GenerateLabelLimitPerMinute,
            _ => ExportShipmentsLimitPerMinute
        };
    }
}

