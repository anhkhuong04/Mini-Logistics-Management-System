using MiniLogistics.Application.Shipments.GetPendingPickupShipments;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Application.Shippers.GetActiveShippers;

namespace MiniLogistics.Web.Components.Pages;

public sealed record OperationsPendingAssignmentInsight(
    Guid ShipmentId,
    IReadOnlySet<Guid> MatchedShipperIds,
    string FallbackReason)
{
    public bool HasMatchedShippers => MatchedShipperIds.Count > 0;

    public bool Matches(Guid shipperId)
    {
        return MatchedShipperIds.Contains(shipperId);
    }
}

public sealed record OperationsShipperOption(
    GetActiveShipperResponse Shipper,
    bool MatchesPickupArea,
    int ActiveShipmentCount);

public static class OperationsAssignmentUiModels
{
    public static IReadOnlyDictionary<Guid, OperationsPendingAssignmentInsight> BuildPendingInsights(
        IReadOnlyList<GetPendingPickupShipmentResponse> pendingShipments,
        IReadOnlyList<GetActiveShipperResponse> activeShippers)
    {
        return pendingShipments.ToDictionary(
            shipment => shipment.ShipmentId,
            shipment => BuildPendingInsight(shipment, activeShippers));
    }

    public static IReadOnlyList<OperationsShipperOption> BuildShipperOptions(
        GetPendingPickupShipmentResponse shipment,
        OperationsPendingAssignmentInsight insight,
        IReadOnlyList<GetActiveShipperResponse> activeShippers,
        Func<Guid, int> getActiveShipmentCount)
    {
        return activeShippers
            .Select(shipper => new OperationsShipperOption(
                shipper,
                insight.Matches(shipper.UserId),
                getActiveShipmentCount(shipper.UserId)))
            .OrderByDescending(option => option.MatchesPickupArea)
            .ThenBy(option => option.ActiveShipmentCount)
            .ThenBy(option => option.Shipper.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Shipper.UserId)
            .ToList();
    }

    private static OperationsPendingAssignmentInsight BuildPendingInsight(
        GetPendingPickupShipmentResponse shipment,
        IReadOnlyList<GetActiveShipperResponse> activeShippers)
    {
        var matchedShipperIds = activeShippers
            .Where(shipper => MatchesPickupProvince(shipper, shipment.PickupProvince))
            .Select(shipper => shipper.UserId)
            .ToHashSet();

        return new OperationsPendingAssignmentInsight(
            shipment.ShipmentId,
            matchedShipperIds,
            BuildFallbackReason(shipment, activeShippers.Count, matchedShipperIds.Count));
    }

    private static bool MatchesPickupProvince(
        GetActiveShipperResponse shipper,
        string pickupProvince)
    {
        var pickupProvinceKey = LocationNameNormalizer.NormalizeProvince(pickupProvince);

        return shipper.WorkingAreas.Any(area =>
            area.IsActive
            && LocationNameNormalizer.NormalizeProvince(area.Province) == pickupProvinceKey);
    }

    private static string BuildFallbackReason(
        GetPendingPickupShipmentResponse shipment,
        int activeShipperCount,
        int matchedShipperCount)
    {
        if (activeShipperCount == 0)
        {
            return "Auto assign fallback: chưa có shipper active.";
        }

        if (matchedShipperCount == 0)
        {
            return $"Auto assign fallback: chưa có shipper nhận pickup tại {shipment.PickupProvince}.";
        }

        return $"Đang chờ dù có {matchedShipperCount:N0} shipper phù hợp; có thể retry auto assign.";
    }
}
