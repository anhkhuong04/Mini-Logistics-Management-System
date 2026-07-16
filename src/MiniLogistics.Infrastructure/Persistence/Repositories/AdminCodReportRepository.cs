using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.AdminCod;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class AdminCodReportRepository : IAdminCodReportRepository
{
    private readonly MiniLogisticsDbContext _dbContext;
    private readonly IIdentityService _identityService;

    public AdminCodReportRepository(
        MiniLogisticsDbContext dbContext,
        IIdentityService identityService)
    {
        _dbContext = dbContext;
        _identityService = identityService;
    }

    public async Task<AdminCodReportResponse> GetAsync(
        AdminCodReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var rowsQuery =
            from cod in _dbContext.CodTransactions.AsNoTracking()
            join shipment in _dbContext.Shipments.AsNoTracking()
                on cod.ShipmentId equals shipment.Id
            join assignment in _dbContext.ShipmentAssignments
                    .AsNoTracking()
                    .Where(assignment => assignment.UnassignedAtUtc == null)
                on shipment.Id equals assignment.ShipmentId into assignmentGroup
            from activeAssignment in assignmentGroup.DefaultIfEmpty()
            where shipment.Status != ShipmentStatus.Draft
            select new AdminCodReportRow(
                shipment.Id,
                shipment.TrackingCode,
                shipment.ReceiverName,
                shipment.ReceiverPhone,
                shipment.PickupAddress.Province,
                activeAssignment == null ? null : activeAssignment.ShipperId,
                cod.Amount,
                "VND",
                cod.Status,
                shipment.Status,
                shipment.CreatedAtUtc,
                cod.CollectedAtUtc,
                cod.CollectedByUserId,
                cod.SettledAtUtc,
                cod.SettledByUserId);

        if (query.Status.HasValue)
        {
            rowsQuery = query.Status.Value == CodStatus.PendingCollection
                ? rowsQuery.Where(row => row.CodStatus == CodStatus.PendingCollection
                    && row.ShipmentStatus == ShipmentStatus.Delivered)
                : rowsQuery.Where(row => row.CodStatus == query.Status.Value);
        }
        else
        {
            rowsQuery = rowsQuery.Where(row =>
                (row.CodStatus == CodStatus.PendingCollection && row.ShipmentStatus == ShipmentStatus.Delivered)
                || row.CodStatus == CodStatus.Collected
                || row.CodStatus == CodStatus.Settled);
        }

        if (!string.IsNullOrWhiteSpace(query.Province))
        {
            var province = query.Province.Trim();
            rowsQuery = rowsQuery.Where(row => row.Province == province);
        }

        if (query.FromUtc.HasValue)
        {
            rowsQuery = rowsQuery.Where(row =>
                (row.CodStatus == CodStatus.Settled
                    ? row.SettledAtUtc ?? row.CreatedAtUtc
                    : row.CodStatus == CodStatus.Collected
                        ? row.CollectedAtUtc ?? row.CreatedAtUtc
                        : row.CreatedAtUtc) >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            rowsQuery = rowsQuery.Where(row =>
                (row.CodStatus == CodStatus.Settled
                    ? row.SettledAtUtc ?? row.CreatedAtUtc
                    : row.CodStatus == CodStatus.Collected
                        ? row.CollectedAtUtc ?? row.CreatedAtUtc
                        : row.CreatedAtUtc) <= query.ToUtc.Value);
        }

        var rows = await rowsQuery.ToListAsync(cancellationToken);

        rows = rows
            .Where(row => !query.MinAmount.HasValue || row.Amount.Amount >= query.MinAmount.Value)
            .Where(row => !query.MaxAmount.HasValue || row.Amount.Amount <= query.MaxAmount.Value)
            .Where(row => MatchesShipper(row, query.ShipperId))
            .OrderByDescending(GetReferenceDate)
            .Take(500)
            .ToList();

        var shipperIds = rows
            .Select(row => GetEffectiveShipperId(row))
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var shippers = await _identityService.GetUsersByIdsAsync(shipperIds, cancellationToken);
        var shipperNameById = shippers.ToDictionary(shipper => shipper.UserId, shipper => shipper.FullName);

        var items = rows
            .Select(row =>
            {
                var shipperId = GetEffectiveShipperId(row);
                return new AdminCodTransactionResponse(
                    row.ShipmentId,
                    row.TrackingCode.Value,
                    row.ReceiverName,
                    row.ReceiverPhone.Value,
                    row.Province,
                    shipperId,
                    shipperId.HasValue && shipperNameById.TryGetValue(shipperId.Value, out var shipperName)
                        ? shipperName
                        : null,
                    row.Amount.Amount,
                    row.Currency,
                    row.CodStatus,
                    row.ShipmentStatus,
                    row.CreatedAtUtc,
                    row.CollectedAtUtc,
                    row.CollectedByUserId,
                    row.SettledAtUtc,
                    row.SettledByUserId);
            })
            .ToList();

        return new AdminCodReportResponse(
            BuildSummary(items),
            items,
            BuildGroupSummaries(items, item => item.ShipperName ?? FormatShortId(item.ShipperId)),
            BuildGroupSummaries(items, item => item.Province),
            BuildDailySummaries(items));
    }

    private static string FormatShortId(Guid? id)
    {
        return id.HasValue
            ? id.Value.ToString("N")[..8]
            : "Unassigned";
    }

    private static DateTimeOffset GetReferenceDate(AdminCodReportRow row)
    {
        return row.CodStatus switch
        {
            CodStatus.Settled => row.SettledAtUtc ?? row.CreatedAtUtc,
            CodStatus.Collected => row.CollectedAtUtc ?? row.CreatedAtUtc,
            _ => row.CreatedAtUtc
        };
    }

    private static bool MatchesShipper(AdminCodReportRow row, Guid? shipperId)
    {
        if (!shipperId.HasValue)
        {
            return true;
        }

        return GetEffectiveShipperId(row) == shipperId.Value;
    }

    private static Guid? GetEffectiveShipperId(AdminCodReportRow row)
    {
        return row.CodStatus == CodStatus.PendingCollection
            ? row.ActiveShipperId
            : row.CollectedByUserId ?? row.ActiveShipperId;
    }

    private static AdminCodSummary BuildSummary(IReadOnlyCollection<AdminCodTransactionResponse> items)
    {
        var pending = items.Where(item => item.CodStatus == CodStatus.PendingCollection).ToList();
        var collected = items.Where(item => item.CodStatus == CodStatus.Collected).ToList();
        var settled = items.Where(item => item.CodStatus == CodStatus.Settled).ToList();
        var currency = items.FirstOrDefault()?.Currency ?? "VND";

        return new AdminCodSummary(
            pending.Count,
            pending.Sum(item => item.Amount),
            collected.Count,
            collected.Sum(item => item.Amount),
            settled.Count,
            settled.Sum(item => item.Amount),
            currency);
    }

    private static IReadOnlyList<AdminCodGroupSummary> BuildGroupSummaries(
        IReadOnlyCollection<AdminCodTransactionResponse> items,
        Func<AdminCodTransactionResponse, string> keySelector)
    {
        return items
            .GroupBy(keySelector)
            .Select(group =>
            {
                var shipmentCount = group.Count();
                var pending = group
                    .Where(item => item.CodStatus == CodStatus.PendingCollection)
                    .Sum(item => item.Amount);
                var collected = group
                    .Where(item => item.CodStatus == CodStatus.Collected)
                    .Sum(item => item.Amount);
                var settled = group
                    .Where(item => item.CodStatus == CodStatus.Settled)
                    .Sum(item => item.Amount);

                return new AdminCodGroupSummary(
                    group.Key,
                    shipmentCount,
                    pending,
                    collected,
                    settled,
                    shipmentCount == 0 ? 0 : Math.Round((decimal)group.Count(item => item.CodStatus != CodStatus.PendingCollection) / shipmentCount * 100, 2));
            })
            .OrderByDescending(group => group.PendingAmount + group.CollectedAmount + group.SettledAmount)
            .ToList();
    }

    private static IReadOnlyList<AdminCodDailySummary> BuildDailySummaries(
        IReadOnlyCollection<AdminCodTransactionResponse> items)
    {
        return items
            .GroupBy(item => DateOnly.FromDateTime((item.SettledAtUtc ?? item.CollectedAtUtc ?? item.CreatedAtUtc).LocalDateTime))
            .Select(group => new AdminCodDailySummary(
                group.Key,
                group.Count(),
                group.Where(item => item.CodStatus == CodStatus.PendingCollection).Sum(item => item.Amount),
                group.Where(item => item.CodStatus == CodStatus.Collected).Sum(item => item.Amount),
                group.Where(item => item.CodStatus == CodStatus.Settled).Sum(item => item.Amount)))
            .OrderByDescending(summary => summary.Day)
            .ToList();
    }

    private sealed record AdminCodReportRow(
        Guid ShipmentId,
        TrackingCode TrackingCode,
        string ReceiverName,
        PhoneNumber ReceiverPhone,
        string Province,
        Guid? ActiveShipperId,
        Money Amount,
        string Currency,
        CodStatus CodStatus,
        ShipmentStatus ShipmentStatus,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? CollectedAtUtc,
        Guid? CollectedByUserId,
        DateTimeOffset? SettledAtUtc,
        Guid? SettledByUserId);
}
