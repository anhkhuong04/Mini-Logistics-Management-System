# PHÂN TÍCH TOÀN BỘ TÍNH NĂNG CẦN CÓ CỦA ROLE SHOP
## Dự án Mini Logistics Management System

**Phiên bản phân tích:** 1.0  
**Ngày phân tích:** 20/07/2026  
**Mục đích:** Cung cấp tài liệu đầy đủ cho Agent Coding đọc, phân tích và triển khai nhằm đưa role Shop từ mức MVP/demo hiện tại lên gần chuẩn Production thực tế, phục vụ mục tiêu portfolio chất lượng cao.  
**Nguồn tham chiếu:** shop.md, overview.md, README.md, Admin_Role_Full_Features_Production_Analysis.md, operator.md, shipper.md  
**Tiêu chí đánh giá Production:** Scalability, Performance, Maintainability, Security, Clean Code (Clean Architecture + Modular Monolith)

---

## 1. MỤC TIÊU CỦA TÀI LIỆU

Tài liệu này liệt kê **toàn bộ tính năng** mà role `Shop` cần có trong dự án Logistics, bao gồm:

- Tính năng **đã triển khai** ở mức MVP/demo hiện tại.
- Tính năng **cần bổ sung hoặc cải tiến** để đạt chuẩn Production.
- Business rules quan trọng phải được enforce chặt chẽ.
- Production considerations theo 5 tiêu chí cốt lõi (Scalability, Performance, Maintainability, Security, Clean Code).
- Use case / luồng chính và rủi ro nếu thiếu.

Phân loại theo **domain nghiệp vụ thực tế** của một nền tảng logistics (tương tự GHN, GHTK, J&T, SPX) khi nhìn từ góc độ đối tác Shop (merchant / e-commerce seller).

---

## 2. QUẢN LÝ HỒ SƠ SHOP (Shop Profile Management)

### 2.1 Tính năng hiện có

| STT | Tính năng | Trạng thái | Use case chính | Business Rule quan trọng | Production Note |
|-----|-----------|------------|----------------|---------------------------|-----------------|
| 2.1.1 | Đăng ký Shop (RegisterShop) | Đã có | `/register-shop` + `RegisterShopService` | - Tạo Identity User + role `Shop`<br>- Mỗi owner user chỉ có một Shop<br>- Lưu phone + default pickup address | Cần validation mạnh hơn (phone format VN, địa chỉ chuẩn hóa) |
| 2.1.2 | Xem danh sách / chi tiết shipment của chính mình | Đã có | Dashboard + `/shipments` + Detail | Ownership strict theo `ShopId` | - |

### 2.2 Tính năng cần bổ sung / cải tiến (Production)

| STT | Tính năng | Mức độ cần thiết | Use case chính | Business Rule quan trọng | Production Considerations |
|-----|-----------|------------------|----------------|---------------------------|---------------------------|
| 2.2.1 | Xem & chỉnh sửa hồ sơ Shop (tên, phone, default pickup address) | **Rất cao** | `/shop/profile` + `UpdateShopProfileService` | - Chỉ owner active mới được sửa<br>- Không cho phép sửa `IsActive` (chỉ Admin)<br>- Chuẩn hóa địa chỉ (Province/Ward theo danh sách hỗ trợ) | **Security:** Audit mọi thay đổi profile<br>**Maintainability:** Tách Value Object Address rõ ràng<br>**Scalability:** Không ảnh hưởng |
| 2.2.2 | Xem trạng thái Active/Inactive của Shop (read-only) | Cao | Hiển thị banner cảnh báo khi bị deactive | Shop inactive → chặn toàn bộ create/quote/API | Đồng bộ với rule Admin `SetShopActiveStatus` |
| 2.2.3 | Đổi mật khẩu / bảo mật tài khoản | Cao | Tích hợp ASP.NET Core Identity | Password policy mạnh, 2FA optional | **Security:** Bắt buộc change password lần đầu nếu seed, rate-limit forgot-password |
| 2.2.4 | Lịch sử thay đổi hồ sơ (Audit) | Trung bình | Shop xem được lịch sử tự thay đổi | Chỉ hiển thị action của chính mình | Chuẩn bị bảng `ShopAuditLog` dùng chung pattern với AdminAuditLog |

**Business Rules quan trọng cần bảo vệ:**
- Admin không được sửa profile thay Shop (đã chốt ở Admin analysis).
- Shop inactive vẫn xem được lịch sử đơn hàng (read-only) nhưng không tạo đơn mới / không dùng Partner API.
- Default pickup address phải thuộc danh sách Province hỗ trợ Route Classification.

---

## 3. QUẢN LÝ ĐƠN HÀNG (Shipment Management)

### 3.1 Tính năng hiện có (MVP đã khá vững)

| STT | Tính năng | Trạng thái | Use case chính | Business Rule quan trọng | Production Note |
|-----|-----------|------------|----------------|---------------------------|-----------------|
| 3.1.1 | Tạo shipment (UI) với fee preview real-time | Đã có | `/shipments/create` + `CreateShipmentService` | Fee tính 2 lần (UI + server), auto-assign ngay sau create | Transaction boundary hiện tại: save shipment+COD trước, rồi auto-assign |
| 3.1.2 | Xem danh sách shipment của Shop | Đã có | `/shipments` + `GetShipmentsForCurrentShopService` | Chỉ thấy đơn thuộc `ShopId` của mình | Thiếu pagination, filter, sort |
| 3.1.3 | Xem chi tiết + tracking history | Đã có | `/shipments/{id}` | Ownership strict, ẩn PII nếu cần | Tracking history đã map actor |
| 3.1.4 | Hủy đơn (Cancel) khi còn hợp lệ | Đã có | `CancelShipmentForCurrentShopService` | Chỉ cancel trước khi `PickedUp` trở đi; deactivate assignment | Webhook `shipment.status_changed` đã có |
| 3.1.5 | Dashboard tổng quan | Đã có | `/dashboard` | Thống kê cơ bản theo status | Cần nâng cấp thành KPI thực tế |

### 3.2 Tính năng cần bổ sung / cải tiến (Production)

| STT | Tính năng | Mức độ cần thiết | Ghi chú Production theo tiêu chí |
|-----|-----------|------------------|----------------------------------|
| 3.2.1 | **Pagination + Filter + Search nâng cao** trên danh sách đơn | **Rất cao** | Filter theo status, date range, COD amount, tracking code, receiver phone/name. **Performance:** Index DB + server-side paging. **Scalability:** Hỗ trợ 10k+ đơn/shop |
| 3.2.2 | **Draft Shipment** (lưu nháp) | Cao | Cho phép lưu form chưa submit, edit lại trước khi tạo chính thức. Giảm lỗi nhập liệu |
| 3.2.3 | **Chỉnh sửa đơn** trước khi Assigned / PickedUp | Cao | Chỉ cho phép sửa địa chỉ nhận, COD, ghi chú khi status còn `PendingPickup` hoặc `Assigned` (chưa PickingUp). Domain rule mới cần thiết kế cẩn thận |
| 3.2.4 | **Bulk Create / Import CSV/Excel** | Cao | Shop upload file → validate → tạo nhiều đơn. **Performance:** Background job (Hangfire/Quartz) + progress tracking. **Security:** Giới hạn kích thước file + virus scan (production) |
| 3.2.5 | **Clone / Tái tạo đơn** từ đơn cũ | Trung bình | Giảm thời gian nhập liệu cho đơn lặp lại |
| 3.2.6 | **In nhãn vận đơn (Label / AWB)** | Cao | PDF label chuẩn (có barcode tracking code). Cần thư viện PDF (QuestPDF hoặc tương đương) |
| 3.2.7 | **Thông báo trạng thái đơn** (Email / In-app) | Cao | Khi status thay đổi quan trọng (Assigned, PickedUp, Delivered, Failed, Returned). Có thể dùng SignalR + Email queue |
| 3.2.8 | **Xuất Excel / CSV** danh sách đơn | Cao | Export theo filter hiện tại. **Performance:** Streaming export, không load toàn bộ vào memory |

**Business Rules quan trọng cần bảo vệ / mở rộng:**
- Shop **không bao giờ** được assign / re-assign shipper.
- Shop **không** được update status sau khi tạo (trừ Cancel hợp lệ).
- Cancel chỉ được phép trước các trạng thái terminal / in-transit mạnh (`PickedUp` trở đi bị chặn).
- Mọi thay đổi đơn (nếu cho phép edit) phải ghi audit + publish webhook nếu có external reference.
- Fee luôn được tính lại server-side, không tin tưởng client.

**Production Considerations theo tiêu chí:**
- **Scalability:** Mọi list query phải có pagination + index trên `(ShopId, Status, CreatedAtUtc)`.
- **Performance:** Fee calculation nên cache FeeRule active; volumetric weight tính nhanh.
- **Maintainability:** Giữ nguyên Clean Architecture – Command/Query tách biệt, FluentValidation cho mọi input.
- **Security:** Ownership check ở Application layer (không chỉ UI), chống IDOR.
- **Clean Code:** Tách `IShipmentQueryService` riêng cho các query phức tạp (filter/paging).

---

## 4. PARTNER INTEGRATIONS & API (Kênh tích hợp bên thứ ba)

### 4.1 Tính năng hiện có (MVP khá hoàn chỉnh)

| STT | Tính năng | Trạng thái | Ghi chú |
|-----|-----------|------------|---------|
| 4.1.1 | Quản lý API Client (tạo, rotate key, active/deactive) | Đã có | Key chỉ hiện 1 lần khi tạo |
| 4.1.2 | Upsert Webhook Endpoint + Test delivery | Đã có | |
| 4.1.3 | Xem lịch sử Webhook Delivery | Đã có | |
| 4.1.4 | Partner REST API đầy đủ (quote, create, track, cancel) | Đã có | Rate limit in-memory, Idempotency-Key, HMAC signature hỗ trợ |
| 4.1.5 | Audit request Partner API | Đã có một phần | |

### 4.2 Tính năng cần bổ sung / cải tiến (Production)

| STT | Tính năng | Mức độ cần thiết | Production Considerations |
|-----|-----------|------------------|---------------------------|
| 4.2.1 | **Dashboard sử dụng API** (số request, rate limit còn lại, lỗi gần đây) | Cao | Shop tự monitor quota. **Performance:** Aggregate metrics theo giờ/ngày |
| 4.2.2 | **Webhook Retry Policy + Dead Letter** | **Rất cao** | Retry exponential backoff, max 5 lần, sau đó đưa vào DLQ để Shop xem. Hiện chỉ có delivery history cơ bản |
| 4.2.3 | **IP Whitelist / Allowed Origins** cho API Client | Cao | Tăng Security mạnh |
| 4.2.4 | **Scope / Permission nhỏ hơn** trên API Client (chỉ quote, chỉ create…) | Trung bình | Granular permission thay vì full access |
| 4.2.5 | **Xoay vòng key tự động + cảnh báo hết hạn** | Trung bình | Security best practice |
| 4.2.6 | **Webhook Signature Verification mạnh hơn + Timestamp** | Cao | Chống replay attack |
| 4.2.7 | Endpoint bổ sung hữu ích: `GET /shipments` (list theo externalOrderId hoặc date) | Trung bình | Giảm tải tracking từng đơn |

**Business Rules quan trọng:**
- API Client inactive → reject ngay lập tức.
- Shop inactive → toàn bộ API Client của shop bị vô hiệu hóa logic (không cần deactivate từng key).
- Idempotency-Key + request hash phải được enforce nghiêm ngặt (đã có).
- Mọi create qua API vẫn phải chạy auto-assign và tạo COD giống UI.

**Production Considerations theo tiêu chí:**
- **Scalability / Performance:** Rate limit phải chuyển sang distributed (Redis) thay vì in-memory. Webhook delivery nên dùng background worker + outbox pattern.
- **Security:** Key storage hash (không lưu plain), audit mọi rotate/create/delete, rate-limit theo IP + ClientId.
- **Maintainability:** Tách Partner module rõ ràng hơn nếu cần (Modular Monolith).
- **Clean Code:** Giữ Idempotency, HMAC, Validation ở Application layer, không nhét logic vào Endpoint.

---

## 5. COD & TÀI CHÍNH TỪ GÓC NHÌN SHOP

### 5.1 Tính năng hiện có
- Xem COD amount + status trên từng shipment (PendingCollection / Collected / NotRequired).
- Không có quyền mark collected hay settle (đúng role).

### 5.2 Tính năng cần bổ sung (Production)

| STT | Tính năng | Mức độ cần thiết | Ghi chú |
|-----|-----------|------------------|---------|
| 5.2.1 | **Báo cáo COD tổng hợp** theo khoảng thời gian | Cao | Tổng COD Pending / Collected, theo ngày / tuần. Hỗ trợ đối soát với kế toán Shop |
| 5.2.2 | **Xem trạng thái Settlement** (nếu Admin đã settle) | Trung bình | Shop biết tiền đã được chuyển khoản hay chưa |
| 5.2.3 | **Export báo cáo COD** | Cao | Excel chi tiết từng đơn |
| 5.2.4 | Thông báo khi COD được Collected / Settled | Trung bình | Email / in-app |

**Business Rule cần bảo vệ:**  
Shop chỉ được **xem**, không được thay đổi bất kỳ trạng thái COD nào. Mọi action COD thuộc Shipper / Operator / Admin.

---

## 6. BÁO CÁO, DASHBOARD & GIÁM SÁT

### 6.1 Hiện có
- Dashboard cơ bản (số đơn theo status).

### 6.2 Cần bổ sung (Production)

| STT | Tính năng | Mức độ cần thiết | Production Note |
|-----|-----------|------------------|-----------------|
| 6.2.1 | Dashboard KPI nâng cao | **Rất cao** | Tỷ lệ giao thành công, tỷ lệ hoàn, thời gian trung bình giao hàng, tổng phí, tổng COD. Chart theo ngày/tuần |
| 6.2.2 | Báo cáo hiệu suất giao hàng | Cao | On-time rate, Failed rate theo tỉnh / theo khoảng thời gian |
| 6.2.3 | Cảnh báo đơn bất thường | Trung bình | Đơn PendingPickup quá lâu, DeliveryFailed nhiều lần |
| 6.2.4 | Export toàn bộ dữ liệu theo filter | Cao | Hỗ trợ compliance & kế toán nội bộ Shop |

**Performance note:** Tất cả báo cáo phải dùng query tối ưu hoặc materialized view / summary table nếu volume lớn.

---

## 7. BẢO MẬT, COMPLIANCE & AUDIT (Security-focused)

| STT | Tính năng / Cải tiến | Mức độ cần thiết | Ghi chú |
|-----|----------------------|------------------|---------|
| 7.1 | Ownership check chặt chẽ ở mọi service (chống IDOR) | **Rất cao** | Đã có nhưng cần review toàn bộ |
| 7.2 | Audit log hành động quan trọng của Shop (create, cancel, update profile, rotate key…) | Cao | Bảng `ShopActionLog` hoặc dùng chung pattern AdminAuditLog |
| 7.3 | Rate limiting mạnh hơn cho UI create shipment | Cao | Chống spam tạo đơn |
| 7.4 | Ẩn / mask PII trên UI khi cần (phone, address) | Trung bình | Tuân thủ bảo vệ dữ liệu cá nhân |
| 7.5 | Consent / Privacy policy cho public tracking | Trung bình | Người nhận biết dữ liệu được chia sẻ thế nào |
| 7.6 | Session management & concurrent login control | Trung bình | Bảo mật tài khoản Shop |

---

## 8. CẢI TIẾN KỸ THUẬT THEO 5 TIÊU CHÍ PRODUCTION

### 8.1 Scalability
- Mọi list/query phải hỗ trợ server-side pagination + filtering.
- Bulk import dùng background job, không block request.
- Partner API rate limit chuyển sang Redis / distributed cache.
- Auto-assign và webhook dùng Outbox pattern để đảm bảo consistency khi scale ngang.

### 8.2 Performance
- Index quan trọng: `(ShopId, Status, CreatedAtUtc)`, `(TrackingCode)`, `(ExternalOrderId)`.
- Fee calculation cache FeeRule active.
- Tracking history lazy-load hoặc limit số bản ghi gần nhất.
- Dashboard dùng summary query hoặc cache ngắn hạn.

### 8.3 Maintainability
- Giữ nguyên Clean Architecture + Modular Monolith.
- Tách rõ Command (Create, Cancel, UpdateProfile) và Query (GetList, GetDetail, GetReports).
- FluentValidation cho mọi input.
- Domain rule (cancel condition, ownership) nằm trong Domain / Application, không leak ra UI.

### 8.4 Security
- Mọi endpoint / service kiểm tra `CurrentUser` + `Shop.IsActive` + ownership.
- API Key chỉ hiện một lần, lưu hash.
- Webhook signature + timestamp chống replay.
- Audit đầy đủ các hành động nhạy cảm.
- Không expose internal ID nếu không cần (dùng tracking code / external id).

### 8.5 Clean Code
- Naming nhất quán, tránh magic string.
- Value Objects mạnh (Address, Money, Weight, TrackingCode).
- Không để business logic trong Razor component.
- Test coverage cao cho ownership, cancel rule, fee calculation, Partner API idempotency.

---

## 9. TỔNG KẾT & KHUYẾN NGHỊ ƯU TIÊN TRIỂN KHAI

### 9.1 Nhóm tính năng nên làm ngay (Ưu tiên cao nhất sau khi hoàn thiện Admin)

| Thứ tự | Tính năng | Lý do chính |
|--------|-----------|-------------|
| 1 | Shop Profile Management (xem + sửa hồ sơ) | Hoàn thiện vòng đời tài khoản Shop, cần thiết cho production |
| 2 | Pagination + Filter + Search trên danh sách đơn | Scalability & UX cơ bản nhất khi số đơn tăng |
| 3 | Dashboard KPI nâng cao + Export Excel | Portfolio thể hiện giá trị nghiệp vụ rõ ràng |
| 4 | Webhook Retry Policy + Dead Letter + Dashboard API usage | Đưa Partner API lên mức production-ready |
| 5 | In nhãn vận đơn (Label PDF) | Tính năng thực tế mọi shop logistics đều cần |
| 6 | Bulk Create / Import CSV | Khả năng mở rộng cho shop có volume lớn |
| 7 | Audit log hành động Shop + Ownership hardening | Security & Compliance |

### 9.2 Nhóm tính năng Production nâng cao (có thể làm sau)

- Draft / Edit shipment trước pickup.
- Thông báo real-time (SignalR + Email).
- Báo cáo COD tổng hợp & Settlement visibility.
- IP Whitelist + granular API scope.
- Multi-shop per user (nếu muốn mở rộng mô hình).

### 9.3 Rủi ro nếu không triển khai đầy đủ

- Shop không tự quản lý được hồ sơ → trải nghiệm kém, tăng tải hỗ trợ.
- List đơn không phân trang → performance sụp khi demo với dữ liệu lớn.
- Partner API thiếu retry & monitoring → không thể dùng thực tế với e-commerce lớn.
- Thiếu audit & ownership chặt → rủi ro bảo mật và khó debug khi có sự cố.
- Dashboard yếu → portfolio không thể hiện được giá trị phân tích nghiệp vụ.

---

**Kết thúc tài liệu**

Tài liệu này được thiết kế để **Agent Coding** có thể đọc trực tiếp, hiểu rõ business context của role Shop, và triển khai chính xác các tính năng còn thiếu / cần cải tiến theo chuẩn Production (Scalability – Performance – Maintainability – Security – Clean Code).

Nếu cần chi tiết hơn về bất kỳ tính năng nào (thiết kế Entity, Service interface, UI flow, API contract, hoặc test case), hãy yêu cầu cụ thể để tiếp tục phân tích sâu.

## Current Code Snapshot / Implementation Checklist

Canonical checklist: `docs/roles/Implementation_Checklist_Current_Code.md`.

- Implemented: Shop profile, multi-shop selection/create, create shipment, draft shipment, edit before pickup, submit draft, CSV import preview/confirm, CSV export endpoint, label PDF endpoint, COD report service, KPI service, advanced query filters.
- Implemented but needs hardening: Shop profile audit, shipment create/draft/update/submit/cancel audit, import summary audit, label/export/report/KPI service contracts; dashboard UI should be switched fully to KPI aggregate service, and report UI should be added.
- Remaining: address normalization, import background processing, PII masking policy, richer Excel export, production PDF barcode/QR rendering, and stronger UI rate limits.
