# ADR-004: React + TypeScript + Ant Design cho frontend

- Ngày: 25/10/2026.
- Trạng thái quyết định: **Accepted** cho React + TypeScript + Ant Design theo lựa chọn của người dùng.
- Trạng thái triển khai: Phase0–10 đã hoàn tất; Phase11 có public pages/tracking summary, lazy routes, root hosting và cleanup. Kiểm chứng/checkpoint cuối theo [validation log](../react-migration-validation.md), không đồng nghĩa đã deploy môi trường ngoài.
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

[Kế hoạch](../../../refactorfe.md) dùng Vite, React Router, TanStack Query, cùng origin, Identity cookie và coexist dưới `/app`; T00.02 chốt browser DecimalString/error/actor/route contracts và [exact pins](../react-migration-toolchain.md). Phase 1–6 đã triển khai hosting/shell, JSON Identity/CSRF, auth/reference/Shop context, Shop dashboard/read/quote/commands/import/download và generated contracts. Các API nghiệp vụ còn lại theo Phase 7+. Readiness, Redis skip và scope corrections theo source ở [migration contract/status](../react-migration-contracts.md) và [validation log](../react-migration-validation.md). Đây là ghi chú tiến triển của ADR-004, không thay đổi lịch sử ADR-002.

## Quyết định cutover bổ sung — 26/10/2026

Quyết định này supersede **cơ chế frontend Blazor Interactive Server** ghi trong bối cảnh ADR-002, không supersede modular monolith hay dependency direction backend. React là UI duy nhất sau checkpoint Phase11; Web host HTTP/static/health/OpenAPI/workers, không circuit. appsettings bật root SPA; runtime config đổi basename từ `/app` sang rỗng và redirect deep links `/app/...` sang root giữ query. Các HTTP/system/file paths giữ route ownership riêng.

Chỉ gỡ Blazor sau khi parity, published artifact và rollback checkpoint đạt; kết quả thực tế theo validation log. Chín Operations source files đang có thay đổi người dùng được giữ nguyên và excluded khỏi compile/publish, không giữ runtime. Application operation-scoped proxy, Identity/CSRF, SQL schema, Partner contracts, workers và Data Protection giữ nguyên.

Sau cleanup, rollback về Blazor cần artifact trước cleanup với root flag false; không thể chỉ đổi cấu hình của artifact cuối. Giữ banner storage, key ring/certificate/ApplicationName và SQL/Redis config ổn định. Không có DB downgrade cho việc đổi renderer. Scripts publish/smoke chỉ tạo/kiểm artifact trong môi trường local được chỉ định; production rollout và readiness vẫn theo operations runbook. Đây là SPA không SSR; verified tracking UX và multi-node public limiter chưa thuộc phạm vi.
