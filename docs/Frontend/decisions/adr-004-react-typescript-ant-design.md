# ADR-004: React + TypeScript + Ant Design cho frontend

- Ngày: 25/10/2026.
- Trạng thái quyết định: **Accepted** cho React + TypeScript + Ant Design theo lựa chọn của người dùng.
- Trạng thái triển khai: Phase 0 có inventory/baseline/contract/pins; Phase 1–3 có hosting opt-in `/app`, theme/layouts/locale, browser auth/CSRF/session/Shop context, typed transport, Shop dashboard và shipment list/detail. Mutation/API còn lại theo Phase 4+; Blazor được giữ trong coexist. Icon dùng thư viện `@ant-design/icons` đã pin.
- Phạm vi: lựa chọn frontend; chi tiết hosting/auth/toolchain đã được người thực hiện chốt theo kế hoạch trong T00.02 và được ghi bên dưới.

## Bối cảnh

UI hiện nằm trong MiniLogistics.Web và dùng Blazor Interactive Server để gọi trực tiếp Application services. Người dùng đã quyết định chuyển frontend sang React TypeScript và chọn Ant Design. Backend có use case, DTO, validation và các ranh giới nghiệp vụ có thể tái sử dụng; Partner API hiện tại không cung cấp đủ operation cho mọi actor nội bộ.

## Quyết định

1. Dùng React + TypeScript + Ant Design cho frontend thay thế; giữ thiết kế feature theo nghiệp vụ và các yêu cầu UX có ý nghĩa của UI hiện tại.
2. React gọi HTTP adapters trong Web; các adapter gọi Application use case. Authorization, Shop isolation và validation nghiệp vụ vẫn ở Application; invariant vẫn ở Domain; EF/Identity/I/O/worker vẫn ở Infrastructure.
3. Giữ Partner API, idempotency, outbox/webhook, audit, PII masking và dữ liệu hiện tại. Không đổi schema hoặc tách backend service chỉ vì đổi UI.
4. Chuyển theo task có parity checks; giữ UI cũ đến checkpoint cutover/rollback và chỉ bỏ Blazor sau khi assertions tương đương đã có.

## Quan hệ với quyết định trước

[ADR-002](../../Backend/decisions/adr-002-modular-monolith.md) tiếp tục quy định modular monolith và dependency direction backend. ADR này ghi hướng thay frontend/transport UI trong tương lai; không supersede kiến trúc backend hoặc viết lại lịch sử quyết định Blazor hiện hành. [ADR-001](../../Service/decisions/adr-001-partner-api-production-contract.md) và [ADR-003](../../Service/decisions/adr-003-transactional-outbox-and-webhooks.md) tiếp tục áp dụng cho tích hợp.

## Hệ quả

- Có thêm frontend build/test toolchain và browser HTTP contracts; UI không còn gọi C# service trực tiếp sau migration.
- Cần kiểm chứng session/CSRF, permission/context cache, serialization và lỗi mạng tại ranh giới HTTP mới.
- Ant Design thay primitive UI; theme, dữ liệu thật, error/empty, quyền và keyboard/mobile behavior cần được chuyển có chủ đích.
- Trong coexist cần hai frontend runtime; route ownership, assets, auth cũ và rollback phải được kiểm chứng trước khi xóa Blazor.

## Thiết kế và tiến triển triển khai

[Kế hoạch](../../../refactorfe.md) dùng Vite, React Router, TanStack Query, cùng origin, Identity cookie và coexist dưới `/app`; T00.02 chốt browser DecimalString/error/actor/route contracts và [exact pins](../react-migration-toolchain.md). Phase 1–2 đã triển khai hosting/shell, JSON Identity/CSRF, auth/reference/Shop context và generated contracts. API nghiệp vụ Phase 3+ chưa phát hành. Readiness, Redis skip và scope corrections theo source ở [migration contract/status](../react-migration-contracts.md) và [validation log](../react-migration-validation.md). Đây là ghi chú tiến triển của ADR-004, không thay đổi lịch sử ADR-002.
