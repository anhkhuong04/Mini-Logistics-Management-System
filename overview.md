# Mini Logistics Management System - Project Overview

## 1. Định Vị Dự Án

Mini Logistics Management System là hệ thống quản lý giao hàng mini mô phỏng các flow vận hành cốt lõi của một nền tảng logistics như GHN, GHTK hoặc J&T ở mức MVP.

Mục tiêu chính:

- Xây dựng MVP chạy được end-to-end.
- Thể hiện năng lực Fullstack .NET với Blazor Web App.
- Áp dụng Clean Architecture và modular monolith.
- Mô phỏng nghiệp vụ thực tế: tạo đơn, điều phối, giao hàng, COD, tracking.

## 2. Công Nghệ

| Thành phần | Công nghệ |
| --- | --- |
| Backend | .NET 10 |
| Frontend | Blazor Web App |
| Architecture | Clean Architecture + Modular Monolith |
| Database | SQL Server |
| ORM | Entity Framework Core |
| Authentication | ASP.NET Core Identity |
| Validation | FluentValidation |
| Testing | xUnit |

## 3. Role Và Flow Hiện Có

### Shop

Flow end-to-end hiện đã có:

1. Đăng ký hoặc login bằng tài khoản Shop.
2. Tạo shipment.
3. Hệ thống tự tính phí vận chuyển, bảo hiểm, route classification.
4. Theo dõi danh sách đơn và chi tiết đơn.
5. Hủy đơn khi còn trong trạng thái cho phép.
6. Tra cứu tracking code.

Trạng thái: hoàn thành mức demo end-to-end cho flow tạo đơn.

### Operator/Admin

Flow end-to-end hiện đã có:

1. Login bằng tài khoản Operator hoặc Admin.
2. Vào `/operations/assignments`.
3. Xem đơn `PendingPickup` cần phân công.
4. Assign shipper active cho đơn.
5. Theo dõi các đơn đang vận hành: `Assigned`, `PickingUp`, `PickedUp`, `InTransit`, `Delivering`, `DeliveryFailed`.
6. Hỗ trợ update shipment status theo lifecycle rule.
7. Hỗ trợ xác nhận COD khi shipment `Delivered` và COD còn `PendingCollection`.

Trạng thái: hoàn thành mức demo end-to-end cho flow điều phối và hỗ trợ vận hành.

### Shipper

Flow end-to-end hiện đã có:

1. Login bằng tài khoản Shipper.
2. Vào `/shipper/shipments`.
3. Xem các đơn đang được assign active.
4. Update trạng thái theo lifecycle hợp lệ.
5. Khi `DeliveryFailed`, note là bắt buộc.
6. Khi giao thành công và COD còn `PendingCollection`, shipper vẫn thấy đơn để xác nhận COD.
7. Sau khi COD collected hoặc đơn terminal không còn việc cần xử lý, đơn biến mất khỏi workspace chính.

Trạng thái: hoàn thành mức demo end-to-end cho flow giao hàng và COD chính.

### Receiver

Flow hiện có:

1. Mở trang tracking public.
2. Nhập tracking code.
3. Xem trạng thái hiện tại và timeline.

Trạng thái: hoàn thành mức demo cho tracking public.

## 4. Business Rules Đang Được Enforce

### Assignment

- Chỉ Admin/Operator được assign shipper.
- Shop không được assign shipper.
- Target user phải active và có role `Shipper`.
- Chỉ shipment `PendingPickup` mới được assign.
- Mỗi shipment chỉ có một active assignment.

### Status Lifecycle

- Shipper chỉ update shipment thuộc active assignment của mình.
- Admin/Operator có thể hỗ trợ update status.
- Transition sai lifecycle bị chặn.
- Shipment terminal không được update tiếp.
- `DeliveryFailed` bắt buộc có note.

### Assignment Lifecycle Sau Khi Hoàn Tất

Rule đã chốt:

- `Delivered + COD PendingCollection`: chưa deactivate assignment, để shipper còn xác nhận COD.
- `COD Collected`: deactivate active assignment.
- `Delivered + COD NotRequired`: deactivate active assignment ngay.
- `Returned`: deactivate active assignment ngay.
- `Cancelled`: deactivate active assignment ngay.
- Assignment history vẫn được giữ, không xóa dữ liệu.

### COD

- COD transaction được tạo theo COD amount của shipment.
- Chỉ shipment `Delivered` mới được mark COD collected.
- Chỉ COD `PendingCollection` mới được mark collected.
- Assigned shipper, Admin hoặc Operator có thể xác nhận COD theo quyền.
- Sau khi COD collected, active assignment được đóng.

### Fee

- Core fee gần GHN: `TotalFee = BaseFee + ExtraWeightFee + InsuranceFee + ReturnFee`.
- Chargeable weight = max(actual weight, volumetric weight).
- Volumetric weight = `length x width x height / 5000`.
- Insurance fee tự tính theo declared goods value.
- Return fee = 50% x (`BaseFee + ExtraWeightFee`).
- Route được phân loại tự động: `IntraProvince`, `IntraRegion`, `InterRegion`.

## 5. Các Màn Hình Quan Trọng

| Route | Role | Trạng thái |
| --- | --- | --- |
| `/shipments/create` | Shop | Tạo đơn end-to-end |
| `/shipments` | Shop | Danh sách đơn |
| `/shipments/{id}` | Shop | Chi tiết, tracking, cancel khi hợp lệ |
| `/operations/assignments` | Admin/Operator | Assign, tracking vận hành, update status, COD support |
| `/shipper/shipments` | Shipper | Workspace xử lý đơn, update status, COD collect |
| `/tracking` | Public | Tra cứu timeline |

## 6. Tiến Độ Gần Nhất

### Hoàn Thành Trong Đợt Cập Nhật Hôm Nay

1. Dọn workspace shipper:
   - Không hiển thị đơn `Returned` hoặc `Cancelled`.
   - Không hiển thị `Delivered` đã xong COD hoặc không cần COD.
   - Vẫn hiển thị `Delivered + COD PendingCollection` để shipper xác nhận COD.

2. Mở rộng operations UI:
   - `/operations/assignments` không chỉ assign đơn mới mà còn theo dõi đơn đang vận hành.
   - Operator/Admin thấy shipper đang xử lý đơn nào.
   - Có action update status hỗ trợ thủ công.
   - Có action xác nhận COD khi shipment `Delivered` và COD `PendingCollection`.

3. Hoàn thiện status update support:
   - Gắn `IUpdateShipmentStatusService` vào operations UI.
   - Giữ lifecycle rule ở domain/application.
   - Bắt buộc note khi chuyển sang `DeliveryFailed`.

4. Hoàn thiện COD collection support:
   - Gắn `IMarkCodCollectedService` vào shipper workspace và operations UI.
   - Shipper là flow chính xác nhận COD.
   - Admin/Operator là fallback khi cần hỗ trợ.
   - COD collected sẽ deactivate active assignment.

5. Chuẩn hóa UI tiếng Việt ở các page ưu tiên:
   - `NavMenu.razor`
   - `ShipperShipments.razor`
   - `OperationsAssignments.razor`
   - `DEMO-CHECKLIST.md`
   - `overview.md`

6. Bổ sung test business rules:
   - Assign shipper authorization.
   - Target shipper role validation.
   - Status update permission và invalid transition.
   - Required note for `DeliveryFailed`.
   - COD collected rules.
   - Assignment lifecycle sau COD/Returned.
   - Shipper workspace visibility cho `Delivered + COD PendingCollection`.

7. Thêm manual demo checklist:
   - File: `DEMO-CHECKLIST.md`.
   - Bao gồm seed database, login từng role, tạo đơn, assign, giao hàng, COD collected, tracking timeline.

## 7. Verification

Đã chạy:

```powershell
dotnet test Mini-logistics-manegemant-system.slnx
```

Kết quả gần nhất:

- Passed: 14
- Failed: 0
- Skipped: 0

Build web cũng đã pass trước khi bổ sung rule assignment lifecycle mới.

## 8. Trạng Thái End-To-End Theo Role

| Role | Mức hoàn thành | Ghi chú |
| --- | --- | --- |
| Shop | Hoàn thành demo E2E | Tạo đơn, xem đơn, tracking, cancel khi hợp lệ |
| Operator/Admin | Hoàn thành demo E2E | Assign, theo dõi vận hành, update status, COD support |
| Shipper | Hoàn thành demo E2E | Nhận đơn, update status, giao hàng, xác nhận COD |
| Receiver/Public | Hoàn thành demo tracking | Tra cứu timeline bằng tracking code |

Kết luận: code đã đạt mục tiêu demo chính. Việc còn lại là chạy manual browser checklist để xác nhận UI thực tế sau khi seed database.

## 9. Task Tiếp Theo Sau Khi Hoàn Thành Task Cũ

### Ưu Tiên 1: Manual Demo Validation

1. Chạy seed database:

```powershell
dotnet run --project src/MiniLogistics.Web -- --seed
```

2. Chạy app:

```powershell
dotnet run --project src/MiniLogistics.Web
```

3. Thực hiện checklist trong `DEMO-CHECKLIST.md`.
4. Ghi lại lỗi UI hoặc lỗi lifecycle nếu phát sinh.

### Ưu Tiên 2: Polish UI Cho Demo

- Kiểm tra responsive của operations table.
- Rà thêm tiếng Việt ở các page cũ chưa ưu tiên.
- Làm rõ các message lỗi nghiệp vụ cho người demo.

### Ưu Tiên 3: Test Bổ Sung Sau Demo

- Test query operations shipments.
- Test shipper workspace không hiện `Delivered + COD Collected` sau khi service mark COD collected.
- Test cancel shipment deactivate assignment nếu đơn đã assigned nhưng chưa pickup.

### Ưu Tiên 4: Hardening Sau MVP Demo

- Bổ sung màn hình quản lý user/shipper nếu cần demo Admin đầy đủ hơn.
- Bổ sung audit display cho người cập nhật trạng thái trong tracking history.
- Bổ sung COD settlement flow cho Admin nếu muốn đi xa hơn collected.
