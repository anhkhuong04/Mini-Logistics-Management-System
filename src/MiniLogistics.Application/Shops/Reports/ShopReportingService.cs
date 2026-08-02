using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shops.Reports;

public sealed class ShopReportingService : IGetShopCodReportService, IGetShopDashboardKpiService
{
    private const int MaxRows = 10_000;

    private readonly IShopAccessService _shopAccessService;
    private readonly IShopReportingRepository _reportingRepository;

    public ShopReportingService(
        IShopAccessService shopAccessService,
        IShopReportingRepository reportingRepository)
    {
        _shopAccessService = shopAccessService;
        _reportingRepository = reportingRepository;
    }

    public async Task<Result<ShopCodReportResponse>> GetAsync(
        GetShopCodReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var dateRangeValidation = ValidateDateRange(query.FromUtc, query.ToUtc);
        if (dateRangeValidation.IsFailure)
        {
            return Result<ShopCodReportResponse>.Failure(dateRangeValidation.Error);
        }

        var shopResult = await _shopAccessService.GetShopAccessAsync(
            query.OwnerUserId,
            query.ShopId,
            requireActiveShop: false,
            ShopPermission.ViewCod,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<ShopCodReportResponse>.Failure(shopResult.Error);
        }

        var rows = await _reportingRepository.GetCodReportRowsAsync(
            shopResult.Value.Shop.Id,
            query.FromUtc,
            query.ToUtc,
            MaxRows,
            cancellationToken);

        return Result<ShopCodReportResponse>.Success(new ShopCodReportResponse(
            shopResult.Value.Shop.Id,
            rows.Where(row => row.CodStatus == CodStatus.PendingCollection).Sum(row => row.DeclaredAmount),
            rows.Where(row => row.CodStatus is CodStatus.Collected or CodStatus.Settled).Sum(row => row.CollectedAmount ?? row.DeclaredAmount),
            rows.Where(row => row.CodStatus == CodStatus.Settled).Sum(row => row.CollectedAmount ?? row.DeclaredAmount),
            rows.Sum(row => row.DiscrepancyAmount),
            "VND",
            rows));
    }

    public async Task<Result<ShopDashboardKpiResponse>> GetAsync(
        ShopDashboardKpiQuery query,
        CancellationToken cancellationToken = default)
    {
        var dateRangeValidation = ValidateDateRange(query.FromUtc, query.ToUtc);
        if (dateRangeValidation.IsFailure)
        {
            return Result<ShopDashboardKpiResponse>.Failure(dateRangeValidation.Error);
        }

        var shopResult = await _shopAccessService.GetShopAccessAsync(
            query.OwnerUserId,
            query.ShopId,
            requireActiveShop: false,
            ShopPermission.ViewShipments,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<ShopDashboardKpiResponse>.Failure(shopResult.Error);
        }

        var metrics = await _reportingRepository.GetDashboardKpiAsync(
            shopResult.Value.Shop.Id,
            query.FromUtc,
            query.ToUtc,
            cancellationToken);
        var nonDraftCount = metrics.TotalShipments
            - metrics.CountByStatus.GetValueOrDefault(ShipmentStatus.Draft);
        var canViewCod = shopResult.Value.HasPermission(ShopPermission.ViewCod);

        return Result<ShopDashboardKpiResponse>.Success(new ShopDashboardKpiResponse(
            shopResult.Value.Shop.Id,
            metrics.TotalShipments,
            metrics.DeliveredShipments,
            metrics.ReturnedShipments,
            metrics.DeliveryFailedShipments,
            Rate(metrics.DeliveredShipments, nonDraftCount),
            Rate(metrics.ReturnedShipments, nonDraftCount),
            Rate(metrics.DeliveryFailedShipments, nonDraftCount),
            metrics.TotalShippingFee,
            canViewCod ? metrics.PendingCodAmount : 0m,
            canViewCod ? metrics.CollectedCodAmount : 0m,
            canViewCod ? metrics.SettledCodAmount : 0m,
            metrics.Currency,
            metrics.CountByStatus));
    }

    private static decimal Rate(int count, int total)
    {
        return total == 0 ? 0m : decimal.Round((decimal)count / total * 100m, 2);
    }

    private static Result ValidateDateRange(DateTimeOffset? fromUtc, DateTimeOffset? toUtc)
    {
        return fromUtc.HasValue && toUtc.HasValue && toUtc.Value < fromUtc.Value
            ? Result.Failure(ApplicationErrors.ValidationFailed("To date must be greater than or equal to from date."))
            : Result.Success();
    }
}
