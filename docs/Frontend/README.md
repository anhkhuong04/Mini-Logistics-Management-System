# Frontend

Frontend hiện tại là React + TypeScript strict + Ant Design, build bằng Vite tại `src/MiniLogistics.Frontend`. ASP.NET Core phục vụ bundle/static assets và browser HTTP adapters; Application tiếp tục sở hữu use case/quyền, Domain sở hữu invariants. Đọc [migration contracts/status](react-migration-contracts.md), [inventory A001–A093](react-migration-inventory.md), [toolchain pins](react-migration-toolchain.md) và [validation log](react-migration-validation.md) để truy vết từng ranh giới.

## Cấu trúc và render

| Vị trí | Vai trò |
| --- | --- |
| `src/app/App.tsx` | Router, route lazy loading, PublicLayout/AppLayout và session tools |
| `src/app/providers`, `src/app/layouts`, `src/app/styles` | Query/session/AntD/locale providers, shell và theme |
| `src/features/` | public, auth, shop-context, dashboard, shipments, shipment-import, operations, shipper, shop-settings, administration, integrations |
| `src/shared/` | HTTP/generated contracts, i18n, formatting và UI primitives |
| `e2e/` | Shell/public, real-cookie role workflows và published root smoke |
| `Web/Endpoints/Browser`, `Web/Services/BrowserSpa*` | HTTP adapters và static/root host |
| `Web/wwwroot/images`, `Web/wwwroot/data` | Ảnh hiện có và administrative JSON còn dùng |

Không có Blazor runtime trong UI sau cleanup. Chín file Operations đã có thay đổi người dùng được giữ nguyên tại `archive/legacy-blazor/Operations`, ngoài source runtime. Không dùng các file này làm hướng dẫn triển khai mới. Các path Razor trong inventory là nguồn baseline lịch sử, không phải link runtime.

`PublicLayout` dành cho landing/FAQ/contact/tracking/login/register; `AppLayout` dành cho workspace. PageHeader quản lý title/focus; AsyncContent phân biệt loading/error/empty. Route lazy loading tách code feature; public navigation không cần tải Admin/Integration feature chunks. AntD vendor vẫn có cảnh báo bundle lớn; xem validation thay vì suy ra mọi dependency đã nhỏ.

## Dependency và formatting

`app` ghép các feature; `features` gọi public API `index.ts` của feature khác; `shared` không phụ thuộc `app` hoặc `features`. ESLint kiểm các ranh giới này, gồm dynamic imports. Routing helpers ở `shared/routing`, shipment filter contracts ở `shared/model`, ngày UTC/server-time-zone ở `shared/lib`, download transport ở `shared/api`. Shop-scoped command/download controls ở `features/shop-context`.

Prettier được pin ở devDependencies; dùng `npm run format` và `npm run format:check`. Generated browser types không qua formatter. `npm run test:architecture` kiểm rule bằng cả ca được phép và ca vi phạm. Xem [source organization](../Backend/source-organization.md) và [kế hoạch lịch sử](history/refactorfe.md).

## Màn hình theo actor

| Khu vực | Route chính | Chức năng |
| --- | --- | --- |
| Public | `/`, `/faq`, `/contact`, `/tracking?code=...`, `/login`, `/register-shop` | Banner active/fallback, FAQ, contact links, tracking summary, tài khoản Shop |
| Shop | `/dashboard`, `/shipments`, `/shipments/create`, `/shipments/:id` | KPI/drill-down, tạo/draft/sửa/gửi/hủy, CSV import/export, label PDF |
| Shop settings | `/profile`, `/shop/staff`, `/shop/cod-report`, `/shop/notifications`, `/shop/audit` | Hồ sơ/thêm Shop, staff, COD, thông báo, audit |
| Operations | `/operations/assignments`, alias `/operations/assignment` | Assign/reassign/cancel, bulk retry, trạng thái và COD |
| Shipper | `/shipper/shipments` | Mobile assignment/pickup/delivery, COD, availability |
| Admin | `/admin`, alias `/admin/dashboard`, `/admin/users`, `/admin/shops`, `/admin/hubs`, `/admin/system-config`, `/admin/cod`, `/admin/audit-logs`, `/admin/banners` | Dashboard, user/config, đối soát, audit, banner |
| Integration | `/partner/integrations` | Client/key/scope, webhook/test/retry, lịch sử giới hạn |
| System UI | `/Error`, `/not-found`, unknown navigation | Safe error/NotFound và focus recovery |

Root config bật `BrowserSpa:Enabled` và `ServeAtRoot`; `/app/...` redirect local sang root, giữ query. Cùng dist có thể chạy dưới `/app` khi `ServeAtRoot=false`, nhưng bản cleanup không khôi phục Blazor. API/file/health/asset paths không được nhận SPA HTML. Auth cookie và query/deep links giữ nguyên; không reset tài khoản hay dữ liệu.

## Quy ước interaction và bảo mật

- Dùng named imports từ `@ant-design/icons`, ẩn icon trang trí và đặt accessible name cho nút chỉ có icon. Không hardcode glyph/SVG.
- Dùng AntD Form/Table/Modal và theme/locale provider; status có text, form có labels, loading/disabled ngăn submit lặp. Table scroll trong region, layout mobile, keyboard/focus/portal phải dùng được.
- Session đọc cookie thật; mutation dùng `X-CSRF-TOKEN`, refresh token khi đổi identity. Không lưu session/key/password/PII vào persistent storage. Frontend guard phục vụ UX; Application kiểm quyền và Shop/assignment/scope độc lập.
- Query key theo user/Shop, abort và bỏ response cũ khi đổi context; filter/page trong URL và server paging. Không hiển thị dữ liệu tenant trước trong lúc refetch.
- Mutation không tự retry khi timeout. Unknown outcome cần kiểm tra authoritative state; 409 cần refresh thành công trước khi thử lại. Quote chỉ là preview, server tính phí lại. Không prefill/sửa masked snapshots.
- Decimal/money qua HTTP là string; enum là tên xác định. UTC timestamp hiển thị theo IANA `serverTimeZoneId` và rule tại từng timestamp, không dùng timezone browser hoặc một offset hiện tại cho mọi ngày.
- Locale vi/en đi cùng culture cookie qua `/culture/set`; cookie reload/deep link giữ route/query. Theme sea-blue giữ design intent; font stack Be Vietnam Pro/Segoe UI/Arial, dùng fallback khi máy chưa có Be Vietnam Pro.

Public tracking chỉ gọi summary, tên mask và không có phone/address fields trong DTO. Limiter HTTP lấy remote IP sau trusted-forwarded processing; threshold/store hiện có vẫn process-local, chưa giải quyết scale nhiều node. Không có phone-last-four form. Landing dùng API active banners, chuyển 3 giây với pause/manual controls; không thêm contact submit. SPA chưa có SSR/SEO server-rendered.

Import giữ UTF-8/2.000.000 bytes/500 rows, preview/edit/revalidate và batch polling2s; cell numeric lỗi cần sửa rõ ràng. Download giữ cookie/quyền, MIME/filename và revoke object URL. ExportData độc lập với ViewCod/PII/audit. Banner upload giữ storage bền vững ngoài release. Integration keys chỉ trong modal transient, signing secret write-only; test/retry xếp hàng, không chứng minh delivery ngoài hệ thống.

## Phát triển và kiểm tra

Lệnh đầy đủ ở [Development](../Backend/development.md) và [.agents/commands](../../.agents/commands.md). Đúng pins Node24.22.1/npm11.19.1; `npm ci`, typecheck/lint/test/build, dev certificate/HTTPS proxy, Web Release build và Playwright Chromium. Vite root dùng React HMR; HTTP/static proxy vẫn kiểm TLS.

Real-cookie E2E fixture dùng Identity/Application và EF InMemory tách biệt, không thay bằng chứng transaction/concurrency LocalDB. Public/root smoke còn chạy trên published process. Chỉ artifact trước cleanup mới có thể rollback về Blazor; [operations](../Service/operations.md#react-release-và-rollback-phase11) mô tả storage/config/schema. Không có production deployment tự động trong task này.

[Dashboard UI review](dashboard-ui-review.md) giữ lịch sử thiết kế; implementation/evidence mới theo validation log. Manual reports bị người dùng xóa không được khôi phục và không dùng làm bằng chứng test. Các test renderer cũ chỉ được bỏ sau mapping assertion ở [inventory](react-migration-inventory.md#retirement-map-cho-test-renderer-blazor).
