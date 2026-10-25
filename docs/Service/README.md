# Service — tích hợp và phụ thuộc bên ngoài

Nhóm này mô tả cách Mini Logistics liên kết với hệ thống ngoài và các dịch vụ cần để chạy web. `Service` là nhóm tài liệu, không phải project hoặc microservice mới.

## Tích hợp thực sự trong source

| Thành phần | Hướng kết nối | Vai trò và implementation |
| --- | --- | --- |
| Partner REST API | Backend Shop → Mini Logistics | Quote/create/track/cancel qua `/api/v1/partner`; `Web/Endpoints`, `Application/PartnerApi` |
| Signed webhook | Mini Logistics → receiver HTTPS của partner | Event từ outbox, HMAC, retry; `Infrastructure/PartnerApi` |
| SQL Server | Web/workers → SQL | Identity, dữ liệu nghiệp vụ và queue; `Infrastructure/Persistence` |
| Redis | Web/cache → Redis | Atomic quota cho Partner và shared generation của configuration cache |
| Data Protection storage/certificate | Các replica → shared key ring | Bảo vệ signing secret, giải mã dùng chung giữa các node |
| Google Fonts | Browser → Google Fonts | Font Be Vietnam Pro được tham chiếu trong `Components/App.razor` |

SQL, Redis và Data Protection là dependency hạ tầng; xem [Persistence](../Backend/persistence.md) để hiểu bảo đảm dữ liệu. Dữ liệu địa giới hành chính được đọc từ file JSON local; chưa có API bản đồ/geocoding, ngân hàng hoặc hãng vận chuyển được đăng ký trong source.

## Tài liệu theo nhu cầu

| Nhu cầu | Nguồn chính |
| --- | --- |
| Endpoint, auth, ownership, idempotency, lỗi và chữ ký webhook | [Partner API reference](partner-api.md) |
| Schema request/response chính xác | [Generated OpenAPI](partner-api.openapi.json) |
| Browser Identity/CSRF/reference/Shop context | [Browser OpenAPI](browser-api.openapi.json), [migration contract](../Frontend/react-migration-contracts.md) |
| Gắn trạng thái giao hàng vào website Shop | [Integration guide](integration-guide.md) |
| Cấu hình production, deploy, recovery và release evidence | [Operations](operations.md) |
| Threat, control và security verification | [Threat model](security.md) |
| Versioning/deprecation và thay đổi đã công bố | [Changelog](partner-api-changelog.md) |
| Quyết định production contract | [ADR-001](decisions/adr-001-partner-api-production-contract.md) |
| Quyết định outbox/webhook | [ADR-003](decisions/adr-003-transactional-outbox-and-webhooks.md) |

Ví dụ backend: [Node.js](examples/partner-api-node.mjs), [C#](examples/PartnerApiExample.cs). Công cụ gọi thử: [Postman collection](../../postman/partner-api.postman_collection.json); đặt các biến collection cho môi trường của bạn. Các URL `.example` là placeholder, thay bằng host của môi trường.

## Ranh giới trách nhiệm

Mini Logistics xác thực client, giới hạn quota, giữ Shop/client isolation, commit business/outbox và gửi webhook đã ký. Backend Shop giữ key, authorize customer/order, lưu mapping tracking code, verify webhook, deduplicate và reconcile. Browser của partner gọi backend Shop; UI React nội bộ Mini Logistics dùng same-origin Identity cookie và browser API riêng, không dùng Partner API key.

API reference sở hữu hợp đồng; guide chỉ mô tả cách áp dụng; runbook sở hữu cấu hình/vận hành; threat model sở hữu phân tích rủi ro. Không sao chép bảng scope/lỗi/chữ ký sang từng tài liệu. OpenAPI sinh từ Web project; khi hợp đồng đổi, cập nhật reference, changelog và examples cùng artifact theo [Development](../Backend/development.md).

Code/test không phải bằng chứng production đã được chứng nhận. Checklist và evidence nằm trong [Operations](operations.md). [Báo cáo 2026-08-02](history/production-readiness-2026-08-02.md) chỉ lưu lịch sử.
