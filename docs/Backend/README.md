# Backend

Backend chịu trách nhiệm quản lý vận đơn, điều phối, tính phí, COD, phân quyền và dữ liệu. Mini Logistics là **modular monolith .NET 10**: UI, HTTP endpoints và workers chạy trong cùng ứng dụng ASP.NET Core; SQL Server là nguồn dữ liệu chính.

## Đọc theo thứ tự

1. Tài liệu này: kiến trúc và đường đi của một request.
2. [Nghiệp vụ](business-rules.md): trạng thái vận đơn, assignment, phí, COD và quyền truy cập.
3. [Dữ liệu](persistence.md): mapping, transaction, concurrency và cache.
4. [Phát triển local](development.md): cấu hình, chạy, build, test và migration.

UI được mô tả tại [Frontend](../Frontend/README.md); hợp đồng tích hợp và vận hành tại [Service](../Service/README.md).

## Kiến trúc và trách nhiệm

| Project | Trách nhiệm | Project dependency |
| --- | --- | --- |
| `MiniLogistics.Domain` | Entity, value object, trạng thái, công thức phí và invariant | Không có |
| `MiniLogistics.Application` | Use case, DTO, FluentValidation, authorization, repository/service ports | Domain |
| `MiniLogistics.Infrastructure` | EF Core, Identity, repository, cache, bảo vệ secret, external I/O, workers | Application, Domain |
| `MiniLogistics.Web` | Composition root, Razor, endpoints, HTTP mapping, localization | Application, Infrastructure |

Web tham chiếu Infrastructure để cấu hình host và adapter Identity. Component và endpoint nghiệp vụ gọi Application; không truy vấn DbContext trực tiếp. Application tổ chức theo feature/use case, không dùng generic repository hay mediator.

Quyết định và trade-off: [ADR-002: Modular monolith](decisions/adr-002-modular-monolith.md).

## Tìm code theo chức năng

Các đường dẫn dưới đây tính từ repository root.

| Chức năng | Điểm bắt đầu |
| --- | --- |
| Vòng đời vận đơn | `src/MiniLogistics.Domain/Shipments/Shipment.cs` |
| Tạo, draft, sửa trước pickup, trạng thái, import, tracking | `src/MiniLogistics.Application/Shipments/` |
| Điều phối | `Application/Shipments/AutoAssignShipment/`, `AssignmentSelection/`, `AssignShipment/`, `ReassignShipment/` |
| Phân loại tuyến và tính phí | `Application/Routing/`, `Application/Fees/`, `Domain/Fees/` |
| COD | `Application/CashOnDelivery/`, `Domain/CashOnDelivery/` |
| Shop và quyền staff | `Application/Shops/`, `Domain/Shops/` |
| Quản trị | Các feature `Application/Admin*/` |
| Partner API | `Application/PartnerApi/`, `Web/Endpoints/PartnerApiEndpoints.cs` |
| Lưu trữ | `Infrastructure/Persistence/`: context, configurations, repositories, migrations |
| Tác vụ nền | `Infrastructure/Outbox/`, `Infrastructure/PartnerApi/`, import worker trong `Persistence/` |
| Khởi tạo ứng dụng | [Program.cs](../../src/MiniLogistics.Web/Program.cs), `DependencyInjection.cs` của Application và Infrastructure |

Sau hàng đầu tiên, tên `Application/`, `Domain/`, `Infrastructure/`, `Web/` là viết tắt của project tương ứng trong `src/MiniLogistics.*`.

## Luồng chính

**Tạo qua Shop:** validate và xác định quyền Shop → chuẩn hóa địa chỉ → phân loại tuyến → tính phí trên server → tạo Shipment, COD, audit/notification outbox → lưu → thử auto-assignment. Không có shipper phù hợp thì đơn vẫn được tạo ở `PendingPickup`.

**Tạo qua Partner API:** xác thực key/scope → kiểm tra idempotency và Shop → tính tuyến/phí → mở transaction → lưu Shipment, COD, external reference và outbox → thử auto-assignment trong cùng transaction → cập nhật response snapshot → commit. Retry đúng payload trả snapshot đã lưu. Xem [API reference](../Service/partner-api.md).

**Đổi trạng thái:** validate → load aggregate → kiểm tra quyền vận hành hoặc active assignment của Shipper → gọi Domain → ghi history, outbox và audit theo use case → lưu. Xung đột ghi được trả về dạng lỗi nghiệp vụ để caller tải lại dữ liệu.

**Xử lý nền:** transaction nghiệp vụ ghi outbox → worker claim bằng lease → tạo notification/webhook delivery → worker gửi HTTPS đã ký → thành công, retry hoặc dead letter. Xem [ADR-003](../Service/decisions/adr-003-transactional-outbox-and-webhooks.md).

## Lifetime và lỗi

`AddApplication` bọc các service interface bằng `OperationScopedServiceProxy`: mỗi lời gọi async tạo child scope, các lời gọi lồng nhau dùng lại scope của operation. Repository và DbContext trong một operation dùng chung scope; scope được dispose khi Task kết thúc. Blazor circuit có thể sống lâu hơn request, nhưng không vì thế giữ tracked entity/DbContext giữa các lần gọi use case.

FluentValidation kiểm tra input; Domain giữ invariant; Application kiểm tra quyền và orchestration. Lỗi dự kiến dùng `Result`/typed error. Web chuyển lỗi thành thông báo UI hoặc HTTP envelope. Truyền `CancellationToken` và dùng `TimeProvider` cho thời gian nghiệp vụ.

## Phạm vi hiện tại

Hệ thống có Shop portal, dispatch/Shipper, COD nội bộ, tracking công khai, quản trị và Partner API/webhook. Chưa có tích hợp ngân hàng/thanh toán hay API hãng vận chuyển; không cam kết tối ưu tuyến giao xe hoặc exactly-once delivery.

Giới hạn đã đối chiếu: public/Shop UI rate limiter còn dùng memory cache và get/set không atomic; trang tracking chưa truyền phone-last-four dù Application hỗ trợ xác minh. Hành vi UI xem [Frontend](../Frontend/README.md); chi tiết tác nghiệp cho agent xem [known issues](../../.agents/known-issues.md).

## Tài liệu lịch sử

[Review kiến trúc](history/review-code.md) và [nghiên cứu phí](history/fee-study.md) được giữ để tra cứu bối cảnh. Các nhận định, số test và đề xuất trong đó không mô tả trạng thái hiện tại. Khi sửa hệ thống, ưu tiên code, test và migration.
