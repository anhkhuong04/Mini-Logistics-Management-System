using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Web.Services;

public sealed record DashboardStatusGroup(string Label, string Tone, int Count, decimal Percentage);

public static class DashboardPresentation
{
    public static DateTimeOffset? NormalizeUtcQuery(DateTime? value)
    {
        if (value is null) return null;
        var date = value.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : value.Value;
        return new DateTimeOffset(date).ToUniversalTime();
    }

    public static IReadOnlyList<DashboardStatusGroup> GroupStatuses(IReadOnlyDictionary<ShipmentStatus, int> counts)
    {
        var groups = new (string Label, string Tone, ShipmentStatus[] Statuses)[]
        {
            ("Chờ lấy hàng", "warning", [ShipmentStatus.PendingPickup]),
            ("Đang xử lý", "primary", [ShipmentStatus.Assigned, ShipmentStatus.PickingUp, ShipmentStatus.PickedUp,
                ShipmentStatus.InTransit, ShipmentStatus.Delivering]),
            ("Đã giao", "success", [ShipmentStatus.Delivered]),
            ("Giao thất bại", "danger", [ShipmentStatus.DeliveryFailed]),
            ("Đã hoàn", "warning", [ShipmentStatus.Returned]),
            ("Đã hủy", "muted", [ShipmentStatus.Cancelled]),
            ("Bản nháp", "muted", [ShipmentStatus.Draft])
        };
        var total = counts.Values.Sum();
        return groups.Select(group =>
            {
                var count = group.Statuses.Sum(status => counts.GetValueOrDefault(status));
                return new DashboardStatusGroup(group.Label, group.Tone, count,
                    total == 0 ? 0m : (decimal)count / total * 100m);
            })
            .Where(group => group.Count > 0)
            .ToArray();
    }

    public static string? DateRangeError(string from, string to)
    {
        var hasFrom = !string.IsNullOrWhiteSpace(from);
        var hasTo = !string.IsNullOrWhiteSpace(to);
        var validFrom = DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start);
        var validTo = DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end);
        if (hasFrom && !validFrom || hasTo && !validTo)
        {
            return "Vui lòng chọn ngày hợp lệ.";
        }

        return hasFrom && hasTo && end < start ? "Ngày kết thúc phải bằng hoặc sau ngày bắt đầu." : null;
    }

    public static string ShipmentListUrl(Guid shopId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc,
        ShipmentStatus? status = null) => QueryHelpers.AddQueryString("/shipments", new Dictionary<string, string?>
        {
            ["shopId"] = shopId == Guid.Empty ? null : shopId.ToString("D"),
            ["fromUtc"] = fromUtc?.ToString("O", CultureInfo.InvariantCulture),
            ["toUtc"] = toUtc?.ToString("O", CultureInfo.InvariantCulture),
            ["status"] = status?.ToString()
        });
}
