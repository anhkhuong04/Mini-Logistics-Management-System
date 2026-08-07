using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments;

public sealed record PublicShipmentStatusMessage(
    string MessageCode,
    string Message,
    string Locale);

public static class ShipmentPublicStatusMessageMapper
{
    public const string DefaultLocale = "vi-VN";

    public static PublicShipmentStatusMessage FromStatus(ShipmentStatus status)
    {
        return status switch
        {
            ShipmentStatus.Draft => Create("SHIPMENT_DRAFT", "Vận đơn đã được tạo nháp."),
            ShipmentStatus.PendingPickup => Create("SHIPMENT_PENDING_PICKUP", "Vận đơn đang chờ lấy hàng."),
            ShipmentStatus.Assigned => Create("SHIPMENT_ASSIGNED", "Vận đơn đã được phân công giao nhận."),
            ShipmentStatus.PickingUp => Create("SHIPMENT_PICKING_UP", "Nhân viên đang đến lấy hàng."),
            ShipmentStatus.PickedUp => Create("SHIPMENT_PICKED_UP", "Vận đơn đã được lấy hàng."),
            ShipmentStatus.InTransit => Create("SHIPMENT_IN_TRANSIT", "Vận đơn đang được vận chuyển."),
            ShipmentStatus.Delivering => Create("SHIPMENT_DELIVERING", "Vận đơn đang được giao đến người nhận."),
            ShipmentStatus.Delivered => Create("SHIPMENT_DELIVERED", "Vận đơn đã được giao thành công."),
            ShipmentStatus.DeliveryFailed => Create("SHIPMENT_DELIVERY_FAILED", "Lần giao hàng chưa thành công."),
            ShipmentStatus.Returned => Create("SHIPMENT_RETURNED", "Vận đơn đã được hoàn trả."),
            ShipmentStatus.Cancelled => Create("SHIPMENT_CANCELLED", "Vận đơn đã được hủy."),
            _ => Create("SHIPMENT_STATUS_UPDATED", "Trạng thái vận đơn đã được cập nhật.")
        };
    }

    public static PublicShipmentStatusMessage FromHistory(
        ShipmentStatus status,
        FailureReasonCode? failureReasonCode)
    {
        if (status != ShipmentStatus.DeliveryFailed || failureReasonCode is null)
        {
            return FromStatus(status);
        }

        return failureReasonCode switch
        {
            FailureReasonCode.ReceiverUnavailable => Create(
                "SHIPMENT_DELIVERY_FAILED_RECEIVER_UNAVAILABLE",
                "Chưa thể giao hàng vì người nhận chưa sẵn sàng nhận hàng."),
            FailureReasonCode.WrongAddress => Create(
                "SHIPMENT_DELIVERY_FAILED_WRONG_ADDRESS",
                "Chưa thể giao hàng do cần xác minh lại địa chỉ nhận."),
            FailureReasonCode.ReceiverRejected => Create(
                "SHIPMENT_DELIVERY_FAILED_RECEIVER_REJECTED",
                "Người nhận đã từ chối nhận vận đơn."),
            FailureReasonCode.CannotContactReceiver => Create(
                "SHIPMENT_DELIVERY_FAILED_CANNOT_CONTACT_RECEIVER",
                "Chưa thể liên hệ người nhận để giao hàng."),
            FailureReasonCode.DamagedParcel => Create(
                "SHIPMENT_DELIVERY_FAILED_DAMAGED_PARCEL",
                "Vận đơn cần được kiểm tra trước khi tiếp tục giao."),
            FailureReasonCode.PaymentIssue => Create(
                "SHIPMENT_DELIVERY_FAILED_PAYMENT_ISSUE",
                "Chưa thể hoàn tất giao hàng do vấn đề thanh toán."),
            FailureReasonCode.WeatherOrAccessIssue => Create(
                "SHIPMENT_DELIVERY_FAILED_WEATHER_OR_ACCESS",
                "Chưa thể giao hàng do điều kiện thời tiết hoặc tiếp cận."),
            _ => FromStatus(status)
        };
    }

    private static PublicShipmentStatusMessage Create(string code, string message)
    {
        return new PublicShipmentStatusMessage(code, message, DefaultLocale);
    }
}
