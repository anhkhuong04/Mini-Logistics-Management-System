using Microsoft.AspNetCore.WebUtilities;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Web.Services;
using Xunit;

namespace MiniLogistics.Web.Tests;

public sealed class DashboardPresentationTests
{
    [Fact]
    public void QueryDates_PreserveUtcInstantAfterFrameworkConvertsToLocalTime()
    {
        var utc = new DateTime(2026, 10, 7, 23, 59, 59, DateTimeKind.Utc);
        Assert.Equal(new DateTimeOffset(utc), DashboardPresentation.NormalizeUtcQuery(utc.ToLocalTime()));
        Assert.Equal(new DateTimeOffset(utc), DashboardPresentation.NormalizeUtcQuery(DateTime.SpecifyKind(utc, DateTimeKind.Unspecified)));
        Assert.Null(DashboardPresentation.NormalizeUtcQuery(null));
    }

    [Fact]
    public void Distribution_CoversEveryShipmentStateWithoutLosingOrDuplicatingOrders()
    {
        var counts = Enum.GetValues<ShipmentStatus>().ToDictionary(status => status, _ => 1);
        var groups = DashboardPresentation.GroupStatuses(counts);

        Assert.Equal(counts.Count, groups.Sum(group => group.Count));
        Assert.InRange(groups.Sum(group => group.Percentage), 99.999m, 100.001m);
        Assert.Equal(5, groups.Single(group => group.Label == "Đang xử lý").Count);
    }

    [Fact]
    public void Distribution_UsesRealProportionsAndOmitsZeroStates()
    {
        var groups = DashboardPresentation.GroupStatuses(new Dictionary<ShipmentStatus, int>
        {
            [ShipmentStatus.PendingPickup] = 1,
            [ShipmentStatus.Delivered] = 3,
            [ShipmentStatus.DeliveryFailed] = 0
        });

        Assert.Equal(2, groups.Count);
        Assert.Equal(25m, groups.Single(group => group.Label == "Chờ lấy hàng").Percentage);
        Assert.Equal(75m, groups.Single(group => group.Label == "Đã giao").Percentage);
        Assert.Empty(DashboardPresentation.GroupStatuses(new Dictionary<ShipmentStatus, int>()));
    }

    [Theory]
    [InlineData("2026-10-15", "2026-10-14")]
    [InlineData("2026-02-30", "2026-10-15")]
    [InlineData("invalid", "")]
    public void DateRange_InvalidInputsReceiveActionableFeedback(string from, string to)
    {
        Assert.NotNull(DashboardPresentation.DateRangeError(from, to));
    }

    [Theory]
    [InlineData("2026-10-15", "2026-10-15")]
    [InlineData("", "2026-10-15")]
    [InlineData("2026-10-15", "")]
    [InlineData("", "")]
    public void DateRange_SameDayAndOpenEndedRangesAreAllowed(string from, string to)
    {
        Assert.Null(DashboardPresentation.DateRangeError(from, to));
    }

    [Fact]
    public void ShipmentDrillDown_PreservesShopStatusAndExactUtcRange()
    {
        var shopId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(7).AddTicks(-1);
        var url = DashboardPresentation.ShipmentListUrl(shopId, from, to, ShipmentStatus.DeliveryFailed);
        var query = QueryHelpers.ParseQuery(new Uri("http://localhost" + url).Query);

        Assert.StartsWith("/shipments?", url);
        Assert.Equal(shopId.ToString("D"), query["shopId"]);
        Assert.Equal("DeliveryFailed", query["status"]);
        Assert.Equal(from, DateTimeOffset.Parse(query["fromUtc"]!));
        Assert.Equal(to, DateTimeOffset.Parse(query["toUtc"]!));
        Assert.Equal("/shipments", DashboardPresentation.ShipmentListUrl(Guid.Empty, null, null));
    }
}
