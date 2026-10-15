# Nghiệp vụ và phân quyền

Tài liệu này là nơi duy nhất mô tả các quy tắc nghiệp vụ cho developer. Chi tiết input nằm trong validator; hợp đồng HTTP nằm tại [Service](../Service/partner-api.md).

## Actor và phạm vi dữ liệu

| Actor | Quyền chính | Giới hạn |
| --- | --- | --- |
| Admin | Quản trị, vận hành, đối soát COD | Theo permission của use case |
| Operator | Điều phối, trạng thái, proof, thu COD | Không settlement COD hoặc xem admin audit |
| Shop owner/staff | Vận đơn, import/export, COD report, hồ sơ, nhân viên, tích hợp | Shop hiện tại; staff theo membership và `ShopPermission` |
| Shipper | Xử lý đơn được giao, ghi kết quả và thu COD | Active assignment thuộc Shipper, đúng action/trạng thái |
| IntegrationAdmin | Quản lý tích hợp | Scope global, Shop hoặc province được cấp |
| API client | Quote, create, track, cancel | Shop của client và `PartnerApiScope`; ghi/idempotency theo client |
| Public | Tra cứu bằng tracking code | Projection an toàn; chi tiết theo mức xác minh |

Identity role chỉ là lớp kiểm tra đầu. Application phải xác minh user/Shop active, membership, permission và quyền trên dữ liệu đích; không tin ShopId/role do caller cung cấp. Tracking khác Shop không tiết lộ sự tồn tại của đơn.

Nguồn: [OperationPermissions](../../src/MiniLogistics.Application/Authorization/OperationPermissions.cs), [ShopPermission](../../src/MiniLogistics.Domain/Shops/ShopPermission.cs), [PartnerApiScope](../../src/MiniLogistics.Domain/PartnerApi/PartnerApiScope.cs).

## Vòng đời vận đơn

| Từ | Được chuyển đến |
| --- | --- |
| Draft | PendingPickup, Cancelled |
| PendingPickup | Assigned, Cancelled |
| Assigned | PickingUp, Cancelled |
| PickingUp | PickedUp, Cancelled |
| PickedUp | InTransit, Returned |
| InTransit | Delivering, Returned |
| Delivering | Delivered, DeliveryFailed, Returned |
| DeliveryFailed | Delivering, Returned |

`Delivered`, `Returned`, `Cancelled` là trạng thái kết thúc. `DeliveryFailed` bắt buộc có note. Mỗi lần tạo/chuyển trạng thái ghi history với actor và thời điểm; không sửa trực tiếp `Status`.

Shop có thể lưu draft, submit và sửa thông tin trước pickup qua các use case riêng. Tạo trực tiếp cho ra `PendingPickup`, có thể trở thành `Assigned` sau auto-assignment. Public/Partner timeline dùng safe mapper, không lấy internal note làm thông báo.

Nguồn: [Shipment](../../src/MiniLogistics.Domain/Shipments/Shipment.cs). Bằng chứng: `test/MiniLogistics.Domain.Tests/` và `test/MiniLogistics.Application.Tests/ShipmentBusinessRuleTests.cs`.

## Assignment

- Assign bắt đầu từ `PendingPickup`; mỗi đơn tối đa một active assignment.
- Reassign hoặc hủy assignment chỉ khi `Assigned`; lịch sử phân công được giữ lại.
- Auto-assignment xét tỉnh pickup, hub/working area, user active/available và `MaxActiveShipments`. Không có ứng viên là kết quả hợp lệ.
- Manual assignment cho phép override khu vực với cảnh báo, vẫn kiểm tra quyền và capacity. Capacity được reserve trong transaction; xem [Persistence](persistence.md).
- `Returned`/`Cancelled` kết thúc active assignment. `Delivered` không COD cũng kết thúc ngay; có COD pending thì giữ assignment đến khi thu tiền hoàn tất.

## Tuyến và phí

`IntraProvince`: cùng tỉnh; `IntraRegion`: khác tỉnh, cùng vùng; `InterRegion`: khác vùng. Phân loại dùng route-region configuration active, không dùng bảng vùng hard-code làm nguồn chính.

```text
volumetricKg = lengthCm × widthCm × heightCm / 5000
chargeableKg = max(actualKg, volumetricKg)
extraBlocks = ceil(max(0, chargeableKg - baseWeightKg) / extraWeightStepKg)
extraFee = extraBlocks × extraStepFee
totalFee = baseFee + extraFee + insuranceFee + returnFee
```

`ShippingFeeCalculator` chọn rule active đầu tiên phù hợp route và khoảng cân. `FeeRule` giữ base/step, version và policy bảo hiểm/hoàn hàng. Bảo hiểm bằng 0 khi giá trị khai báo dưới ngưỡng miễn; ngược lại bằng `min(goodsValue, maximumValue) × rate`. Các mặc định Domain là 1.000.000 VND, 20.000.000 VND và 0,5%; giá trị rule được lưu mới là policy áp dụng.

Lúc tạo đơn `returnFee = 0`. Khi `Returned`, phí hoàn bằng `(baseFee + extraFee) × returnFeeRate` đã snapshot trên đơn. Không lấy rule mới để tính lại phí đơn cũ. Shipment lưu breakdown, cân tính phí, route và ID/version cấu hình đã dùng; sửa đơn trước pickup được xử lý qua use case và tính lại rõ ràng.

UI chỉ hiển thị estimate. Application luôn tính lại trước khi lưu. Seed là dữ liệu mẫu, không phải bảng giá cố định.

Nguồn: [FeeRule](../../src/MiniLogistics.Domain/Fees/FeeRule.cs), [ShippingFeeBreakdown](../../src/MiniLogistics.Domain/Fees/ShippingFeeBreakdown.cs), `Application/Fees/`, `Application/Routing/`. Test: [FeeRuleTests](../../test/MiniLogistics.Domain.Tests/FeeRuleTests.cs).

## COD

Mỗi shipment có một `CodTransaction`. COD bằng 0 là `NotRequired`; số dương là `PendingCollection`. Chỉ thu khi shipment `Delivered`. Số thực thu có thể khác số dự kiến; giữ discrepancy và note. Hoàn tất thu tiền kết thúc assignment đang giữ cho COD.

Settlement chỉ từ `Collected`, qua workflow có quyền Admin. Đây là ghi nhận đối soát nội bộ, chưa gọi ngân hàng. Nguồn: [CodTransaction](../../src/MiniLogistics.Domain/CashOnDelivery/CodTransaction.cs), [CodTransactionTests](../../test/MiniLogistics.Domain.Tests/CodTransactionTests.cs).

## Side effect

History, audit và outbox liên quan phải được lưu theo transaction của use case. HTTP webhook chạy ở worker sau commit; không gọi receiver trong transaction nghiệp vụ. Ownership/idempotency và quy tắc consumer nằm tại [API reference](../Service/partner-api.md).
