# PHÂN TÍCH TOÀN BỘ TÍNH NĂNG CẦN CÓ CỦA ROLE OPERATOR
## Dự án Mini Logistics Management System

**Phiên bản phân tích:** 1.0  
**Ngày phân tích:** 20/07/2026  
**Mục đích:** Cung cấp tài liệu đầy đủ cho Agent Coding đọc, phân tích và triển khai nhằm đưa role Operator từ mức MVP/demo hiện tại lên gần chuẩn Production thực tế (control-tower operations của logistics enterprise), phục vụ mục tiêu portfolio chất lượng cao.  
**Nguồn tham chiếu:** operator.md, overview.md, README.md, Admin_Role_Full_Features_Production_Analysis.md, shipper.md, shop.md, các bản phân tích Shop & Shipper trước đó  
**Tiêu chí đánh giá Production:** Scalability, Performance, Maintainability, Security, Clean Code (Clean Architecture + Modular Monolith)

---

## 1. MỤC TIÊU CỦA TÀI LIỆU

Tài liệu này liệt kê **toàn bộ tính năng** mà role `Operator` cần có trong dự án Logistics, bao gồm:

- Tính năng **đã triển khai** ở mức MVP/demo hiện tại.
- Tính năng **cần bổ sung hoặc cải tiến** để đạt chuẩn Production thực tế của các nền tảng logistics lớn (GHN, GHTK, J&T, SPX…).
- Business rules quan trọng phải được enforce chặt chẽ.
- Production considerations theo 5 tiêu chí cốt lõi.
- Use case / luồng chính, rủi ro vận hành nếu thiếu, và khuyến nghị ưu tiên triển khai thực tế.

**Định vị role Operator trong enterprise logistics:**  
Operator là **Control Tower** của vận hành hàng ngày. Họ không phải Admin (không quản lý user, capacity, partner), cũng không phải Shipper (không ra đường giao hàng). Nhiệm vụ cốt lõi là: điều phối, giám sát, hỗ trợ xử lý ngoại lệ, đảm bảo đơn hàng chảy mượt từ PendingPickup → Delivered/Returned. Ở production thật, role này thường được tách thành nhiều cấp (Dispatcher, Supervisor, COD Officer…) với permission granular.

---

## 2. PHẠM VI ROLE & ENTRY POINTS HIỆN TẠI

### 2.1 Phạm vi đã chốt (MVP)

- Chỉ thao tác trên shipment lifecycle và assignment.
- Không quản lý user, working area, capacity, partner integrations.
- Chia sẻ một số quyền vận hành với Admin (update status, COD collected).
- Không có quyền settle COD (chỉ Admin).

### 2.2 Entry points hiện có

| Entry point | File | Ghi chú |
|-------------|------|---------|
| `/operations/assignments` | `OperationsAssignments.razor` | Trang chính – vừa pending queue vừa active board |
| `/tracking` | `Tracking.razor` | Tra cứu nhanh public |

Navigation hiện chỉ hiện nhóm “Điều phối” cho `Admin, Operator`.

---

## 3. HÀNG ĐỢI PENDING PICKUP & ASSIGNMENT

### 3.1 Tính năng hiện có

| STT | Tính năng | Trạng thái | Use case chính | Business Rule quan trọng | Production Note |
|-----|-----------|------------|----------------|---------------------------|-----------------|
| 3.1.1 | Xem hàng đợi `PendingPickup` + Fallback insight | Đã có | `GetPendingPickupShipmentsService` | Hiển thị lý do không auto-assign được + ưu tiên shipper khớp area | Insight tính ở UI layer (tốt cho demo) |
| 3.1.2 | Retry Auto Assignment (từng đơn) | Đã có | `IAutoAssignShipmentService` | Chỉ xử lý khi status = `PendingPickup` | - |
| 3.1.3 | Manual Assign / Override | Đã có | `AssignShipperToShipmentService` | Cho phép override shipper ngoài working area + cảnh báo rõ ràng | Service không validate area (đúng chủ đích override) |

### 3.2 Tính năng cần bổ sung / cải tiến (Production)

| STT | Tính năng | Mức độ cần thiết | Use case chính | Production Considerations |
|-----|-----------|------------------|----------------|---------------------------|
| 3.2.1 | **Bulk Retry Auto Assignment** | **Rất cao** | Chọn nhiều đơn PendingPickup → Retry hàng loạt | **Performance:** Background job hoặc batch service. Tránh N+1 |
| 3.2.2 | **Re-assign Shipper sau khi đã Assigned** | **Rất cao** | Shipper nghỉ đột xuất, xe hỏng, overload… | Hiện domain chỉ cho assign ở `PendingPickup`. Cần mở rộng domain rule + audit mạnh |
| 3.2.3 | **Hủy Assignment thủ công (Unassign)** | Cao | Trả đơn về `PendingPickup` khi cần | Phải deactivate active assignment + ghi lý do |
| 3.2.4 | **Ghi chú / Lý do Override bắt buộc** khi assign ngoài area | Cao | Audit trail rõ ràng | Bắt buộc note khi mismatch area |
| 3.2.5 | **Approval workflow cho Override** (tùy chọn) | Trung bình | Supervisor duyệt khi override | Chỉ cần khi tổ chức có nhiều cấp |
| 3.2.6 | **Filter + Search nâng cao trên Pending Queue** | Cao | Theo province, COD amount, thời gian tạo, số lần retry… | Index tốt + server-side paging |

**Business Rules quan trọng cần bảo vệ / mở rộng:**
- Chỉ `Admin` / `Operator` được assign.
- Target phải là user active + role `Shipper`.
- Mỗi shipment chỉ có **một** active assignment tại một thời điểm.
- Auto-assign chỉ chọn shipper: active + available + area match + chưa vượt `MaxActiveShipments`.
- Manual override được phép nhưng **phải cảnh báo + ghi audit**.

---

## 4. BẢNG VẬN HÀNH ACTIVE (Operations Board)

### 4.1 Tính năng hiện có

| STT | Tính năng | Trạng thái | Use case chính | Business Rule quan trọng |
|-----|-----------|------------|----------------|---------------------------|
| 4.1.1 | Theo dõi các đơn đang vận hành | Đã có | `GetOperationsShipmentsService` | Hiển thị: Assigned → DeliveryFailed + Delivered (chỉ khi COD PendingCollection) |
| 4.1.2 | Xem shipper đang xử lý đơn nào | Đã có | Response có active shipper summary | - |
| 4.1.3 | Xem tracking history nhanh | Đã có | - | - |

### 4.2 Tính năng cần bổ sung / cải tiến (Production)

| STT | Tính năng | Mức độ cần thiết | Production Considerations |
|-----|-----------|------------------|---------------------------|
| 4.2.1 | **Filter + Search + Sort mạnh** (shipper, status, province, date range, COD, SLA) | **Rất cao** | Đây là màn hình “sống” cả ngày của Operator. UX kém = năng suất thấp |
| 4.2.2 | **Phân tab / Queue theo giai đoạn** (Pickup / In-transit / Delivery / Exception / COD Pending) | Cao | Giảm cognitive load |
| 4.2.3 | **SLA Warning & Color coding** | Cao | Đơn PendingPickup > X giờ, DeliveryFailed nhiều lần, COD pending quá hạn → highlight đỏ/vàng | Rất quan trọng với vận hành thực tế |
| 4.2.4 | **Real-time update (SignalR)** | Cao | Đơn mới assign / status đổi hiện ngay không cần refresh | Scale bằng Redis backplane |
| 4.2.5 | **Drill-down nhanh sang chi tiết + action** | Cao | Click vào đơn → side panel hoặc modal đầy đủ action | Tránh navigate lung tung |
| 4.2.6 | **Export danh sách đang vận hành** | Trung bình | Báo cáo cuối ca / cuối ngày | - |

**Lưu ý kiến trúc:**  
Hiện tại một page gộp cả Pending Queue + Active Board. Ở production volume cao nên cân nhắc tách thành các queue riêng hoặc dùng tab + lazy load để Performance tốt hơn.

---

## 5. HỖ TRỢ CẬP NHẬT TRẠNG THÁI (Status Support)

### 5.1 Tính năng hiện có

- Operator có thể update status **bất kỳ** shipment operations (không cần là assigned shipper).
- Lifecycle rule được enforce ở Domain.
- `DeliveryFailed` bắt buộc note.

### 5.2 Tính năng cần bổ sung / cải tiến

| STT | Tính năng | Mức độ cần thiết | Ghi chú |
|-----|-----------|------------------|---------|
| 5.2.1 | **Bắt buộc chọn Failure Reason Code** (ngoài free-text note) | Cao | Đồng bộ với Shipper analysis – dữ liệu sạch cho báo cáo |
| 5.2.2 | **Ghi nhận người hỗ trợ + lý do hỗ trợ** khi Operator update thay Shipper | Cao | Audit rõ “ai làm thay” |
| 5.2.3 | **Hạn chế một số transition nhạy cảm** (ví dụ chỉ Supervisor mới được force Returned) | Trung bình | Khi tách cấp permission |
| 5.2.4 | **Xem Proof of Delivery** (khi Shipper đã upload) | Cao | Operator cần kiểm tra ảnh/chữ ký khi có khiếu nại |

---

## 6. HỖ TRỢ COD (COD Support)

### 6.1 Tính năng hiện có

- Confirm COD Collected khi `Delivered` + `PendingCollection` (fallback cho Shipper).
- Không có quyền `MarkCodSettled` (đúng – chỉ Admin).

### 6.2 Tính năng cần bổ sung / cải tiến

| STT | Tính năng | Mức độ cần thiết | Production Considerations |
|-----|-----------|------------------|---------------------------|
| 6.2.1 | **Dashboard COD Pending theo Shipper / Hub** | **Rất cao** | Operator cần nhìn tổng quan ai đang giữ tiền bao nhiêu | Filter + sort theo số tiền / thời gian |
| 6.2.2 | **Cảnh báo COD Pending quá hạn** | Cao | Highlight đơn Delivered lâu mà chưa collected | - |
| 6.2.3 | **Xác nhận số tiền thực tế** (khi support) | Cao | Đồng bộ với Shipper – ghi discrepancy nếu lệch | - |
| 6.2.4 | **Chuyển tiếp sang quy trình Handover / Settlement** | Trung bình | Operator đánh dấu “đã nhắc shipper bàn giao” | Chuẩn bị cho flow Cash Handover |

**Business Rule cần bảo vệ tuyệt đối:**  
Operator chỉ được **Collected**, không được **Settled**. Sau Collected → assignment deactivate.

---

## 7. GIÁM SÁT, ESCALATION & BÁO CÁO VẬN HÀNH

Đây là nhóm tính năng gần như chưa có, nhưng rất quan trọng với role Control Tower.

| STT | Tính năng | Mức độ cần thiết | Ghi chú Enterprise |
|-----|-----------|------------------|--------------------|
| 7.1 | **SLA Monitoring & Escalation** | **Rất cao** | Đơn pending quá lâu → tự động escalate lên Supervisor / Admin |
| 7.2 | **Exception Queue** (DeliveryFailed nhiều lần, địa chỉ sai, COD discrepancy…) | Cao | Tách riêng để ưu tiên xử lý |
| 7.3 | **Báo cáo hiệu suất theo ca / theo ngày** | Cao | Số đơn xử lý, tỷ lệ thành công, thời gian trung bình… |
| 7.4 | **Heatmap / Thống kê theo khu vực** | Trung bình | Tỉnh / quận nào đang tắc | Hữu ích khi scale |
| 7.5 | **Audit log mọi action của Operator** | **Rất cao** | Ai assign, ai override, ai update status, ai confirm COD… | Compliance bắt buộc |

---

## 8. PHÂN QUYỀN & BẢO MẬT (Production-ready Permission)

Hiện tại role `Operator` khá “phẳng”. Ở production thật cần chuẩn bị tách:

| Cấp độ đề xuất | Quyền chính | Ghi chú |
|----------------|-------------|---------|
| Dispatcher | Assign, Retry, xem board | Level cơ bản |
| Supervisor | Re-assign, Unassign, force một số status, xem báo cáo | Level cao hơn |
| COD Officer | Chỉ tập trung COD Pending + Collected | Có thể tách riêng |
| Full Operator (hiện tại) | Gần như tất cả quyền vận hành | Giữ cho demo / team nhỏ |

**Khuyến nghị kỹ thuật:**  
Không hard-code role check nữa. Chuyển sang **Permission-based** (claim hoặc policy) để sau này dễ tách cấp mà không phải sửa hàng loạt service.

---

## 9. CẢI TIẾN KỸ THUẬT THEO 5 TIÊU CHÍ PRODUCTION

### 9.1 Scalability
- Pending Queue + Operations Board phải hỗ trợ hàng nghìn đơn (server-side paging + filter mạnh).
- Bulk action dùng background job.
- Real-time dùng SignalR + Redis backplane.

### 9.2 Performance
- Query Operations phải tối ưu index `(Status, AssignedShipperId, UpdatedAtUtc)`, `(PickupProvince, Status)`.
- Tránh load full tracking history trên list.
- Insight shipper match area nên cache hoặc pre-compute nếu volume lớn.

### 9.3 Maintainability
- Giữ Clean Architecture.
- Tách rõ Query service (`GetPendingPickup`, `GetOperationsShipments`) và Command service (Assign, ReAssign, Unassign, UpdateStatus, MarkCodCollected).
- Domain event khi assignment thay đổi / status thay đổi để dễ mở rộng notification & audit.

### 9.4 Security
- Mọi action phải audit (actor, target shipment, old → new, lý do, IP, timestamp).
- Override ngoài area bắt buộc note + audit mức cao.
- Permission check ở Application layer, không chỉ UI.

### 9.5 Clean Code
- Tránh “god service”. Tách `IAssignmentService`, `IOperationsQueryService`, `ICodSupportService`.
- Explicit method trong Domain cho các hành động quan trọng (`Shipment.ReassignShipper(...)`, `Shipment.Unassign(...)`).
- Test coverage cao cho: authorization, override warning, re-assign rule, visibility của Delivered + COD Pending.

---

## 10. TỔNG KẾT & KHUYẾN NGHỊ ƯU TIÊN TRIỂN KHAI

### 10.1 Nhóm tính năng nên làm ngay (Ưu tiên cao nhất)

| Thứ tự | Tính năng | Lý do chính (Business + Portfolio) |
|--------|-----------|------------------------------------|
| 1 | **Re-assign Shipper sau khi đã Assigned + Unassign** | Vận hành thực tế gặp liên tục. Thiếu tính năng này = tắc nghẽn khi shipper có vấn đề |
| 2 | **Filter + Search + SLA Warning trên Operations Board** | Màn hình chính của Operator. UX kém = năng suất thấp rõ rệt |
| 3 | **Bulk Retry Auto Assignment** | Tiết kiệm thời gian rất nhiều khi có nhiều đơn fallback |
| 4 | **Audit log đầy đủ mọi action của Operator** | Compliance + truy vết sự cố – bắt buộc với enterprise |
| 5 | **Dashboard COD Pending theo Shipper** | Kiểm soát tiền mặt – vấn đề nóng |
| 6 | **Failure Reason Code + bắt buộc note khi override** | Dữ liệu sạch + audit rõ ràng |
| 7 | **Chuẩn bị Permission-based** (thay vì chỉ check role) | Để sau này tách Dispatcher / Supervisor dễ dàng |

### 10.2 Nhóm tính năng Production nâng cao (làm sau)

- Tách queue theo station / team / hub.
- Escalation tự động theo SLA.
- Real-time board với SignalR.
- Approval workflow cho override.
- Báo cáo hiệu suất chi tiết theo ca.

### 10.3 Rủi ro nếu không triển khai đầy đủ

- Không có Re-assign → khi shipper nghỉ đột xuất, đơn bị treo, khách hàng khiếu nại.
- Operations Board thiếu filter/SLA → Operator làm việc như “mò kim đáy bể”, năng suất thấp.
- Thiếu Audit → không truy được trách nhiệm khi có sự cố assign sai hoặc mất COD.
- Quyền quá phẳng → khó scale tổ chức (không tách được cấp bậc vận hành).

---

**Kết thúc tài liệu**

Tài liệu này được thiết kế để **Agent Coding** có thể đọc trực tiếp, hiểu rõ vị trí “Control Tower” của role Operator, và triển khai chính xác các tính năng còn thiếu theo chuẩn Production enterprise.

**Điểm nhấn quan trọng nhất:**  
Operator là người giữ nhịp độ vận hành hàng ngày. Nếu tool của họ yếu (thiếu re-assign, thiếu filter, thiếu SLA, thiếu audit), toàn bộ hệ thống sẽ bị đánh giá là “chỉ chạy được demo đẹp”, không chịu được áp lực thực tế.

Nếu bạn muốn đi sâu ngay vào thiết kế kỹ thuật của một hạng mục ưu tiên cao (ví dụ: mở rộng Domain để hỗ trợ Re-assign + Unassign, hoặc thiết kế Operations Board với filter + SLA, hoặc mô hình Permission), hãy cho tôi biết để phân tích tiếp ở mức implementation-ready.

## Current Code Snapshot / Implementation Checklist

Canonical checklist: `docs/roles/Implementation_Checklist_Current_Code.md`.

- Implemented: operations filters, SLA badge, paging UI, assign/reassign/cancel assignment, bulk retry auto assignment, COD support.
- Implemented but needs hardening: operations/pending queries now filter/page in repository, COD lookup is batched, permission catalog/service and route policy are added, audit taxonomy is expanded.
- Remaining focus: query-shape tests, UI permission granularity beyond Admin/Operator mapping, failure reason, COD dashboard, and production-grade COD actual collected amount.
