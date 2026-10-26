# Tài liệu Mini Logistics

Tài liệu dành cho developer hiểu và bảo trì **hệ thống hiện tại**. Đọc Backend trước để nắm kiến trúc và nghiệp vụ, sau đó chọn Frontend hoặc Service theo phần đang làm.

## Frontend migration

React + TypeScript + Ant Design là frontend hiện tại; Web host HTTP/static assets và workers, root route ownership theo Phase11. Đọc [kế hoạch ở root](../refactorfe.md), [trạng thái/phạm vi migration](Frontend/react-migration-contracts.md), [ADR-004](Frontend/decisions/adr-004-react-typescript-ant-design.md) và [validation log](Frontend/react-migration-validation.md) khi thực hiện các task. Tài liệu runtime chỉ cập nhật theo phần đã triển khai; các đề xuất và roadmap nằm trong bộ migration này.

## Ba nhóm chính

| Folder | Nội dung và thứ tự đọc |
| --- | --- |
| [Backend](Backend/README.md) | Kiến trúc → [business rules](Backend/business-rules.md) → [persistence](Backend/persistence.md) → [development](Backend/development.md) |
| [Frontend](Frontend/README.md) | Render model, cấu trúc UI, route/actor, luồng tương tác và quy ước giao diện |
| [Service](Service/README.md) | External dependency → [API contract](Service/partner-api.md) → [integration guide](Service/integration-guide.md) → [operations](Service/operations.md) / [security](Service/security.md) |

`Service` chủ yếu nhóm tài liệu liên kết hệ thống ngoài; repository vẫn là modular monolith. Partner API schema và backend examples nằm cùng tài liệu Service; browser OpenAPI và generated TypeScript types được sinh riêng, không thay Partner contract. Load scenarios ở [test/load](../test/load/README.md); migration QA và bằng chứng thực tế ở [validation log](Frontend/react-migration-validation.md). Các manual report đã bị xóa trong working tree không phải nguồn kiểm chứng hiện hành.

## Một chủ đề, một nguồn chính

- Backend sở hữu kiến trúc/nghiệp vụ/dữ liệu; Frontend liên kết đến quy tắc đó thay vì viết lại.
- API reference sở hữu auth/scope/ownership/idempotency/error/signature; integration guide chỉ mô tả cách website Shop áp dụng.
- Operations sở hữu cấu hình/recovery/release checklist; threat model sở hữu phân tích security.
- [OpenAPI](Service/partner-api.openapi.json) sinh từ Web project, không chỉnh tay. Khi public contract đổi, cập nhật reference, [changelog](Service/partner-api-changelog.md), examples và Postman.
- ADR trong `Backend/decisions`, `Frontend/decisions` và `Service/decisions` giữ quyết định/trade-off. Quyết định thay đổi cần ADR mới nêu rõ quan hệ với quyết định cũ; khi thay thế thì ghi supersede, không sửa lịch sử âm thầm.

Source, tests, EF configurations/migrations và generated contract là căn cứ để đối chiếu tài liệu. Hướng dẫn hiện hành đã sửa các mô tả cũ về design-time workers, operation-scoped services, rowversion, capacity reservation và Partner create transaction.

## Bảo trì

Cập nhật đúng tài liệu sở hữu khi behavior/boundary/contract đổi; các trang còn lại chỉ cần liên kết. Dùng đường dẫn source/test để người đọc truy vết, tránh sao chép cây source đầy đủ, schema từng cột, test count, phần trăm tiến độ hoặc roadmap chưa triển khai.

Các báo cáo review/nghiên cứu cũ nằm trong `Backend/history`; báo cáo Partner readiness cũ nằm trong `Service/history`, có nhãn lịch sử. Phần tổng quan và quy tắc phí trùng lặp đã được gộp; production-readiness checklist được gộp vào Operations. [AGENTS.md](../AGENTS.md) và `.agents/` giữ vai trò hướng dẫn agent riêng.
