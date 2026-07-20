# PHÂN TÍCH TOÀN BỘ TÍNH NĂNG CẦN CÓ CỦA ROLE ADMIN
## Dự án Mini Logistics Management System

**Phiên bản phân tích:** 1.0  
**Ngày phân tích:** 16/07/2026  
**Mục đích:** Cung cấp tài liệu đầy đủ cho Agent Coding đọc, phân tích và triển khai  
**Nguồn tham chiếu:** admin.md, overview.md, task.md, operator.md, shipper.md, shop.md

---

## 1. MỤC TIÊU CỦA TÀI LIỆU

Tài liệu này liệt kê **toàn bộ tính năng** mà role `Admin` cần có trong dự án Logistics, bao gồm:

- Tính năng **đã triển khai** ở mức MVP/demo hiện tại
- Tính năng **cần bổ sung** để đạt chuẩn Production
- Business rules quan trọng
- Production considerations & rủi ro
- Use case / luồng chính

Phân loại theo **domain nghiệp vụ thực tế** của một nền tảng logistics (tương tự GHN, GHTK, J&T).

---

## 2. QUẢN LÝ NGƯỜI DÙNG NỘI BỘ (Internal User Management)

### 2.1 Tính năng hiện có

| STT | Tính năng | Trạng thái | Use case chính | Business Rule quan trọng | Production Note |
|-----|-----------|------------|----------------|---------------------------|-----------------|
| 2.1.1 | Tạo tài khoản Operator / Shipper | Đã có | Admin tạo user nội bộ qua `/admin/users` + `CreateInternalUserService` | - Chỉ active Admin được tạo<br>- Không được tạo Admin mới từ UI/service này<br>- User mới active mặc định | Cần ghi **Audit Log** (actor, thời gian, IP, user được tạo) |
| 2.1.2 | Xem danh sách User nội bộ + Working Area + Capacity | Đã có | `GetAdminUsersService` enrich `ShipperWorkingArea` + Hub metadata | Chỉ hiện user có role `Shipper` hoặc `Operator` | Cần thêm filter, pagination, export CSV/Excel |
| 2.1.3 | Active / Deactive User (Operator & Shipper) | Đã có | `SetUserActiveStatusService` | Deactive Shipper → tự động loại khỏi danh sách auto assignment và manual assign | Cần audit + lý do deactive + thông báo cho Operator |
| 2.1.4 | Cấu hình Capacity Shipper | Đã có | `SetShipperCapacity` (`IsAvailableForAssignment`, `MaxActiveShipments`) | Auto assignment chỉ chọn shipper: active + available + chưa vượt `MaxActiveShipments` | Cần lịch sử thay đổi capacity theo thời gian |
| 2.1.5 | Gán Working Area cho Shipper (Hub / Ward / ZoneCode) | Đã có | `SetShipperWorkingAreasService` | - Giới hạn tối đa 30 working areas / shipper<br>- Hub phải tồn tại và active<br>- Normalize whitespace + duplicate check | Cần **approval workflow** + audit log khi thay đổi area |
| 2.1.6 | Xem Working Area + Hub metadata của Shipper | Đã có | `GetShipperWorkingAreas` + `GetAdminUsers` | - | - |

### 2.2 Tính năng cần bổ sung (Production)

- Quản lý **Hub** (tạo/sửa/xóa/active/deactive) – hiện chỉ seed/demo
- Approval flow khi Admin gán/sửa Working Area cho Shipper
- Lịch sử thay đổi Capacity và Working Area
- Khóa tài khoản Admin khác (nếu cần) – hiện chỉ hỗ trợ Shipper/Operator

---

## 3. QUẢN LÝ SHOP & BUSINESS ACCOUNT

### 3.1 Tính năng cần triển khai (Task 1 - Shop Profile Management)

| STT | Tính năng | Trạng thái | Use case chính | Business Rule quan trọng | Production Note |
|-----|-----------|------------|----------------|---------------------------|-----------------|
| 3.1.1 | Xem danh sách tất cả Shop | Cần làm | `/admin/shops` + `GetAllAsync` | Admin active mới được xem | Cần filter theo `IsActive`, search theo tên/phone |
| 3.1.2 | Active / Deactive Shop (`Shop.IsActive`) | Cần làm | `SetShopActiveStatusService` | Shop inactive → **chặn hoàn toàn**:<br>- Tạo shipment (UI + Partner API)<br>- Quote<br>- Update integration<br>- Tạo API client<br>Nhưng vẫn xem lịch sử read-only | **Rất quan trọng**. Phải enforce ở mọi service Shop-facing |
| 3.1.3 | Xem chi tiết Shop Profile | Cần làm | - | Hiển thị: Owner User, Name, Phone, Default Pickup Address, Active status, Created/Updated time | - |
| 3.1.4 | Xem lịch sử thay đổi profile của Shop | Chưa có | - | - | Cần audit |

### 3.2 Business Rules quan trọng

- Admin **không được phép sửa profile thay Shop** (đã chốt trong Task 1)
- Deactive Shop không hủy shipment đang vận hành và không revoke API key ngay lập tức
- Reactivate Shop khôi phục ngay khả năng tạo đơn và sử dụng API client đang active

---

## 4. ĐIỀU PHỐI & VẬN HÀNH ĐƠN HÀNG (Operations & Dispatching)

### 4.1 Tính năng hiện có (đã hoàn thiện tốt cho demo)

| STT | Tính năng | Trạng thái | Use case chính | Business Rule quan trọng |
|-----|-----------|------------|----------------|---------------------------|
| 4.1.1 | Xem hàng đợi `PendingPickup` + Fallback reason | Đã có | `/operations/assignments` + `GetPendingPickupShipments` | Hiển thị insight shipper match area (ưu tiên shipper khớp) |
| 4.1.2 | Retry Auto Assignment (từng đơn) | Đã có | `AutoAssignShipmentService` | Chỉ xử lý shipment đang ở trạng thái `PendingPickup` |
| 4.1.3 | Manual Assign / Override Shipper | Đã có | `AssignShipperToShipmentService` | - Chỉ cho phép assign khi shipment ở `PendingPickup`<br>- Mỗi shipment chỉ có 1 active assignment<br>- Cảnh báo rõ ràng nếu shipper không khớp working area |
| 4.1.4 | Theo dõi bảng vận hành (Active Operations Board) | Đã có (mở rộng gần đây) | `GetOperationsShipments` | Hiển thị các trạng thái: `Assigned` → `DeliveryFailed` + `Delivered + COD PendingCollection` |
| 4.1.5 | Update Shipment Status hỗ trợ (thay Shipper) | Đã có | `UpdateShipmentStatusService` | Admin/Operator không cần là assigned shipper của đơn đó |
| 4.1.6 | Xác nhận COD Collected (fallback) | Đã có | `MarkCodCollectedService` | Chỉ khi shipment `Delivered` và COD `PendingCollection` |

### 4.2 Tính năng cần bổ sung (Production)

| STT | Tính năng | Mức độ cần thiết | Ghi chú |
|-----|-----------|------------------|---------|
| 4.2.1 | **Re-assign Shipper** sau khi đã `Assigned` | Rất cao | Hiện domain chỉ cho phép assign ở `PendingPickup`. Cần mở rộng |
| 4.2.2 | Bulk Retry Auto Assignment | Cao | Hiện chỉ retry từng đơn |
| 4.2.3 | Hủy Assignment thủ công | Cao | Cần khi shipper nghỉ đột xuất hoặc có vấn đề |
| 4.2.4 | SLA Warning & Escalation | Trung bình | Cảnh báo đơn pending quá lâu, delivery failed nhiều |
| 4.2.5 | Filter + Search nâng cao trên Operations Board | Cao | Theo shipper, province, status, date range, COD amount |

---

## 5. QUẢN LÝ PARTNER INTEGRATIONS (Toàn hệ thống)

### 5.1 Tính năng hiện có

| STT | Tính năng | Trạng thái | Ghi chú Production |
|-----|-----------|------------|---------------------|
| 5.1.1 | Xem danh sách tất cả Shop + API Client | Đã có | Admin thấy **toàn bộ** shop |
| 5.1.2 | Tạo / Rotate / Active / Deactive API Client cho mọi Shop | Đã có | Task 5 đã có audit credential |
| 5.1.3 | Upsert Webhook Endpoint + Test Webhook | Đã có | - |
| 5.1.4 | Xem lịch sử Webhook Delivery | Đã có | - |

### 5.2 Vấn đề Production quan trọng

**Rủi ro bảo mật & vận hành:**
- Hiện tại **một Admin có quyền quản lý integrations của tất cả Shop**. Đây là quyền quá rộng.
- **Khuyến nghị:** 
  - Tách quyền thành `Integration Admin` hoặc permission theo Shop Group / Region.
  - Hoặc Admin chỉ được xem integrations của Shop do mình phụ trách.

**Tính năng cần bổ sung:**
- Quản lý integrations theo phân quyền (không thấy hết)
- Audit chi tiết mọi thay đổi API Key / Webhook (đã có một phần ở Task 5)
- Dashboard theo dõi Webhook delivery success rate / latency

---

## 6. XỬ LÝ COD & TÀI CHÍNH (COD & Settlement)

### 6.1 Tính năng hiện có

- Xác nhận COD Collected (Shipper chính + Admin/Operator fallback)
- `MarkCodSettledService` (chỉ Admin)

### 6.2 Tính năng cần bổ sung (Rất quan trọng cho Production)

| STT | Tính năng | Mức độ cần thiết | Ghi chú |
|-----|-----------|------------------|---------|
| 6.2.1 | Dashboard COD Pending / Collected / Settled | Rất cao | Filter theo Shipper, Hub, Date, Amount |
| 6.2.2 | COD Settlement Flow đầy đủ | Rất cao | Hiện chỉ có service, thiếu UI + workflow rõ ràng |
| 6.2.3 | Báo cáo COD theo Shipper / Hub / Ngày | Cao | Tỷ lệ thu COD, số tiền pending |
| 6.2.4 | Xử lý COD Discrepancy / Khiếu nại | Cao | Workflow + note + approval |
| 6.2.5 | Cash Handover Workflow (Shipper → Hub → Accounting) | Trung bình | Hiện chưa có |

**Business Rule cần bảo vệ:**
- `Delivered + COD PendingCollection` → **giữ active assignment** để shipper/Admin xác nhận
- Chỉ khi `MarkCodCollected` thành công → mới deactivate assignment

---

## 7. GIÁM SÁT, AUDIT & BÁO CÁO (Monitoring, Audit & Reporting)

### 7.1 Tính năng cần có (hầu hết chưa triển khai)

| STT | Tính năng | Mức độ cần thiết | Ghi chú |
|-----|-----------|------------------|---------|
| 7.1.1 | **Audit Log** cho mọi hành động của Admin | **Rất cao** | Ghi: Actor, Action, Target, Old Value → New Value, IP, User Agent, Timestamp |
| 7.1.2 | Audit Log cho Partner API Credential actions | Đã có (Task 5) | Cần UI xem lịch sử đẹp hơn |
| 7.1.3 | Dashboard tổng quan hệ thống | Rất cao | Số đơn hôm nay, Pending assignment, Active shipper, COD pending, Failed delivery rate |
| 7.1.4 | Báo cáo hiệu suất Shipper | Cao | On-time delivery rate")). Return rate, COD collected rate |
| 7.1.5 | Cảnh báo SLA & Exception | Cao | Đơn pending quá lâu, Shipper overload, COD pending quá hạn |
| 7.1.6 | Export dữ liệu (đơn hàng, COD, Shipper performance) | Cao | Hỗ trợ CSV / Excel |

**Production Recommendation:**  
Nên có một bảng `AdminAuditLog` chung (hoặc `AdminActionLog`) để ghi tất cả hành động nhạy cảm của Admin.

---

## 8. CẤU HÌNH HỆ THỐNG (System Configuration)

| STT | Tính năng | Trạng thái | Ghi chú Production |
|-----|-----------|------------|---------------------|
| 8.1 | Quản lý Hub (tạo/sửa/xóa/active) | Chưa có UI | Repository đã có, cần UI + validation |
| 8.2 | Quản lý danh sách Province hỗ trợ Route Classification | Chưa có UI | Hiện hardcode trong `RouteClassificationService` |
| 8.3 | Cấu hình phí vận chuyển (BaseFee, ExtraWeightFee, Insurance rate, ReturnFee) | Chưa có | Hiện tính trong `ShippingFeeService` |
| 8.4 | Cấu hình Return Fee rate | Chưa có | - |
| 8.5 | Quản lý danh sách Working Area Template (nếu cần) | Chưa có | - |

---

## 9. TỔNG KẾT & KHUYẾN NGHỊ ƯU TIÊN TRIỂN KHAI

### 9.1 Nhóm tính năng nên làm ngay (Sau Demo)

| Thứ tự | Tính năng | Lý do |
|--------|-----------|-------|
| 1 | Admin Shop Management (Active/Deactive Shop) - Task 1 | Enforce business rule quan trọng nhất |
| 2 | Audit Log cho Admin actions | Compliance + traceability |
| 3 | COD Settlement Dashboard + Reporting | Tài chính thực tế |
| 4 | Re-assign Shipper sau khi đã Assigned | Vận hành thực tế hay gặp |
| 5 | Hub Management UI | Cấu hình working area |

### 9.2 Nhóm tính năng Production nâng cao

- Granular Permission cho Admin (tách quyền Integration)
- Shipper Performance Dashboard
- SLA & Exception Monitoring
- Full COD Accounting module
- Mobile/PWA support cho Shipper (không phải Admin trực tiếp, nhưng Admin cần theo dõi)

### 9.3 Rủi ro nếu không triển khai đầy đủ

- Admin có quyền quá rộng → rủi ro bảo mật nội bộ
- Thiếu Audit Log → khó truy vết khi có sự cố
- Thiếu COD Settlement → không đóng được dòng tiền thực tế
- Không có Re-assign → vận hành bị tắc khi shipper có vấn đề

---

**Kết thúc tài liệu**

Tài liệu này được thiết kế để **Agent Coding** có thể đọc trực tiếp, hiểu rõ business context, và triển khai chính xác các tính năng còn thiếu của role Admin theo chuẩn Production. 

Nếu cần chi tiết hơn về bất kỳ tính năng nào (ví dụ: thiết kế Entity, Service, UI flow, hoặc API), hãy yêu cầu cụ thể.