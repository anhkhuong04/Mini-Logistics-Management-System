using System.Globalization;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Web.Services;

public static class UiDisplay
{
    public static string FormatLocalDateTime(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
    }

    public static string ToErrorMessage(Error error)
    {
        return error.Code switch
        {
            "Application.ValidationFailed" => "Dữ liệu chưa hợp lệ. Vui lòng kiểm tra các trường bắt buộc và thử lại.",
            "Application.Conflict" => "Dữ liệu đã tồn tại hoặc bị trùng. Vui lòng kiểm tra lại trước khi tiếp tục.",
            "Application.NotFound" => "Không tìm thấy dữ liệu phù hợp. Vui lòng làm mới trang hoặc kiểm tra lại thông tin.",
            "Application.Forbidden" => "Tài khoản hiện không có quyền thực hiện thao tác này hoặc chưa được kích hoạt.",
            "Shipment.CannotAssign" => "Chỉ đơn đang chờ lấy hàng mới có thể phân công shipper.",
            "Shipment.ActiveAssignmentExists" => "Đơn hàng đã có shipper đang được phân công.",
            "Shipment.InvalidShipper" => "Shipper được chọn không hợp lệ. Vui lòng chọn lại shipper active.",
            "Shipment.InvalidStatusTransition" => "Không thể chuyển đơn sang trạng thái này từ trạng thái hiện tại.",
            "Shipment.DeliveryFailedNoteRequired" => "Vui lòng ghi rõ lý do khi cập nhật giao thất bại.",
            "Shipment.CompletedShipmentCannotChange" => "Đơn đã hoàn tất nên không thể cập nhật trạng thái.",
            "Shipment.CannotCancel" => "Không thể hủy đơn ở trạng thái hiện tại.",
            "COD.CollectionNotRequired" => "Đơn này không yêu cầu thu COD.",
            "COD.ShipmentMustBeDelivered" => "Chỉ có thể xác nhận thu COD sau khi đơn đã giao thành công.",
            "COD.CannotCollect" => "COD không còn ở trạng thái chờ thu.",
            "COD.CannotSettle" => "Chỉ COD đã thu mới có thể đối soát.",
            "FeeRule.NoMatchingRule" => "Chưa có bảng phí active phù hợp với tuyến và cân tính phí của đơn.",
            "RouteClassification.ProvinceNotSupported" => $"Tỉnh/thành chưa được hỗ trợ để phân loại tuyến: {ExtractValueAfterColon(error.Description)}.",
            "Shop.Inactive" => "Shop đang bị khóa hoặc chưa active.",
            _ => ToErrorMessage(error.Description)
        };
    }

    public static string ToErrorMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var normalized = message.Trim();
        return normalized switch
        {
            "Email hoặc mật khẩu không đúng." => "Email hoặc mật khẩu không đúng.",
            "Tài khoản đã được tạo. Vui lòng đăng nhập." => "Tài khoản đã được tạo. Vui lòng đăng nhập.",
            "Email or password is incorrect." => "Email hoặc mật khẩu không đúng.",
            "Account was created. Please sign in." => "Tài khoản đã được tạo. Vui lòng đăng nhập.",
            "Account is inactive." => "Tai khoan dang bi khoa hoac chua active.",
            "Shop already exists for this user." => "Tài khoản này đã có shop.",
            "Shop account is not active." => "Shop đang bị khóa hoặc chưa active.",
            "Shop was not found for current user." => "Không tìm thấy shop cho tài khoản đang đăng nhập.",
            "Shipment was not found for current shop." => "Không tìm thấy đơn hàng thuộc shop hiện tại.",
            _ when normalized.Contains("Password", StringComparison.OrdinalIgnoreCase)
                => "Mật khẩu chưa hợp lệ. Vui lòng dùng mật khẩu đủ mạnh và thử lại.",
            _ when normalized.Contains("Email", StringComparison.OrdinalIgnoreCase)
                => "Email chưa hợp lệ hoặc đã được sử dụng.",
            _ when normalized.Contains("Phone", StringComparison.OrdinalIgnoreCase)
                => "Số điện thoại chưa hợp lệ.",
            _ => normalized
        };
    }

    private static string ExtractValueAfterColon(string value)
    {
        var colonIndex = value.LastIndexOf(':');
        return colonIndex >= 0 && colonIndex + 1 < value.Length
            ? value[(colonIndex + 1)..].Trim()
            : value;
    }
}
