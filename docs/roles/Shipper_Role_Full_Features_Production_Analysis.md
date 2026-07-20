# PHÂN TÍCH TOÀN BỘ TÍNH NĂNG CẦN CÓ CỦA ROLE SHIPPER
## Dự án Mini Logistics Management System

**Phiên bản phân tích:** 1.0  
**Ngày phân tích:** 20/07/2026  
**Mục đích:** Cung cấp tài liệu đầy đủ cho Agent Coding đọc, phân tích và triển khai nhằm đưa role Shipper từ mức MVP/demo hiện tại lên gần chuẩn Production thực tế (enterprise logistics), phục vụ mục tiêu portfolio chất lượng cao.  
**Nguồn tham chiếu:** shipper.md, overview.md, README.md, Admin_Role_Full_Features_Production_Analysis.md, operator.md, shop.md, Shop_Role_Full_Features_Production_Analysis.md  
**Tiêu chí đánh giá Production:** Scalability, Performance, Maintainability, Security, Clean Code (Clean Architecture + Modular Monolith)

---

## 1. MỤC TIÊU CỦA TÀI LIỆU

Tài liệu này liệt kê **toàn bộ tính năng** mà role `Shipper` cần có trong dự án Logistics, bao gồm:

- Tính năng **đã triển khai** ở mức MVP/demo hiện tại.
- Tính năng **cần bổ sung hoặc cải tiến** để đạt chuẩn Production thực tế của các nền tảng logistics lớn (GHN, GHTK, J&T, SPX…).
- Business rules quan trọng phải được enforce chặt chẽ ở Domain/Application layer.
- Production considerations theo 5 tiêu chí cốt lõi: Scalability, Performance, Maintainability, Security, Clean Code.
- Use case / luồng chính, rủi ro vận hành nếu thiếu, và khuyến nghị ưu tiên triển khai.

Phân loại theo **domain nghiệp vụ thực tế** khi nhìn từ góc độ người giao hàng (last-mile shipper / rider).

---

## 2. WORKSPACE & VISIBILITY ĐƠN HÀNG (Shipper Workspace)

### 2.1 Tính năng hiện có

| STT | Tính năng | Trạng thái | Use case chính | Business Rule quan trọng | Production Note |
|-----|-----------|------------|----------------|---------------------------|-----------------|
| 2.1.1 | Xem danh sách đơn được assign active | Đã có | `/shipper/shipments` + `GetAssignedShipmentsForShipperService` | Chỉ lấy shipment có active assignment của chính `shipperUserId`. Không hiển thị `Returned`, `Cancelled`. `Delivered` chỉ hiện khi COD còn `PendingCollection` | Rule visibility đã đúng nghiệp vụ |
| 2.1.2 | Xem thông tin chi tiết đơn trong workspace | Đã có | Cùng page | Tracking history, địa chỉ, COD, fee | - |
| 2.1.3 | Xem Working Areas + Capacity + Active Load hiện tại | Đã có | `GetShipperWorkingAreasService` | Shipper chỉ xem được của chính mình. Admin mới được xem của người khác | Read-only đúng role |

### 2.2 Tính năng cần bổ sung / cải tiến (Production)

| STT | Tính năng | Mức độ cần thiết | Use case chính | Business Rule quan trọng | Production Considerations |
|-----|-----------|------------------|----------------|---------------------------|---------------------------|
| 2.2.1 | **Filter + Sort + Search** trong workspace | **Rất cao** | Lọc theo status (Assigned/PickingUp/…), COD amount, khoảng cách, thời gian tạo | Chỉ trên tập đơn active của chính shipper | **Performance:** Index `(ShipperUserId, Status, AssignedAt)`. Server-side filter |
| 2.2.2 | **Phân nhóm / Tab theo giai đoạn** (Pickup / In-transit / Delivery / COD Pending) | Cao | Giúp shipper ưu tiên công việc theo flow thực tế | - | UX quan trọng với volume cao |
| 2.2.3 | **Hiển thị khoảng cách / ETA ước tính** (nếu có GPS) | Cao | Ưu tiên đơn gần nhất | Chỉ tính toán, không thay đổi assignment | Cần tích hợp map service (Google/Mapbox) – cân nhắc cost |
| 2.2.4 | **Batch view / Multi-select** để update status hàng loạt (khi hợp lệ) | Trung bình | Khi shipper lấy nhiều đơn cùng lúc tại 1 điểm | Chỉ cho phép transition hợp lệ trên tất cả đơn được chọn | Cẩn thận concurrency |
| 2.2.5 | **Pull-to-refresh + Real-time update** (SignalR) | Cao | Đơn mới được assign hiện ngay không cần F5 | - | **Scalability:** SignalR group theo shipperId |

**Business Rules quan trọng cần bảo vệ:**
- Shipper **tuyệt đối không** thấy đơn không có active assignment của mình.
- Sau khi COD Collected hoặc đơn chuyển sang terminal (`Returned`/`Cancelled`/`Delivered + COD NotRequired`), đơn phải biến mất khỏi workspace chính ngay lập tức.
- Active load chỉ đếm các status: `Assigned`, `PickingUp`, `PickedUp`, `InTransit`, `Delivering`, `DeliveryFailed`.

---

## 3. CẬP NHẬT TRẠNG THÁI ĐƠN HÀNG (Status Lifecycle)

### 3.1 Tính năng hiện có

| STT | Tính năng | Trạng thái | Use case chính | Business Rule quan trọng | Production Note |
|-----|-----------|------------|----------------|---------------------------|-----------------|
| 3.1.1 | Update status theo lifecycle hợp lệ | Đã có | `UpdateShipmentStatusService` | Shipper chỉ update được đơn có active assignment của mình. Admin/Operator được support | Domain rule đã chặn invalid transition |
| 3.1.2 | Bắt buộc note khi `DeliveryFailed` | Đã có | Domain enforce | Note không được null/empty | Đúng nghiệp vụ |
| 3.1.3 | Deactivate assignment đúng thời điểm | Đã có | Khi `Returned`, `Cancelled`, `Delivered + COD=0`, hoặc sau `MarkCodCollected` | - | Rule lifecycle assignment đã solid |

### 3.2 Tính năng cần bổ sung / cải tiến (Production)

| STT | Tính năng | Mức độ cần thiết | Ghi chú Production theo tiêu chí |
|-----|-----------|------------------|----------------------------------|
| 3.2.1 | **Proof of Pickup / Proof of Delivery (POD)** | **Rất cao** | Ảnh chụp hàng + chữ ký người nhận / OTP xác nhận. Lưu vào storage (Azure Blob/S3). **Security + Compliance** cực kỳ quan trọng với logistics thực tế |
| 3.2.2 | **GPS Check-in tại điểm lấy / giao** | Cao | Ghi nhận tọa độ khi chuyển sang `PickedUp` / `Delivered`. Chống gian lận | Cần mobile app hoặc PWA có quyền location |
| 3.2.3 | **Lý do thất bại chuẩn hóa (Failure Reason Code)** | Cao | Thay vì chỉ free-text note, có dropdown: Người nhận không có nhà, Sai địa chỉ, Từ chối nhận, Hàng hư hỏng… + note bổ sung | Báo cáo và AI phân tích sau này dễ hơn |
| 3.2.4 | **Retry Delivery với lịch hẹn lại** | Trung bình | Khi `DeliveryFailed` → cho phép chọn thời gian giao lại (nếu business cho phép) | Cần mở rộng domain status hoặc metadata |
| 3.2.5 | **Partial Delivery / Multi-package** (nếu mở rộng sau) | Thấp (tương lai) | Hiện tại 1 shipment = 1 kiện | Chỉ note để biết scope |

**Business Rules quan trọng cần bảo vệ / mở rộng:**
- Chỉ cho phép transition đúng lifecycle (đã có).
- `DeliveryFailed` bắt buộc có note (và nên có reason code).
- Terminal status không cho update tiếp.
- Mọi thay đổi status phải ghi `StatusHistory` với actor + timestamp + note/GPS/ảnh (nếu có).
- Khi chuyển sang `Returned` → apply return fee + deactivate assignment ngay.

**Production Considerations:**
- **Security:** Ảnh POD phải có watermark thời gian + tọa độ, không cho phép upload ảnh cũ.
- **Performance:** Upload ảnh dùng pre-signed URL, không đi qua application server.
- **Maintainability:** Tách `IProofOfDeliveryService`, không nhét logic lưu file vào `UpdateShipmentStatusService`.
- **Clean Code:** Value Object cho `DeliveryProof` (Images, Signature, Otp, GpsCoordinate, CapturedAt).

---

## 4. THU HỘ COD (Cash on Delivery Collection)

### 4.1 Tính năng hiện có

| STT | Tính năng | Trạng thái | Use case chính | Business Rule quan trọng |
|-----|-----------|------------|----------------|---------------------------|
| 4.1.1 | Xác nhận COD Collected | Đã có | `MarkCodCollectedService` | Chỉ khi shipment = `Delivered` và COD = `PendingCollection`. Shipper là flow chính, Admin/Operator là fallback | Sau khi collected → deactivate active assignment |

### 4.2 Tính năng cần bổ sung / cải tiến (Production)

| STT | Tính năng | Mức độ cần thiết | Production Considerations |
|-----|-----------|------------------|---------------------------|
| 4.2.1 | **Xác nhận số tiền thực tế thu được** | **Rất cao** | Shipper nhập số tiền thực tế (có thể khác declared nếu có vấn đề). Ghi discrepancy nếu lệch | **Business critical** – tránh thất thoát |
| 4.2.2 | **Cash Handover Workflow** (Shipper → Hub / Kế toán) | Cao | Sau khi collected, shipper phải bàn giao tiền mặt tại hub. Có trạng thái `PendingHandover` → `HandedOver` | Hiện chỉ có `MarkCodSettled` ở Admin, thiếu flow trung gian |
| 4.2.3 | **In biên lai thu COD** (PDF / ảnh) | Cao | Gửi cho người nhận + lưu lại cho shipper | Compliance |
| 4.2.4 | **Cảnh báo COD pending quá lâu** | Trung bình | Đơn Delivered + COD PendingCollection > X giờ | Giúp shipper không quên |
| 4.2.5 | **Tổng hợp COD trong ngày của shipper** | Cao | Dashboard nhỏ: Tổng phải thu, đã thu, còn pending | Hỗ trợ đối soát cuối ngày |

**Business Rules quan trọng:**
- Shipper **không** được settle COD (chỉ Admin).
- Chỉ được mark collected trên đơn mình đang active assignment.
- Sau `MarkCodCollected` → assignment deactivate ngay, đơn biến mất khỏi workspace.
- Mọi thao tác COD phải audit (ai, lúc nào, số tiền).

---

## 5. WORKING AREA, CAPACITY & AVAILABILITY

### 5.1 Tính năng hiện có
- Xem working areas, `IsAvailableForAssignment`, `MaxActiveShipments`, active load hiện tại (read-only).

### 5.2 Tính năng cần bổ sung (Production)

| STT | Tính năng | Mức độ cần thiết | Ghi chú |
|-----|-----------|------------------|---------|
| 5.2.1 | **Self Check-in / Check-out ca làm việc** | Cao | Shipper tự bật/tắt `IsAvailableForAssignment` theo ca. Admin vẫn override được | Thực tế vận hành rất cần |
| 5.2.2 | **Xem lịch sử thay đổi capacity / area** (read-only) | Trung bình | Minh bạch khi bị Admin điều chỉnh | - |
| 5.2.3 | **Cảnh báo sắp đạt MaxActiveShipments** | Trung bình | UX tốt | - |
| 5.2.4 | **Capacity thông minh hơn** (theo weight/volume/zone) | Thấp → Trung bình (tương lai) | Hiện chỉ đếm số đơn. Production thực tế cần phức tạp hơn | Cần redesign model nếu làm |

**Lưu ý quan trọng:**  
Shipper **không** được tự ý thay đổi Working Area hay MaxActiveShipments. Đó là quyền của Admin (đã phân tích ở Admin document). Chỉ cho phép self-toggle availability nếu business chấp nhận.

---

## 6. MOBILE / PWA & TRẢI NGHIỆM THỰC TẾ

Đây là khoảng trống lớn nhất so với logistics production.

| STT | Tính năng | Mức độ cần thiết | Lý do Business + Kỹ thuật |
|-----|-----------|------------------|---------------------------|
| 6.1 | **Mobile-first / PWA hoàn chỉnh** | **Rất cao** | Shipper chủ yếu dùng điện thoại ngoài đường. Desktop workspace hiện tại không đủ dùng thực tế |
| 6.2 | **Offline support cơ bản** | Cao | Lưu draft status update / ảnh khi mất mạng, sync lại khi có mạng | Service Worker + IndexedDB |
| 6.3 | **Tích hợp bản đồ + chỉ đường** | Cao | Mở Google Maps / Apple Maps với địa chỉ giao | Deep link đơn giản đã tốt ở giai đoạn đầu |
| 6.4 | **Push Notification** khi có đơn mới được assign | Cao | Quan trọng để shipper phản ứng nhanh | Firebase / OneSignal / native |
| 6.5 | **Camera + Upload ảnh tối ưu** | Cao | Cho POD. Compress ảnh trước khi upload | Performance trên mạng 3G/4G |

**Khuyến nghị kiến trúc:**  
Giữ Blazor Web App hiện tại cho Admin/Operator/Shop. Với Shipper nên cân nhắc:
- Blazor Hybrid (MAUI) hoặc
- PWA mạnh + responsive cực tốt, hoặc
- Tách mobile app riêng (React Native / Flutter) gọi cùng Application API nếu scale lớn.

Ở giai đoạn portfolio hiện tại: **ưu tiên PWA + responsive hoàn hảo + camera + location** là đủ thuyết phục.

---

## 7. BÁO CÁO & HIỆU SUẤT CÁ NHÂN (Shipper Performance)

| STT | Tính năng | Mức độ cần thiết | Ghi chú |
|-----|-----------|------------------|---------|
| 7.1 | Dashboard cá nhân: số đơn hoàn thành hôm nay / tuần, tỷ lệ thành công, COD đã thu | Cao | Động lực + đối soát |
| 7.2 | Xem lịch sử đơn đã hoàn thành (kể cả terminal) | Trung bình | Hiện workspace ẩn đi, cần có trang “Lịch sử” riêng |
| 7.3 | Thống kê theo lý do DeliveryFailed | Trung bình | Giúp shipper tự cải thiện |

---

## 8. BẢO MẬT, AUDIT & COMPLIANCE

| STT | Tính năng / Cải tiến | Mức độ cần thiết | Ghi chú |
|-----|----------------------|------------------|---------|
| 8.1 | Mọi action (status update, COD collect) phải ghi Audit đầy đủ (actor, timestamp, IP, device, GPS nếu có) | **Rất cao** | Tránh tranh chấp |
| 8.2 | Chống gian lận POD (ảnh cũ, tọa độ giả) | Cao | Watermark + server-side validation tọa độ gần địa chỉ giao |
| 8.3 | Session / Device management | Trung bình | Shipper có thể bị khóa nếu phát hiện bất thường |
| 8.4 | Rate limit các action nhạy cảm (COD collect, status update liên tục) | Trung bình | - |

---

## 9. CẢI TIẾN KỸ THUẬT THEO 5 TIÊU CHÍ PRODUCTION

### 9.1 Scalability
- Query workspace phải cực nhanh (index đúng, chỉ lấy active assignment).
- SignalR scale bằng Redis backplane khi có nhiều shipper online.
- Upload ảnh POD dùng object storage + CDN.

### 9.2 Performance
- Workspace load < 300ms ngay cả khi shipper có 30–50 đơn active.
- Ảnh compress phía client trước khi upload.
- Không load full tracking history nếu không cần (lazy).

### 9.3 Maintainability
- Giữ nguyên Clean Architecture.
- Tách rõ `IShipperWorkspaceQuery`, `IUpdateShipmentStatusService`, `IMarkCodCollectedService`, `IProofOfDeliveryService`.
- Domain event khi status thay đổi / COD collected để dễ mở rộng (webhook, notification, analytics).

### 9.4 Security
- Ownership check nghiêm ngặt ở Application layer (shipper chỉ đụng được assignment của mình).
- Không tin tưởng client gửi status / số tiền COD.
- Audit mọi thao tác tài chính (COD).

### 9.5 Clean Code
- Value Objects mạnh: `GpsCoordinate`, `DeliveryProof`, `FailureReason`.
- Không để if-else lifecycle dài trong service → dùng State pattern hoặc explicit transition methods trong Domain (`Shipment.MarkPickedUp()`, `Shipment.MarkDelivered(proof)`…).
- Test coverage cao cho visibility rule, lifecycle, COD deactivate assignment.

---

## 10. TỔNG KẾT & KHUYẾN NGHỊ ƯU TIÊN TRIỂN KHAI

### 10.1 Nhóm tính năng nên làm ngay (Ưu tiên cao nhất)

| Thứ tự | Tính năng | Lý do chính (Business + Portfolio) |
|--------|-----------|------------------------------------|
| 1 | **Proof of Delivery (ảnh + chữ ký / OTP + GPS)** | Đây là tính năng “make or break” của logistics thực tế. Thiếu POD thì hệ thống chỉ là demo |
| 2 | **Mobile-first / PWA hoàn chỉnh + Camera + Location** | Shipper dùng điện thoại 95% thời gian. Desktop-only không thuyết phục |
| 3 | **Filter / Tab / Search trong Workspace** | UX cơ bản khi số đơn tăng |
| 4 | **Xác nhận số tiền COD thực tế + tổng hợp COD trong ngày** | Giảm thất thoát, thể hiện hiểu biết tài chính vận hành |
| 5 | **Failure Reason Code chuẩn hóa** | Dữ liệu sạch cho báo cáo và cải tiến sau này |
| 6 | **Self Check-in / Check-out availability** | Gần với ca làm việc thực tế |
| 7 | **Lịch sử đơn đã hoàn thành + Dashboard cá nhân** | Động lực + đối soát |

### 10.2 Nhóm tính năng Production nâng cao (làm sau)

- Cash Handover Workflow đầy đủ (Shipper → Hub → Accounting).
- Offline-first support.
- Capacity theo weight/volume/zone.
- Route optimization / sequencing (rất phức tạp, chỉ nên làm khi đã solid core).
- Push notification nâng cao.

### 10.3 Rủi ro nếu không triển khai đầy đủ

- Không có POD → không thể giải quyết tranh chấp “đã giao / chưa giao” → mất niềm tin.
- Workspace chỉ dùng được trên desktop → không thể demo thực tế với shipper.
- Thiếu xác nhận số tiền COD → rủi ro thất thoát tiền mặt cao.
- Không có mobile experience → portfolio trông như hệ thống nội bộ văn phòng, không phải logistics platform thực thụ.

---

**Kết thúc tài liệu**

Tài liệu này được thiết kế để **Agent Coding** có thể đọc trực tiếp, hiểu sâu business context + technical debt của role Shipper, và triển khai chính xác các tính năng còn thiếu theo chuẩn Production enterprise.

**Điểm nhấn quan trọng nhất cần nhớ:**  
Role Shipper là “last-mile execution”. Mọi thiếu sót ở đây (đặc biệt POD, mobile, COD accuracy) sẽ làm giảm nghiêm trọng độ tin cậy của toàn bộ hệ thống trong mắt nhà tuyển dụng / reviewer portfolio.

Nếu cần đi sâu bất kỳ hạng mục nào (thiết kế Entity `DeliveryProof`, flow PWA, API contract cho mobile, test strategy, hoặc so sánh với cách GHN/GHTK làm thực tế), hãy yêu cầu cụ thể để tôi phân tích tiếp.

## Current Code Snapshot / Implementation Checklist

Canonical checklist: `docs/roles/Implementation_Checklist_Current_Code.md`.

- Implemented: active assignment visibility, server-side search/tabs/paging, status lifecycle update, COD collection, COD daily summary, self availability toggle, working area/capacity read-only support.
- Implemented but needs hardening: failure reason code is standardized, COD actual/discrepancy is persisted, optional GPS fields exist on status history, POD metadata service/table exists, status/COD audit actions are split by Shipper/Operator/Admin.
- Remaining: full mobile geolocation button, camera/object-storage upload, proof gallery UI, PWA/offline support, and stricter Delivered requires POD rollout via feature flag.
