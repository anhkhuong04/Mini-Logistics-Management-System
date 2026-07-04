using MiniLogistics.Application.Shipments.GetPendingPickupShipments;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Application.Shippers.GetActiveShippers;
using MiniLogistics.Web.Components.Pages;
using Xunit;

namespace MiniLogistics.Web.Tests;

public sealed class OperationsAssignmentUiModelsTests
{
    [Fact]
    public void BuildPendingInsights_WhenProvinceMatchesAfterNormalization_MarksShipperAsMatched()
    {
        var shipment = CreateShipment("Thành phố Hồ Chí Minh");
        var matchedShipper = CreateShipper(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "B Shipper",
            "Hồ Chí Minh");
        var overrideShipper = CreateShipper(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "A Shipper",
            "Hà Nội");

        var insights = OperationsAssignmentUiModels.BuildPendingInsights(
            [shipment],
            [overrideShipper, matchedShipper]);

        var insight = insights[shipment.ShipmentId];

        Assert.True(insight.Matches(matchedShipper.UserId));
        Assert.False(insight.Matches(overrideShipper.UserId));
        Assert.True(insight.HasMatchedShippers);
    }

    [Fact]
    public void BuildShipperOptions_WhenMixedCandidates_OrdersMatchedShippersBeforeManualOverride()
    {
        var shipment = CreateShipment("Hồ Chí Minh");
        var matchedHighLoad = CreateShipper(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "B Shipper",
            "Hồ Chí Minh");
        var matchedLowLoad = CreateShipper(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "A Shipper",
            "Hồ Chí Minh");
        var overrideLowLoad = CreateShipper(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            "C Shipper",
            "Hà Nội");
        var activeLoadByShipperId = new Dictionary<Guid, int>
        {
            [matchedHighLoad.UserId] = 3,
            [matchedLowLoad.UserId] = 1,
            [overrideLowLoad.UserId] = 0
        };
        var insight = OperationsAssignmentUiModels.BuildPendingInsights(
            [shipment],
            [overrideLowLoad, matchedHighLoad, matchedLowLoad])[shipment.ShipmentId];

        var options = OperationsAssignmentUiModels.BuildShipperOptions(
            shipment,
            insight,
            [overrideLowLoad, matchedHighLoad, matchedLowLoad],
            shipperId => activeLoadByShipperId.TryGetValue(shipperId, out var load) ? load : 0);

        Assert.Equal(
            [matchedLowLoad.UserId, matchedHighLoad.UserId, overrideLowLoad.UserId],
            options.Select(option => option.Shipper.UserId));
        Assert.True(options[0].MatchesPickupArea);
        Assert.False(options[^1].MatchesPickupArea);
    }

    private static GetPendingPickupShipmentResponse CreateShipment(string pickupProvince)
    {
        return new GetPendingPickupShipmentResponse(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "ML00000001",
            "Receiver",
            pickupProvince,
            "Hà Nội",
            100_000,
            30_000,
            "VND",
            DateTimeOffset.Parse("2026-07-04T00:00:00Z"));
    }

    private static GetActiveShipperResponse CreateShipper(
        Guid shipperId,
        string fullName,
        string province)
    {
        return new GetActiveShipperResponse(
            shipperId,
            fullName,
            $"{fullName.Replace(" ", ".", StringComparison.OrdinalIgnoreCase).ToLowerInvariant()}@example.test",
            "0900000000",
            [CreateArea(shipperId, province)]);
    }

    private static ShipperWorkingAreaResponse CreateArea(Guid shipperId, string province)
    {
        return new ShipperWorkingAreaResponse(
            Guid.NewGuid(),
            shipperId,
            Guid.NewGuid(),
            $"SPX-{province[..2].ToUpperInvariant()}",
            $"{province} Hub",
            province,
            null,
            null,
            IsActive: true);
    }
}
