using MiniLogistics.Application.CashOnDelivery.GetCodSettlementCandidates;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shipments.GetOperationsShipments;
using MiniLogistics.Application.Shipments.GetPendingPickupShipments;
using MiniLogistics.Application.Shippers.GetActiveShippers;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Web.Services;

namespace MiniLogistics.Web.Components.Pages.Operations;

public static class OperationsDisplay
{
    public static bool CanManageAssignment(GetOperationsShipmentResponse shipment)
    {
        return shipment.Status == ShipmentStatus.Assigned;
    }

    public static bool CanMarkCodCollected(GetOperationsShipmentResponse shipment)
    {
        return shipment.Status == ShipmentStatus.Delivered
            && shipment.CodStatus == CodStatus.PendingCollection;
    }

    public static IReadOnlyList<ShipmentStatus> GetNextStatuses(ShipmentStatus status)
    {
        return status switch
        {
            ShipmentStatus.Assigned => [ShipmentStatus.PickingUp, ShipmentStatus.Cancelled],
            ShipmentStatus.PickingUp => [ShipmentStatus.PickedUp, ShipmentStatus.Cancelled],
            ShipmentStatus.PickedUp => [ShipmentStatus.InTransit, ShipmentStatus.Returned],
            ShipmentStatus.InTransit => [ShipmentStatus.Delivering, ShipmentStatus.Returned],
            ShipmentStatus.Delivering => [ShipmentStatus.Delivered, ShipmentStatus.DeliveryFailed, ShipmentStatus.Returned],
            ShipmentStatus.DeliveryFailed => [ShipmentStatus.Delivering, ShipmentStatus.Returned],
            _ => []
        };
    }

    public static int GetActiveShipmentCount(
        IEnumerable<GetOperationsShipmentResponse> shipments,
        Guid shipperId)
    {
        return shipments.Count(shipment =>
            shipment.ActiveShipperId == shipperId
            && ShipmentLoadStatuses.ActiveAssignmentStatuses.Contains(shipment.Status));
    }

    public static IReadOnlyList<GetActiveShipperResponse> GetReassignShipperOptions(
        GetOperationsShipmentResponse shipment,
        IEnumerable<GetActiveShipperResponse> activeShippers)
    {
        return activeShippers
            .Where(shipper => shipper.UserId != shipment.ActiveShipperId)
            .OrderByDescending(shipper => IsShipperAreaMatch(shipment, shipper))
            .ThenBy(shipper => shipper.FullName)
            .ToList();
    }

    public static bool IsShipperAreaMatch(
        GetOperationsShipmentResponse shipment,
        GetActiveShipperResponse shipper)
    {
        return shipper.WorkingAreas.Any(area =>
            area.IsActive
            && string.Equals(area.Province, shipment.PickupAddress.Province, StringComparison.OrdinalIgnoreCase));
    }

    public static string FormatAssignedShipper(GetOperationsShipmentResponse shipment)
    {
        if (!string.IsNullOrWhiteSpace(shipment.ActiveShipperName))
        {
            return shipment.ActiveShipperName;
        }

        return shipment.ActiveShipperId.HasValue
            ? shipment.ActiveShipperId.Value.ToString()[..8]
            : "Chưa rõ";
    }

    public static string FormatDate(DateTimeOffset value)
    {
        return UiDisplay.FormatLocalDateTime(value);
    }

    public static string FormatNullableDate(DateTimeOffset? value)
    {
        return value.HasValue
            ? UiDisplay.FormatLocalDateTime(value.Value)
            : "Chưa ghi nhận";
    }

    public static string FormatCollectedBy(GetCodSettlementCandidateResponse cod)
    {
        if (!string.IsNullOrWhiteSpace(cod.CollectedByName))
        {
            return cod.CollectedByName;
        }

        return cod.CollectedByUserId.HasValue
            ? cod.CollectedByUserId.Value.ToString()[..8]
            : "Chưa rõ";
    }

    public static string FormatMoney(decimal amount, string currency)
    {
        return $"{amount:N0} {currency}";
    }

    public static string FormatShipperOption(OperationsShipperOption option)
    {
        var matchLabel = option.MatchesPickupArea ? "Phù hợp" : "Override";
        var availabilityLabel = option.Shipper.IsAvailableForAssignment
            ? "nhận auto"
            : "tạm ngưng";
        var capacityLabel = option.IsAtCapacity
            ? $"đầy {option.ActiveShipmentCount:N0}/{option.Shipper.MaxActiveShipments:N0}"
            : $"tải {option.ActiveShipmentCount:N0}/{option.Shipper.MaxActiveShipments:N0}";

        return $"{FormatShipper(option.Shipper)} | {matchLabel} | {availabilityLabel} | {FormatWorkingAreas(option.Shipper)} | {capacityLabel}";
    }

    public static string FormatReassignShipperOption(
        GetActiveShipperResponse shipper,
        bool isAreaMatch,
        int activeShipmentCount)
    {
        var matchLabel = isAreaMatch ? "Phù hợp" : "Override";
        var capacityLabel = activeShipmentCount >= shipper.MaxActiveShipments
            ? $"đầy {activeShipmentCount:N0}/{shipper.MaxActiveShipments:N0}"
            : $"tải {activeShipmentCount:N0}/{shipper.MaxActiveShipments:N0}";

        return $"{FormatShipper(shipper)} | {matchLabel} | {FormatWorkingAreas(shipper)} | {capacityLabel}";
    }

    public static string FormatShipper(GetActiveShipperResponse shipper)
    {
        return string.IsNullOrWhiteSpace(shipper.PhoneNumber)
            ? $"{shipper.FullName} - {shipper.Email}"
            : $"{shipper.FullName} - {shipper.PhoneNumber}";
    }

    public static string FormatWorkingAreas(GetActiveShipperResponse shipper)
    {
        var activeAreas = shipper.WorkingAreas
            .Where(area => area.IsActive)
            .ToList();

        if (activeAreas.Count == 0)
        {
            return "Chưa gán khu vực";
        }

        var displayedAreas = activeAreas
            .Select(area => string.IsNullOrWhiteSpace(area.HubCode)
                ? area.Province
                : $"{area.HubCode}/{area.Province}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();
        var hiddenAreaCount = activeAreas.Count - displayedAreas.Count;
        var suffix = hiddenAreaCount > 0 ? $" +{hiddenAreaCount:N0}" : string.Empty;

        return $"{string.Join(", ", displayedAreas)}{suffix}";
    }

    public static string FormatDefaultShipperMeta(
        GetPendingPickupShipmentResponse shipment,
        int matchedShipperCount)
    {
        if (matchedShipperCount > 0)
        {
            return $"Ưu tiên {matchedShipperCount:N0} shipper match pickup {shipment.PickupProvince}.";
        }

        return $"Chưa có shipper match pickup {shipment.PickupProvince}; dùng manual override nếu cần.";
    }

    public static string FormatSelectedShipperMeta(
        GetPendingPickupShipmentResponse shipment,
        GetActiveShipperResponse shipper,
        bool isAreaMatch,
        int activeLoad)
    {
        var workingAreas = FormatWorkingAreas(shipper);
        var availability = shipper.IsAvailableForAssignment ? "đang nhận auto" : "tạm ngưng auto";
        var capacity = $"{activeLoad:N0}/{shipper.MaxActiveShipments:N0}";

        return isAreaMatch
            ? $"Match pickup {shipment.PickupProvince}; {availability}; tải {capacity}; {workingAreas}."
            : $"Manual override ngoài pickup {shipment.PickupProvince}; {availability}; tải {capacity}; {workingAreas}.";
    }

    public static string FormatStatus(ShipmentStatus status)
    {
        return status switch
        {
            ShipmentStatus.PendingPickup => "Chờ lấy hàng",
            ShipmentStatus.Assigned => "Đã phân công",
            ShipmentStatus.PickingUp => "Đang lấy hàng",
            ShipmentStatus.PickedUp => "Đã lấy hàng",
            ShipmentStatus.InTransit => "Đang vận chuyển",
            ShipmentStatus.Delivering => "Đang giao",
            ShipmentStatus.Delivered => "Đã giao",
            ShipmentStatus.DeliveryFailed => "Giao thất bại",
            ShipmentStatus.Returned => "Đã hoàn",
            ShipmentStatus.Cancelled => "Đã hủy",
            _ => status.ToString()
        };
    }

    public static string GetStatusClass(ShipmentStatus status)
    {
        return status switch
        {
            ShipmentStatus.Delivered => "success",
            ShipmentStatus.Cancelled or ShipmentStatus.DeliveryFailed or ShipmentStatus.Returned => "danger",
            ShipmentStatus.InTransit or ShipmentStatus.Delivering or ShipmentStatus.PickedUp => "info",
            _ => "warning"
        };
    }

    public static string FormatCodStatus(CodStatus status)
    {
        return status switch
        {
            CodStatus.NotRequired => "Không yêu cầu",
            CodStatus.PendingCollection => "Chờ thu",
            CodStatus.Collected => "Đã thu",
            CodStatus.Settled => "Đã đối soát",
            _ => status.ToString()
        };
    }

    public static string GetCodStatusClass(CodStatus status)
    {
        return status switch
        {
            CodStatus.PendingCollection => "warning",
            CodStatus.Collected or CodStatus.Settled => "success",
            _ => "neutral"
        };
    }
}
