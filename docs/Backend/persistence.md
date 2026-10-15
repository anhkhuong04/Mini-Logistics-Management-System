# Dữ liệu, transaction và concurrency

SQL Server Code First là nguồn schema. [MiniLogisticsDbContext](../../src/MiniLogistics.Infrastructure/Persistence/MiniLogisticsDbContext.cs) chứa Identity và dữ liệu nghiệp vụ; mapping nằm trong `Persistence/Configurations`, lịch sử schema nằm trong `Persistence/Migrations` của Infrastructure.

## Nhóm dữ liệu

| Nhóm | Entity chính | Quan hệ |
| --- | --- | --- |
| Identity/Shop | User, Role, Shop, ShopStaffMembership, preferences, notifications | Owner và quyền staff theo Shop |
| Vận đơn | Shipment, assignment, status history, delivery proof, import batch/row | History/assignment thuộc shipment; row thuộc batch |
| COD/phí | CodTransaction, FeeRule, RouteRegionConfig | COD 1–1 shipment; cấu hình có version |
| Điều phối | Hub, ShipperWorkingArea | Phạm vi hoạt động của Shipper |
| Partner | ApiClient, ExternalShipmentReference, WebhookEndpoint/Delivery, audits, integration scopes | Client thuộc Shop; reference và idempotency thuộc client |
| Reliability/audit | OutboxMessage, AdminAuditLog | Queue bền vững và dấu vết thay đổi |

Không sao chép toàn bộ cột vào tài liệu. Khi thay đổi schema, đọc configuration, migration và snapshot tương ứng để xác định type, precision, length, foreign key, delete behavior và index.

## Bảo đảm quan trọng

- Tracking code unique; một active assignment/đơn với filtered unique index `UnassignedAtUtc IS NULL`; một COD transaction/đơn.
- `(ApiClientId, IdempotencyKey)` và `(ApiClientId, ExternalOrderId)` unique. Key API chỉ lưu hash; hash unique.
- Import row number unique trong batch; membership/preferences có ràng buộc compound.
- Shipment lưu địa chỉ/phí và ID/version route/fee đã áp dụng. Reference lưu request hash/response snapshot; webhook delivery lưu secret snapshot được bảo vệ cùng version.
- Shipment, COD, outbox và webhook delivery có SQL rowversion. Không suy rộng rằng mọi entity đều có concurrency token.
- Route configuration unique version theo province; fee rule unique version theo route type và filtered unique active index. Admin write dùng SQL application lock trong transaction trước khi tăng version/đổi active.

## Transaction theo use case

Repository trong một operation chia sẻ DbContext. Một `SaveChangesAsync` là atomic; nhiều lần save cần kiểm tra explicit transaction.

| Luồng | Boundary hiện tại |
| --- | --- |
| Shop create | Lưu Shipment, COD, audit/notification outbox; auto-assignment là bước lưu tiếp theo |
| Partner create | Một explicit transaction bao gồm create, reference/outbox, auto-assignment và final response snapshot |
| Status/COD | Mutation, history và side effect được track trước save; rowversion chặn ghi đè đồng thời |
| Import processing | Processor dùng explicit transaction để cập nhật batch và xử lý rows |
| Assignment | Khóa dòng Shipper trong transaction, kiểm tra load, lưu assignment/outbox rồi mới giải phóng |
| Route/fee admin write | Application lock → deactivate → thêm version → lưu audit/config → commit → invalidate cache |
| Shop registration | Explicit transaction bao gồm Identity user, role và Shop; lỗi trước commit rollback toàn bộ |

Không gọi external HTTP trong transaction. Các workflow Identity/admin khác vẫn cần đọc use case và transaction cụ thể trước khi giả định atomicity.

## Xử lý race

[ShipperAssignmentCapacityGuard](../../src/MiniLogistics.Infrastructure/Persistence/ShipperAssignmentCapacityGuard.cs) khóa `AspNetUsers` bằng `UPDLOCK, HOLDLOCK, ROWLOCK` và đọc capacity/load trong transaction. Guard join transaction sẵn có ở Partner create; auto-assignment có tối đa ba lần thử, manual/reassign từ chối khi đầy.

Partner create chỉ map hai unique-index conflict của external reference. Request thua rollback, xóa tracking và load reference thắng để replay hoặc trả conflict; không biến mọi lỗi SQL unique thành lỗi idempotency.

Repository chuyển lỗi rowversion thành `ConcurrencyConflictException`; Application trả `Application.ConcurrencyConflict`, Web hướng caller reload/retry. Không tự retry mutation đã stale.

Outbox/delivery dùng atomic SQL claim, lease expiry và rowversion để nhiều worker cùng chạy; consumer vẫn phải xử lý duplicate. Cơ chế chi tiết tại [ADR-003](../Service/decisions/adr-003-transactional-outbox-and-webhooks.md).

## Cache cấu hình

Route, fee và hub giữ local snapshot, đối chiếu shared generation trên Redis khi dùng Redis mode. Sau commit, writer invalidate cache. Redis lỗi thì đọc SQL trực tiếp; publish invalidation thất bại được log và giới hạn độ cũ bởi `ConfigurationCache:ConsistencyWindowSeconds` (mặc định 15 giây).

`ConfigurationCache:KeyPrefix` phải riêng cho môi trường và chung giữa các node. Public/Shop UI rate limiter hiện chưa có cơ chế atomic đa instance; không coi `IDistributedCache` backed by memory là Redis.

## Kiểm chứng và migration

Bằng chứng tại `test/MiniLogistics.Infrastructure.Tests/`: `ConcurrencyAndRegistrationTransactionTests`, `SystemConfigurationConcurrencyTests`, `RedisConfigurationCacheIntegrationTests`, `WorkerLeasePersistenceTests`. Integration SQL cần Windows LocalDB; Redis integration dùng `MINILOGISTICS_TEST_REDIS` khi được cấu hình.

Tạo migration bằng EF tooling theo [Development](development.md), review migration/designer/snapshot cùng nhau. Không viết lại migration đã áp dụng hoặc sửa snapshot thủ công. Migration/seed chỉ chạy khi có flag explicit; deployment quyết định thời điểm áp dụng.
