import { useI18n } from './I18nContext';
export const statusLabels = {
  vi: { Draft: 'Bản nháp', PendingPickup: 'Chờ lấy hàng', Assigned: 'Đã phân công', PickingUp: 'Đang lấy hàng', PickedUp: 'Đã lấy hàng',
    InTransit: 'Đang vận chuyển', Delivering: 'Đang giao', Delivered: 'Đã giao', DeliveryFailed: 'Giao thất bại', Returned: 'Đã hoàn', Cancelled: 'Đã hủy' },
  en: { Draft: 'Draft', PendingPickup: 'Pending pickup', Assigned: 'Assigned', PickingUp: 'Picking up', PickedUp: 'Picked up',
    InTransit: 'In transit', Delivering: 'Delivering', Delivered: 'Delivered', DeliveryFailed: 'Delivery failed', Returned: 'Returned', Cancelled: 'Cancelled' },
};
const vi = { list: 'Vận đơn', detail: 'Chi tiết vận đơn', tracking: 'Mã vận đơn', receiver: 'Người nhận', status: 'Trạng thái', fee: 'Phí vận chuyển',
  created: 'Ngày tạo', search: 'Tìm kiếm', advanced: 'Bộ lọc nâng cao', reset: 'Xóa bộ lọc', apply: 'Áp dụng', refresh: 'Làm mới',
  invalid: 'Bộ lọc không hợp lệ. Vui lòng kiểm tra đường dẫn hoặc xóa bộ lọc.', denied: 'Không có quyền truy cập', missing: 'Không tìm thấy vận đơn',
  empty: 'Chưa có vận đơn', from: 'Từ ngày (UTC)', to: 'Đến ngày (UTC)', receiverPhone: 'Số điện thoại người nhận', minCod: 'COD tối thiểu', maxCod: 'COD tối đa',
  sort: 'Sắp xếp', direction: 'Thứ tự', descending: 'Giảm dần', ascending: 'Tăng dần', all: 'Tất cả', sender: 'Người gửi', pickup: 'Địa chỉ lấy hàng', delivery: 'Địa chỉ giao hàng',
  parcel: 'Kiện hàng và phí', weight: 'Khối lượng', chargeable: 'Khối lượng tính phí', dimensions: 'Kích thước', goods: 'Giá trị hàng',
  base: 'Phí cơ bản', extra: 'Phí vượt khối lượng', insurance: 'Phí bảo hiểm', returnFee: 'Phí hoàn', note: 'Ghi chú', history: 'Lịch sử trạng thái',
  back: 'Danh sách vận đơn', inactive: 'Shop đang tạm ngưng. Bạn vẫn có thể xem lịch sử.', retry: 'Thử lại',
};
const en: Record<keyof typeof vi, string> = { list: 'Shipments', detail: 'Shipment details', tracking: 'Tracking code', receiver: 'Receiver', status: 'Status', fee: 'Shipping fee',
  created: 'Created at', search: 'Search', advanced: 'Advanced filters', reset: 'Clear filters', apply: 'Apply', refresh: 'Refresh',
  invalid: 'Invalid filters. Check the URL or clear filters.', denied: 'Access denied', missing: 'Shipment not found', empty: 'No shipments yet',
  from: 'From date (UTC)', to: 'To date (UTC)', receiverPhone: 'Receiver phone', minCod: 'Minimum COD', maxCod: 'Maximum COD', sort: 'Sort by', direction: 'Direction',
  descending: 'Descending', ascending: 'Ascending', all: 'All', sender: 'Sender', pickup: 'Pickup address', delivery: 'Delivery address', parcel: 'Parcel and fees',
  weight: 'Weight', chargeable: 'Chargeable weight', dimensions: 'Dimensions', goods: 'Goods value', base: 'Base fee', extra: 'Extra weight fee', insurance: 'Insurance fee',
  returnFee: 'Return fee', note: 'Note', history: 'Status history', back: 'Shipment list', inactive: 'Shop is inactive. You can still view history.', retry: 'Try again' };
export function useShipmentCopy() { const { locale } = useI18n(); return { ... (locale === 'en-US' ? en : vi), labels: locale === 'en-US' ? statusLabels.en : statusLabels.vi }; }
