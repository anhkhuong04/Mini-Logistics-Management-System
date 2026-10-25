# React + TypeScript + Ant Design Frontend Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Dùng `superpowers:executing-plans` để triển khai tuần tự theo task. Chỉ dùng `superpowers:subagent-driven-development` nếu người dùng chọn cách thực thi bằng subagent. Phase 0 và T01.01 đã hoàn tất; T01.02–03 đang triển khai. Xem validation log/ledger cho evidence; các trang nghiệp vụ và Phase 2 chưa thay đổi.

**Goal:** Thay toàn bộ frontend Blazor bằng React + TypeScript + Ant Design, giữ tương đương chức năng, quyền truy cập và các hợp đồng nghiệp vụ/tích hợp hiện tại.

**Architecture:** Thêm React SPA tại `src/MiniLogistics.Frontend`; giữ `MiniLogistics.Web` làm composition root và HTTP host .NET 10. React gọi API dành cho browser qua Identity cookie và antiforgery; API gọi Application use case. Chạy song song ở `/app` trong quá trình migration, sau đó chuyển React về các URL hiện tại và gỡ Blazor.

**Tech Stack:** React, TypeScript strict, Ant Design, Vite, React Router, TanStack Query, native `fetch`; Ant Design Form; Vitest + React Testing Library; Playwright; ASP.NET Core 10 Minimal API, Identity, FluentValidation, EF Core/SQL Server và xUnit hiện có.

**Spec:** Yêu cầu người dùng ngày 25/10/2026 và các quyết định/phạm vi trong mục 1–5 của tài liệu này; context nguồn là `AGENTS.md`, `.agents/`, `docs/Frontend/README.md`, `docs/Frontend/dashboard-ui-review.md` và source hiện tại. Tài liệu này là kế hoạch, không phải bằng chứng migration đã hoàn tất.

## Global Constraints

- Tuân thủ `AGENTS.md`: Domain không phụ thuộc project khác; Application → Domain; Infrastructure → Application/Domain; Web là composition root.
- Không đưa invariant, authorization hay Shop isolation sang React. Endpoint cũng không thay thế kiểm tra quyền trong Application.
- Không thay schema chỉ để đổi frontend; không sửa migration history/model snapshot bằng tay.
- Giữ Partner API `/api/v1/partner`, API key/scope, idempotency `201/200`, outbox atomicity, audit, webhook signing/SSRF và PII masking.
- Chỉ bổ sung package có vai trò nêu trong kế hoạch; không thêm mediator, generic repository, backend service boundary hoặc frontend state store trùng trách nhiệm.
- Giữ design intent sea-blue, Be Vietnam Pro, KPI thật, error/empty riêng, drill-down và responsive/accessibility từ dashboard review. Ant Design thay primitive/form/table, không mặc định kéo theo thiết kế lại nghiệp vụ.
- Production dùng chung origin. Development dùng Vite HTTPS proxy đến .NET; browser không cần biết backend URL qua biến chứa secret.
- Không commit secret, password, API key, storage state Playwright, `node_modules`, `dist`, browser download, log hoặc build output. OpenAPI/types là contract artifact có chủ đích, sinh bằng tooling và review.
- Chỉ build/test solution `Mini-logistics-manegemant-system.slnx` hoặc project cụ thể; không dùng `.csproj` lạc ở root.
- Infrastructure tests cần Windows SQL Server LocalDB; không dùng EF InMemory để kết luận transaction/concurrency đúng.
- Giữ nguyên thay đổi Operations, `.codex-operations-ui.py` và các deletion `test/manual/*` có trong working tree; không reset, khôi phục, ghi đè hoặc gộp chúng vào commit migration nếu chưa được đặt trong phạm vi. Kiểm tra lại trạng thái này khi bắt đầu thực thi.

## Review Focus

1. Đổi Shop khi request cũ còn chạy: dữ liệu Shop cũ không xuất hiện trong Shop mới; T02.04 kiểm tra hủy request và query key theo user/Shop.
2. Session hết hạn, đổi tài khoản hoặc quyền bị thu hồi: không hiển thị cache/secret cũ; T02.02–T02.04 kiểm tra `401/403`, refetch quyền và xóa cache.
3. Timeout sau mutation có thể đã commit: không tự retry POST tạo đơn, thu/đối soát COD, rotate key hoặc retry webhook; T02.01 và task nghiệp vụ kiểm tra việc reload trạng thái.
4. Date filter/tiền/enum đi qua C#–JSON–TypeScript: không đổi ngày UTC, mất số lẻ hoặc nhận trạng thái ngoài enum; T02.01 và T03.01 kiểm tra contract biên.
5. Direct URL/reload và deploy thiếu asset: API/file/health không trả SPA HTML, bundle khớp HTML, rollback giữ được data/upload; T01.02 và T11.04–T11.05 kiểm tra published artifact.

---

## 1. Phạm vi và các quyết định của kế hoạch

### 1.1 Đã được người dùng xác nhận

- Đổi frontend từ Blazor sang React TypeScript.
- Dùng Ant Design.
- Chia nhiều phase/task theo thứ tự và lưu kế hoạch ở root `refactorfe.md`.
- Lượt hiện tại chỉ lập kế hoạch; chưa scaffold, cài dependency hoặc thay sản phẩm.

### 1.2 Lựa chọn kỹ thuật đề xuất để thực thi

| Hạng mục | Quyết định | Lý do cụ thể |
| --- | --- | --- |
| Frontend build | Vite SPA, không thêm Node server production | Backend và worker đã được host bằng .NET |
| Triển khai | Cùng origin, cùng release artifact với .NET | Tận dụng Identity cookie, giảm cấu hình CORS |
| Chuyển đổi | Từng feature ở `/app`, giữ Blazor ở URL cũ đến cutover | Có thể đối chiếu và rollback trước khi gỡ UI cũ |
| UI | `antd`, `@ant-design/icons`, CSS Modules cho layout riêng | Có Form/Table/Modal/Drawer/Upload; không thêm Bootstrap/Tailwind vào React |
| Server state | TanStack Query | Cache, invalidation, polling import; không lưu bản sao toàn bộ dữ liệu trong context |
| Client state | React state/context; URL cho filter/page/Shop | Không có nhu cầu thêm Redux/Zustand ở giai đoạn này |
| Form | Ant Design Form; server validation là authoritative | Không thêm một form library/schema library thứ hai |
| HTTP | `fetch` wrapper mỏng, types sinh từ OpenAPI browser | Giữ một nơi xử lý cookie/CSRF/error/cancellation |
| Routing | React Router declarative, route lazy theo feature | SPA không cần React server framework |
| Localization | Dictionary TS có type cho copy app; Ant Design locale + Day.js locale | Giữ `vi-VN` mặc định và `en-US`, không mở thêm nền tảng dịch |
| Test | Vitest/RTL cho behavior; Playwright cho browser; xUnit cho API/Application | Bù khoảng trống giữa Blazor server invocation và HTTP thật |

Các lựa chọn này là đề xuất của kế hoạch. T00.02 kiểm tra compatibility và khóa phiên bản trước khi cài; không dùng `latest` trong CI hoặc hướng dẫn tái lập.

### 1.3 Không nằm trong đợt migration

- Viết lại Domain/state machine, đổi ORM/database, chia microservice, tách worker thành deployment khác.
- Đổi Partner API, cơ chế webhook, outbox hoặc tự bổ sung idempotency cho UI giống Partner API.
- Thiết kế lại toàn bộ brand/marketing, thêm chart số liệu backend chưa cung cấp, offline/PWA hoặc realtime hub mới.
- Sửa mọi known issue. Tracking verified chưa được nối từ UI, limiter nhiều node và timezone được ghi rõ ở T00.01; không âm thầm thay hành vi trong migration.
- Xóa `.csproj` lạc ở root, lịch sử tài liệu hoặc file người dùng ngoài phạm vi.

### 1.4 Checkpoint Phase 0 — 25/10/2026

Phần hướng dẫn agent/developer đã được chuẩn bị trước khi viết code: [ADR-004](docs/Frontend/decisions/adr-004-react-typescript-ant-design.md), [migration contract/status](docs/Frontend/react-migration-contracts.md) và [validation log](docs/Frontend/react-migration-validation.md). Entry points, quy tắc Blazor/React, browser security, documentation workflow và các liên kết manual report đã được đồng bộ.

T00.01–T00.02 đã có [inventory từng action và C# field binding](docs/Frontend/react-migration-inventory.md), [exact toolchain pins](docs/Frontend/react-migration-toolchain.md), baseline320 pass/0 fail/1 Redis skip và contract/route/fixture decisions. Node local22.18.0 cần nâng lên target24.22.1/npm11.19.1 trước Phase 1; install/compile/browser/publish của frontend chưa chạy. Không chờ T11.06 mới cập nhật tài liệu: task thay boundary phải cập nhật tài liệu sở hữu trong cùng checkpoint.

Các mô tả proof UI/assignment trong detail và reveal/rotate secret webhook đã được thu hẹp theo source: chúng không phải action UI hiện có. Giữ backend proof capability/tests; thêm chức năng ngoài parity cần task riêng, không đưa vào migration tự động.

## 2. Baseline đã khảo sát

- `src` có bốn project .NET; Application tổ chức theo feature/use case, không theo mediator command/query.
- Web dùng Interactive Server; component gọi trực tiếp Application service. Có 53 file `.razor`, 28 page có route, 30 khai báo route và 38 file CSS scoped tại thời điểm khảo sát.
- API partner hiện có quote/create/track/cancel; browser có auth form, culture endpoint và bốn file download endpoint. Chưa có API nội bộ đầy đủ.
- `CreateShipment.razor`, `Shipments.razor`, `OperationsAssignments.razor`, `ShipperShipments.razor` là các luồng nhiều trạng thái cần phân rã theo use case.
- `BannerService` chưa có actor authorization; upload banner đang ghi file trong Razor. Phải sửa ranh giới này trước khi mở endpoint quản trị tương ứng.
- `VietnamAdministrativeDivisionService` đọc JSON trong `wwwroot/data` và còn phục vụ validation server; không xóa nguồn dữ liệu này khi dọn assets Blazor.
- Application có `OperationScopedServiceProxy` để scope từng operation. Giữ cơ chế này trong migration; không refactor DI/persistence lifecycle kèm theo.
- Web tests đã có `HtmlRenderer` dashboard, drill-down, authentication antiforgery và Partner API contract; không phải toàn bộ đều là helper test.
- CI hiện restore/build/test .NET và kiểm tra Partner OpenAPI drift/compatibility, chưa build frontend.

## 3. Cấu trúc file đích và trách nhiệm

```text
src/MiniLogistics.Frontend/
  package.json, package-lock.json, .node-version, .npmrc
  index.html, vite.config.ts, tsconfig*.json, eslint.config.js
  vitest.config.ts, playwright.config.ts
  public/images/                         # Ảnh tĩnh đã chuyển, không phải upload runtime
  src/
    main.tsx
    app/
      App.tsx, router.tsx, routePaths.ts
      providers/AppProviders.tsx, queryClient.ts
      layouts/AppLayout.tsx, PublicLayout.tsx
      theme/antdTheme.ts, styles/global.css
    shared/
      api/httpClient.ts, ApiError.ts, csrf.ts
      api/generated/browser-api.d.ts      # Sinh bằng tooling, không sửa tay
      auth/SessionProvider.tsx, RequireSession.tsx, permissions.ts
      i18n/I18nProvider.tsx, vi-VN.ts, en-US.ts
      lib/dateTime.ts, money.ts
      ui/PageHeader.tsx, StatusTag.tsx, AsyncContent.tsx
    features/
      auth/                               # Login/register/session UI
      shop-context/                       # Selected Shop và permission theo Shop
      dashboard/                          # Shop dashboard
      shipments/                          # List/detail/create/edit/draft/quote
      shipment-import/                    # Preview/confirm/polling
      operations/                         # Assignment/status/COD workspace
      shipper/                            # Workspace chỉ của shipper đăng nhập
      shop-settings/                      # Profile/staff/report/notifications/audit
      administration/                     # Dashboard/users/shops/hubs/config/COD/banner/audit
      integrations/                       # API client/key/webhook portal
      public/                             # Home/FAQ/contact/tracking
    test/setup.tsx, renderWithProviders.tsx
  e2e/                                    # Test theo actor và luồng

src/MiniLogistics.Web/
  Endpoints/Browser/                      # Minimal endpoints và request/response browser
  Services/BrowserSpaOptions.cs           # Enabled + root cutover option
  Services/BrowserSpaHostingExtensions.cs # SPA assets/fallback giới hạn route
  Services/BrowserSessionReader.cs        # Principal -> safe session DTO
  Services/BrowserUserClaims.cs           # Principal -> actor GUID
  Endpoints/Browser/BrowserApiResultMapper.cs
  Endpoints/Browser/BrowserApiExceptionHandler.cs
  Endpoints/Browser/BrowserApiOpenApiTransformer.cs
  Endpoints/Browser/BrowserApiJsonOptions.cs
  Services/BrowserValidationProblem.cs    # Field errors ở biên HTTP
  wwwroot/                               # Data/ảnh/upload backend vẫn cần

docs/Frontend/react-migration-contracts.md # Route/action/permission/API/compatibility matrix
docs/Frontend/react-migration-validation.md# Bằng chứng chạy test/QA/release thực tế
docs/Service/browser-api.openapi.json     # OpenAPI browser riêng, sinh bằng tooling
test/MiniLogistics.Web.Tests/Browser/     # Browser API, route, session, contracts
```

Mỗi feature React dùng `api.ts`, `queries.ts`, page và component có tên theo nghiệp vụ. Không tạo generic CRUD engine hay bọc mọi component Ant Design. Tên file/task mới trong tài liệu là đích dự kiến; file hiện có phải đọc lại trước khi sửa.

## 4. Hợp đồng nền tảng dùng chung giữa các task

### 4.1 Nhóm HTTP

- Browser authenticated: `/api/v1/app`; `RequireAuthorization` + kiểm tra role/action thích hợp; Application kiểm tra quyền/Shop lần nữa.
- Public: `/api/v1/public`; anonymous DTO có giới hạn PII, limiter riêng.
- Partner: `/api/v1/partner` giữ nguyên. Không gắn filter/serializer/error mapper của browser vào partner.
- Auth React: `GET /api/v1/app/auth/csrf`, `POST /api/v1/app/auth/login`, `POST /api/v1/app/auth/register-shop`, `POST /api/v1/app/auth/logout`, `GET /api/v1/app/auth/me`.
- CSRF/login/register được anonymous; `me` và logout phải authenticated. Tất cả POST/PUT/PATCH/DELETE dùng cookie, kể cả login/register và multipart upload, đều kiểm tra antiforgery ở server.
- Các auth form `/auth/*` cũ giữ đến khi migration hoàn tất để Blazor vẫn chạy. Không đổi redirect của UI cũ trong task thêm JSON endpoint.
- Thành công trả DTO trực tiếp; paged read dùng `{ items, pageNumber, pageSize, totalCount, totalPages }`, page one-based theo contract hiện có.
- Browser error dùng ProblemDetails có `status`, `title`, `code`, `traceId`, `errors?: Record<string, string[]>`; không chứa exception/secret/raw PII. Map validation `400`, unauthenticated `401`, forbidden `403`, not found `404`, conflict/concurrency/capacity `409`, account lockout `423`, rate limit `429` + `Retry-After`, unavailable `503`.
- Error/status map browser là contract mới; không đổi error envelope partner hoặc status file download cũ trong lúc coexist.
- JSON camelCase; GUID string, DateTimeOffset ISO 8601 UTC; enum string có danh sách rõ, reject giá trị ngoài enum. ShopPermission serialize thành mảng tên permission trong browser context để tránh flags string khó dùng.
- Không nhận actor ID/role/permission từ body để làm nguồn tin cậy. Selected `shopId` là input không được tin cậy; use case xác minh membership.
- Theo T00.02, mọi C# decimal trong browser request/response dùng DecimalString/null vì money decimal(18,2) vượt JS safe range; giữ đơn vị và validator hiện tại, InputNumber stringMode, không tính tiền nghiệp vụ ở JS. T02.01 kiểm tra round-trip/malformed/overflow; xem contract/status cho chi tiết. Partner/file contracts không đổi.
- Không thêm ETag/rowversion contract mới nếu use case hiện tại không có; map concurrency result hiện có và yêu cầu reload.
- Dynamic session/PII/secret response dùng `Cache-Control: no-store`; không dùng browser/CDN cache chung cho tenant data.

### 4.2 Interfaces frontend dùng xuyên phase

```ts
type Locale = 'vi-VN' | 'en-US';
type Session = {
  userId: string;
  displayName: string;
  roles: string[];
  operationPermissions: string[];
  locale: Locale;
  // IANA timezone tương ứng TimeZoneInfo.Local của server hiện tại.
  serverTimeZoneId: string;
};
type ShopContext = {
  selectedShopId: string;
  shops: Array<{
    shopId: string;
    name: string;
    isActive: boolean;
    isOwner: boolean;
    permissions: string[];
    // Các field profile đã masking còn lại theo GetShopContextResponse.
  }>;
};
type ApiProblem = {
  status: number;
  title: string;
  code: string;
  traceId: string;
  errors?: Record<string, string[]>;
};
type RequestOptions = {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?: unknown;
  signal?: AbortSignal;
};
// Định nghĩa/export trong shared/api/httpClient.ts:
// requestJson<T>(path: string, options?: RequestOptions): Promise<T>
// requestForm<T>(path: string, form: FormData, options?: Omit<RequestOptions, 'body'>): Promise<T>
// requestDownload(path: string, signal?: AbortSignal): Promise<{ blob: Blob; fileName: string }>
// Không set Content-Type JSON cho multipart, không parse 204 thành JSON.
```

Session fields chỉ phục vụ hiển thị/UX, không dùng như quyền authoritative trên server. `formatDateTime(utcIso, timeZoneId, locale)` dùng `Intl.DateTimeFormat` với timezone IANA do server trả; backend chuyển Windows timezone ID sang IANA bằng TimeZoneInfo khi cần và kiểm chứng timezone đó được browser hỗ trợ. Public response cũng cung cấp `serverTimeZoneId`. Phải dùng timezone rule theo từng timestamp, không lấy offset hiện tại áp cho mọi ngày vì sẽ sai DST. T11.03 kiểm tra DST/date boundaries. Không đổi sang timezone browser hoặc cố định UTC+7 trong refactor này.

`queryKey` luôn có user ID; dữ liệu Shop có thêm Shop ID và filter/page/sort. Khi đổi Shop, hủy request cũ và không dùng previous-data của Shop khác. Khi logout/401, hủy request, clear cache và secret đang hiển thị; không ghi token/key vào localStorage. Locale copy và format có thể dùng preference không nhạy cảm.

### 4.3 Quy tắc áp dụng Ant Design

- Root `ConfigProvider` áp theme/locale và `App` cung cấp message/notification/modal context; tránh static API ngoài context làm mất theme/locale.
- Ánh xạ token từ `app.css` sang `colorPrimary`, semantic colors, `fontFamily`, `fontSize`, border/radius/spacing. Không import nguyên Bootstrap reset sang React.
- Dùng `Layout/Menu/Drawer` cho shell; `Table` server paging/filter/sort; `Form` + `Input/Select/DatePicker/InputNumber`; `Modal` cho confirm; `Alert/Result/Empty/Skeleton` cho state.
- Control/action chính đạt 44px theo dashboard guideline; không mặc định mọi row control quá lớn. Bảng cuộn trong vùng có accessible name; shipper dùng card/action stack khi màn hình hẹp.
- `Form.Item` map field errors bằng mapping request field -> form name; error cấp use case hiển thị Alert có trace ID khi cần hỗ trợ. Form client không thay server validation.
- Không thêm Ant Design Pro, chart library hoặc rich text editor. Phân bố shipment dùng số liệu thật, CSS/Ant Design Progress đủ cho yêu cầu hiện tại.

## 5. Thứ tự phase và điều kiện chuyển bước

| Phase | Tasks | Kết quả có thể nghiệm thu | Phụ thuộc |
| --- | --- | --- | --- |
| 0 | T00.01–02 | Baseline và route/API/permission contract | Không |
| 1 | T01.01–03 | React/Ant Design chạy ở `/app`, shell và build tối thiểu | Phase 0 |
| 2 | T02.01–04 | HTTP/session/CSRF/login/register/Shop context | Phase 1 |
| 3 | T03.01–03 | Dashboard/list/detail Shop đọc dữ liệu thật | Phase 2 |
| 4 | T04.01–03 | Quote/create/draft/edit/submit/cancel | Phase 3 |
| 5 | T05.01–03 | Import/export/label hoạt động | Phase 4 |
| 6 | T06.01–04 | Operations assignment/status/COD | Phase 5 |
| 7 | T07.01–03 | Shipper workspace và lifecycle | Phase 6 |
| 8 | T08.01–03 | Shop profile/staff/report/notification/audit | Phase 7 |
| 9 | T09.01–04 | Admin đầy đủ, banner upload đúng layer | Phase 8 |
| 10 | T10.01–02 | Integration portal tương đương | Phase 9 |
| 11 | T11.01–06 | Public/QA/publish/cutover/gỡ Blazor | Phase 10 |

Đây là thứ tự thực thi mặc định, không phải yêu cầu chạy nhiều agent. Không xây toàn bộ API trước rồi mới xây UI: mỗi task/phase nối HTTP → Application → React → test, để phát hiện lệch contract sớm. Chỉ đánh dấu task xong khi các check của task đã có kết quả thực tế.

## 6. Quy trình kiểm chứng cho từng task

- Task thay behavior: thêm test theo tình huống nêu trong task, chạy để xác nhận thất bại vì behavior chưa có, thực hiện thay đổi nhỏ nhất, chạy lại test và các suite liên quan. Không viết test chỉ kiểm tra config/getter hoặc lặp lại implementation.
- Task scaffold/docs/build: kiểm tra trực tiếp command/artifact tương ứng, không ép viết unit test không có giá trị.
- Backend feature: chạy test Web Browser tương ứng và Application suite của use case; thay Infrastructure/persistence thì thêm LocalDB check liên quan.
- Frontend feature: chạy Vitest file feature, typecheck/lint/build và Playwright flow được ghi trong task.
- Khi endpoint/DTO đổi: sinh lại browser OpenAPI/types, kiểm tra diff; Partner OpenAPI phải giữ nguyên nếu contract partner không đổi.
- Trước mỗi task: `git status`, đọc lại caller/use case/test; sau task: `git diff --check`, inspect diff. Mỗi task là một commit nhỏ khi bắt đầu thực thi; chỉ stage file thuộc task. Không commit trong lượt lập kế hoạch này.
- Mỗi phase có checkpoint có thể chạy/review; không xóa Blazor chỉ vì phiên bản React đã render được.

## Phase 0 — Baseline, scope và contract

### T00.01 — Chốt inventory và bằng chứng baseline

**Files:** đọc/cập nhật `AGENTS.md`, `.agents/*`, README, docs index/Frontend guide/dashboard review/development guide; đọc toàn bộ route/inject trong `Components/Pages`, endpoint/use case/repository/mapping/tests liên quan. `docs/Frontend/react-migration-contracts.md` và `react-migration-validation.md` đã được tạo cho preparation; tiếp tục hoàn thiện inventory/evidence ở đó, không tạo bản trùng.

**Interfaces:** tạo bảng `old route -> action -> Application service -> actor/Shop permission -> new endpoint -> React page -> regression test -> status` làm nguồn kiểm tra parity xuyên phase.

- [x] Đồng bộ entry points và agent/developer guidance cho React/TypeScript/Ant Design; giữ current Blazor và proposed target tách biệt, sửa giới hạn quy tắc legacy cùng liên kết manual report đã lệch.
- [x] Tạo migration contract/status và validation log; ghi rõ các phần chưa chạy, không đánh dấu baseline/inventory hoàn tất bằng việc chỉ có tài liệu.
- [x] Ghi đúng trạng thái working tree và baseline commit; đánh dấu Operations đang sửa để không lấy HEAD cũ làm behavior chuẩn.
- [x] Liệt kê 26 page nghiệp vụ + Error/NotFound, hai alias `/admin/dashboard`, `/operations/assignment`, query parameter và file download;93 page/service bindings cùng contract/regression mapping trong inventory.
- [x] Chạy restore/build/test chuẩn ở mục 7; build0 warning/error,320 pass/0 fail/1 Redis skip; đã ghi command/môi trường và giới hạn trong validation log.
- [x] Ghi quyết định scope: giữ UI behavior/theme; public tracking chỉ summary như hiện tại, không hiển thị verification form chưa hoạt động; giữ timezone server; chuyển vị trí limiter nhưng chưa thay store nhiều node.
- [x] Ghi format upload banner hiện tại và giới hạn5,242,880 byte, import CSV `2_000_000` bytes/500 row, polling2 giây; các giới hạn trong contract đã đối chiếu source.

**Acceptance:** inventory không thiếu role/action; baseline failures tách khỏi regression; cả hai tài liệu có dữ liệu kiểm chứng, không chép test count lịch sử.

### T00.02 — Khóa contract, dependency và đường chuyển route

**Files:** mở rộng `docs/Frontend/react-migration-contracts.md`; quyết định stack được ghi tại `docs/Frontend/decisions/adr-004-react-typescript-ant-design.md`; đọc `Program.cs`, Web csproj, CI và `DashboardPresentation.cs`.

**Interfaces:** chốt mục 4 thành contract dùng cho mọi task; version matrix cho React/ReactDOM/AntD/Vite/TypeScript/Node và các package hỗ trợ.

- [x] Ghi React/TypeScript/Ant Design là quyết định đã chọn trong ADR mới; các chi tiết hỗ trợ của plan là đề xuất chưa triển khai. Giữ nguyên lịch sử ADR-002 và ADR tích hợp.
- [x] Khóa exact27 package pins và Node24.22.1/npm11.19.1 từ nguồn chính thức;51 constraint checks và strict-peer dry-run không conflict. TypeScript5.9.3 phù hợp generator/linter; React/ReactDOM19.3.1. Chưa install; Node local phải nâng ở T01.01.
- [x] Ghi danh sách API tại từng task và C# input/response field bindings; actor lấy claims, DecimalString, schemas/error/status/enum/permission/UTC/validation đã chốt trong contract/inventory. Generated schema/tests là nhiệm vụ implementation.
- [x] Chốt `BrowserSpa:Enabled=false`, `BrowserSpa:ServeAtRoot=false` làm default coexist; `/app` là basename tạm, `/frontend-assets` là asset prefix; root owner/fallback/alias policy đã ghi.
- [x] Chốt thiết kế mapping cho một `routePaths.ts`, giữ query cũ/aliases và chỉ thêm `/app` trong coexist. File code tạo tại T01.03, không scaffold trong Phase 0.
- [x] Chốt fixture matrix user/Shop/roles và policy credential ephemeral/environment trong contract; không ghi secret thật hoặc auth storage vào Git.

**Acceptance:** người triển khai biết package/contract/path phải dùng; không còn lựa chọn kiến trúc chặn scaffold. Những thay đổi ngoài scope phải thành task riêng, không gộp vào migration.

## Phase 1 — React foundation, hosting và Ant Design

### T01.01 — Scaffold frontend và toolchain có thể tái lập

**Files:** tạo `src/MiniLogistics.Frontend/package.json`, lockfile, `.node-version`, `.npmrc`, Vite/TS/ESLint/Vitest/Playwright config, `index.html`, `src/main.tsx`, `src/app/App.tsx`, test helpers; sửa `.gitignore`, thêm `.github/workflows/frontend-ci.yml`.

**Interfaces:** npm scripts `dev`, `typecheck`, `lint`, `test`, `build`, `api:generate`, `test:e2e`; `test` dùng `vitest run`, `build` typecheck rồi Vite build.

- [x] Scaffold React TS theo version matrix; strict TS, npm lockfile và `save-exact=true`; Node/npm pin trong CI. Runtime portable đã kiểm chứng, không đổi Node global.
- [x] Cài đúng27 direct package pins của Phase 0; không thêm library ngoài scope. npm install/ci audit0 vulnerabilities tại lần chạy.
- [x] Test harness matchMedia/ResizeObserver/AntD providers; interaction mở/đóng hỗ trợ và link contact đã qua Vitest cùng Chromium.
- [x] Ignore node_modules/dist/test-results/playwright-report/.auth/env local/tsbuildinfo; không cần `.env.example` khi scaffold chưa có cấu hình môi trường riêng.
- [x] CI frontend npm ci/typecheck/lint/test/build đã khai báo; chạy các lệnh tương ứng local bằng runtime pins đạt. `api:generate` vẫn chặn có chủ đích cho đến T02.01.

**Verify:** các script trên exit 0 từ checkout sạch; artifact chỉ ở dist, không bị theo dõi trong Git. `api:generate` được hoàn thiện khi có schema ở T02.01, không dùng schema giả làm chuẩn.

### T01.02 — Host React ở `/app` mà không bắt nhầm route backend

**Files:** tạo `Services/BrowserSpaOptions.cs`, `BrowserSpaHostingExtensions.cs`; sửa `Program.cs`, Web csproj, Vite config; tạo `test/MiniLogistics.Web.Tests/Browser/BrowserSpaHostingTests.cs`, `e2e/coexist-routing.spec.ts`.

**Interfaces:** `AddBrowserSpa(...)`, `MapBrowserSpa(...)`; router basename lấy runtime config giới hạn `{ basePath, assetBasePath }`, không chứa secret.

- [ ] Viết test bật/tắt SPA: `/app/dashboard` refresh trả HTML React; `/dashboard` vẫn Blazor; `/api/...`, `/auth/...`, `/shop/files/...`, `/culture/set`, health, unknown `.js` không nhận index HTML.
- [ ] Configure Vite dev HTTPS và proxy backend cho API/auth/culture/file; dùng trusted dev certificate, không tắt kiểm tra certificate cho production.
- [ ] Build assets vào dist với prefix `/frontend-assets`; copy vào publish artifact thông qua target có điều kiện, không commit/copy đè lên `wwwroot` source. Test bằng published process, không chỉ factory từ source tree.
- [ ] SPA fallback chỉ dưới `/app` khi root mode tắt; missing asset trả 404. Phân nhánh status-code re-execute hiện tại để browser API không bị `/not-found` Razor biến thành HTML.
- [ ] Khi SPA disabled hoặc chưa có dist, backend build/test/OpenAPI vẫn chạy; publish có bật SPA mà thiếu dist phải fail rõ ràng.

**Verify:** Web hosting test + Playwright coexist pass; .NET build vẫn sinh Partner OpenAPI bằng design-time host không có worker.

### T01.03 — Ant Design theme, layouts và accessibility shell

**Files:** tạo `app/providers/AppProviders.tsx`, `app/theme/antdTheme.ts`, layouts/router/routePaths, `shared/ui/*`, `shared/i18n/*`, `app/styles/global.css`; tham chiếu `wwwroot/app.css`, `MainLayout.razor`, `NavMenu.razor`; test `src/app/layouts/AppLayout.test.tsx`, `e2e/shell.spec.ts`.

**Interfaces:** root AppProviders bọc ConfigProvider/AntD App/QueryClientProvider/I18nProvider; AppLayout nhận session/navigation sau Phase 2, không hard-code demo role.

- [ ] Map theme token/font/spacing hiện có; dựng PublicLayout/AppLayout bằng Layout/Menu/Drawer; icon dùng `@ant-design/icons`.
- [ ] Thêm PageHeader, StatusTag và AsyncContent cho loading/error/empty; không bọc lại mọi AntD component hoặc đưa logic nghiệp vụ vào shared UI.
- [ ] Locale vi/en gồm copy app, component AntD và date control; giữ locale server qua culture cookie khi session tích hợp ở Phase 2.
- [ ] Kiểm tra skip link, title/focus khi navigate, Escape/close/focus return của mobile drawer, keyboard navigation, viewport 390/768/1024/1440 và zoom 200%.

**Acceptance:** theme nhất quán, shell chạy mobile, không phụ thuộc Bootstrap/Blazor CSS; navigation theo quyền được nối thật ở T02.03–04.

## Phase 2 — Browser HTTP, Identity, CSRF và Shop context

### T02.01 — HTTP contract, error boundary, OpenAPI và typed client

**Files:** tạo `Endpoints/Browser/BrowserApiEndpoints.cs`, `BrowserApiResultMapper.cs`, `BrowserApiExceptionHandler.cs`, `BrowserApiOpenApiTransformer.cs`, `BrowserApiJsonOptions.cs`, `Services/BrowserValidationProblem.cs`, `BrowserUserClaims.cs`; sửa `Program.cs`, Web csproj; tạo `shared/api/*`, `shared/lib/dateTime.ts`, `money.ts`, `test/.../BrowserApiContractTests.cs`, `shared/api/httpClient.test.ts`.

**Interfaces:** `MapBrowserApiEndpoints(...)`, `BrowserUserClaims.TryGetUserId(ClaimsPrincipal, out Guid)`; requestJson/requestForm/requestDownload và ApiProblem theo mục 4. Browser OpenAPI document name `browser-v1`, tách group `browser-v1`/`public-v1` khỏi group partner `v1`.

- [ ] Test mapping validation/forbidden/not-found/conflict/concurrency/rate-limit/unavailable, malformed JSON, undefined enum và trace ID; kiểm tra response là JSON, không có stack trace/PII.
- [ ] Branch exception handler theo browser/public prefix; thu hẹp handler partner đang bắt toàn `/api` để không nuốt browser errors, nhưng giữ output partner và toàn bộ Partner contract tests.
- [ ] Chốt browser serializer ở endpoint/type boundary; không thay JSON options toàn host làm đổi partner. Test camelCase/enum/UTC/pagination và money round-trip tại min/max/range có số lẻ của validator.
- [ ] Configure OpenAPI browser và generated artifact/types; kiểm tra OpenAPI version tương thích `openapi-typescript`; không hạ format partner để phục vụ generator. `api:generate` đọc artifact local và sinh `browser-api.d.ts`.
- [ ] Implement fetch wrapper có cookie, AbortSignal, CSRF cho mutation, hỗ trợ FormData/download/204; giữ status/code/traceId khi lỗi, xử lý response HTML bất thường như lỗi protocol.
- [ ] TanStack Query không retry mutation; read chỉ retry hữu hạn lỗi transport/5xx, không retry 401/403/validation/429 ngay lập tức. Thử POST timeout chứng minh chỉ gửi một lần.

**Verify:** Web contract + partner suites pass; httpClient tests cho 204/JSON/error/abort/multipart/timeout pass; regenerate contracts hai lần không tạo diff ngoài dự kiến.

### T02.02 — JSON authentication endpoints tương thích Identity hiện tại

**Files:** tạo `Endpoints/Browser/BrowserAuthenticationEndpoints.cs`, `BrowserAuthenticationContracts.cs`, `BrowserReferenceDataEndpoints.cs`, `Services/BrowserSessionReader.cs`; sửa `Program.cs`, Infrastructure Identity config chỉ khi cần; đọc/reuse `Endpoints/AuthenticationEndpoints.cs`, `Services/VietnamAdministrativeDivisionService.cs`; test `Browser/BrowserAuthenticationTests.cs`, `BrowserReferenceDataTests.cs` và giữ `AuthenticationAntiforgeryTests.cs`.

**Interfaces:** năm auth endpoint ở mục 4; csrf trả `{ requestToken }`, `me`/login/register thành công trả Session; logout trả 204. Session reader lấy user active và role/operation permission server-side. Anonymous `GET /api/v1/public/reference-data/provinces` trả tỉnh/phường đang được dùng trong app để register hoạt động đầy đủ ngay Phase 2; nguồn server vẫn là VietnamAdministrativeDivisionService.

- [ ] Test login/register/logout thiếu hoặc sai CSRF bị 400; login sai 401, lockout 423 với code ổn định, inactive bị từ chối; `me` anonymous 401, role denied 403, không redirect HTML.
- [ ] Reuse SignInManager/UserManager/RegisterShopService, chính sách password, lockout 5 lần/15 phút và Data Protection hiện có; không tạo password verifier/token store mới.
- [ ] Validate JSON/form field errors; password không đưa vào log, error, draft cookie hoặc response. Login/register phải AllowAnonymous rõ ràng dù parent group yêu cầu auth.
- [ ] Lấy identity từ principal; đọc lại trạng thái active/role khi tạo session và ở đường use case. Bảo toàn auth form cũ trong coexist và test regression cũ.
- [ ] Sau login/logout refresh antiforgery token theo identity mới; response auth/CSRF/session có no-store, cookie security giữ theo production policy hiện tại.
- [ ] Thêm anonymous reference endpoint với DTO chỉ có tên tỉnh/phường; kiểm tra không lộ protected data, data có thể dùng cho registration và canonicalization dùng cùng nguồn JSON. Không chuyển file authoritative sang frontend.

**Verify:** Browser authentication và auth form antiforgery suites pass; kiểm tra cookie thật bằng factory HandleCookies và browser, không chỉ fake principal.

### T02.03 — Login/register React và session navigation theo role

**Files:** tạo `shared/auth/*`, `features/auth/api.ts`, `LoginPage.tsx`, `RegisterShopPage.tsx`; sửa router/layouts; test `features/auth/AuthPages.test.tsx`, `e2e/auth.spec.ts`.

**Interfaces:** `useSession(): { session: Session | null; isLoading: boolean; reload(): Promise<void>; logout(): Promise<void> }`; `RequireSession`/role guard phục vụ UX.

- [ ] Test session loading không flash nội dung được bảo vệ; reload browser giữ cookie; expired session clear cache và chuyển login; 403 hiển thị trang không quyền.
- [ ] Dựng form AntD, disabled/loading, map server field errors, clear password sau failure cần thiết; không lưu credential vào storage.
- [ ] Giữ ưu tiên redirect hiện có: Admin/Operator → Operations, Shipper → workspace, Shop → dashboard; IntegrationAdmin-only → trang integrations có quyền. `returnUrl` chỉ local route hợp lệ, không open redirect.
- [ ] Register đầy đủ field Shop/address, validation và auto sign-in như hiện tại; dùng anonymous reference data endpoint T02.02, province đổi phải cập nhật ward; không dùng data giả.
- [ ] Menu đọc role/permission server trả; link đến feature chưa chuyển dùng điều hướng document sang Blazor, không để React router trả blank page.

**Verify:** registration/login/logout/remember-me/lockout/đổi tài khoản và direct protected route pass bằng cookie thật; không có phụ thuộc vào feature của phase sau.

### T02.04 — Shop context, permission và chống stale tenant cache

**Files:** tạo `Endpoints/Browser/BrowserShopContextEndpoints.cs`, `BrowserShopContextContracts.cs`, `features/shop-context/api.ts`, `ShopContextProvider.tsx`, `queryKeys.ts`; test `BrowserShopContextTests.cs`, `ShopContextProvider.test.tsx`, `e2e/shop-context.spec.ts`.

**Interfaces:** `GET /api/v1/app/shop-context?shopId=...` gọi IGetShopContextService; `useShopContext()` trả context, loading và `selectShop(shopId: string)`; chuyển Shop cập nhật URL.

- [ ] Test owner nhiều Shop, staff quyền hạn chế, Shop inactive, Shop ngoài quyền bị 403; profile PII trả đúng masking từ Application.
- [ ] Map ShopPermission flags thành string array ở browser adapter; không tự cấp owner quyền vì role Shop hoặc tin permission client gửi lên.
- [ ] Query key gồm userId/shopId/filters; abort old requests, bỏ stale response và clear sensitive state khi user/Shop đổi. Không dùng keepPreviousData xuyên tenant.
- [ ] Navigation/action được kiểm tra bằng permissions context; đơn vị authorization thật vẫn trong use case. Invalid selectedShop không tự tải dữ liệu một Shop không được phép.

**Verify:** test trì hoãn response Shop A, đổi sang B rồi response A về: UI chỉ hiển thị B; logout rồi login user khác không còn A/B cache.

## Phase 3 — Shop dashboard và shipment read flows

### T03.01 — API dashboard, shipment list và detail

**Files:** tạo `Endpoints/Browser/BrowserShopShipmentReadEndpoints.cs`, `BrowserShopReportingEndpoints.cs`, `BrowserShipmentReadContracts.cs`; đọc `Application/Shops/Reports`, `GetShipmentsForCurrentShop`, `GetShipmentDetailForCurrentShop`, `ShipmentRepository`; test `BrowserShopShipmentReadTests.cs`.

**Interfaces:** `GET /api/v1/app/shops/{shopId}/dashboard`, `/shipments`, `/shipments/{shipmentId}` dưới cùng Shop; gọi IGetShopDashboardKpiService, IGetShipmentsForCurrentShopService và IGetShipmentDetailForCurrentShopService với actor từ claims.

- [ ] Test owner/staff, cross-Shop, masked PII/COD, inactive Shop vẫn xem lịch sử, shipment not found; không trả EF entity hoặc field nội bộ.
- [ ] Bind đầy đủ status/tracking/receiver/date/COD/sort/page theo query hiện tại; server validation và permission cho PII filter, không lọc toàn bộ ở React.
- [ ] Regression UTC cuối ngày, invalid range/status/page size, total count/page một-based; giữ các giới hạn chuẩn hóa hiện tại của Application.
- [ ] Sinh browser OpenAPI/types và verify Partner artifact không đổi.

**Verify:** Browser read tests + ShopStaffPermissionTests/ShopProductionHardeningTests liên quan; kiểm tra request thật gọi paged service, không default full-list method.

### T03.02 — Shipment list/detail với URL state

**Files:** tạo `features/shipments/api.ts`, `queries.ts`, `ShipmentListPage.tsx`, `ShipmentDetailPage.tsx`, `shipmentFilters.ts`, components/test cùng feature; `e2e/shop-shipments.spec.ts`.

**Interfaces:** list query key theo user/Shop/filter/page; router `/shipments`, `/shipments/:id` qua routePaths; filters giữ tên query hiện có.

- [ ] Table server pagination/filter/sort; search chính và advanced filters; back/forward/reload giữ Shop/status/date/page; invalid URL input hiện lỗi an toàn.
- [ ] Detail hiển thị snapshot phí/địa chỉ/history đúng ShipmentDetailResponse được phép, không tự suy luận dữ liệu bị masking. DTO hiện không có assignment/proof; không tạo section giả hoặc mở rộng scope để lấp chỗ trống này.
- [ ] Error/empty/loading tách biệt; 403/404 khác nhau theo contract; nút mutation chưa có API thì không giả thành công hoặc sửa state local.
- [ ] Kiểm tra keyboard/focus/table horizontal scroll và mobile; link từ Blazor dashboard query cũ sang React giữ ý nghĩa filter.

**Verify:** component tests + E2E list/search/detail/back/reload dưới owner và staff hạn chế.

### T03.03 — Shop dashboard giữ các regression UX hiện tại

**Files:** tạo `features/dashboard/api.ts`, `queries.ts`, `ShopDashboardPage.tsx`, `statusDistribution.ts`, tests; tham chiếu `DashboardPresentation.cs`, DashboardRendering/Presentation/DrillDownTests; `e2e/dashboard.spec.ts`.

**Interfaces:** dashboard gọi T03.01 và build shipment URL bằng routePaths/filters của T03.02, không tự tạo format query thứ hai.

- [ ] Port các tình huống test: KPI lỗi không render 0, latest lỗi không giả empty, staff không quyền COD, inactive Shop, chỉ một Shop không có selector thừa.
- [ ] Phân bố đếm mỗi trạng thái đúng một lần, loại nhóm 0, empty không vẽ chart; không dựng dữ liệu tăng trưởng giả.
- [ ] KPI drill-down truyền đúng Shop/status/UTC date; giữ panel tài chính và bảng latest gọn theo dashboard review.

**Verify:** Vitest parity với tình huống HtmlRenderer cũ; E2E KPI → list có request filter đúng, không chỉ assert URL. Giữ Blazor tests đến khi cutover.

## Phase 4 — Reference data, quote và shipment mutations

### T04.01 — Administrative data và shipping quote

**Files:** tạo `Endpoints/Browser/BrowserShippingQuoteEndpoints.cs`, contracts; `features/shipments/quote/*`; dùng lại `BrowserReferenceDataEndpoints.cs` và module reference-data của auth từ T02.02–03, chuyển client reference data dùng chung sang `shared/api/referenceData.ts` nếu chưa ở shared; test `BrowserShippingQuoteTests.cs`, mở rộng `BrowserReferenceDataTests.cs` khi có behavior mới.

**Interfaces:** dùng anonymous `GET /api/v1/public/reference-data/provinces` đã có ở T02.02; authenticated `POST /api/v1/app/shops/{shopId}/shipping/quote` nhận parcel/address/value, trả route + fee breakdown.

- [ ] Endpoint dùng VietnamAdministrativeDivisionService/IAdministrativeDivisionService và IRouteClassificationService/IShippingFeeService; không copy bảng phí/phân tuyến vào TypeScript.
- [ ] Test tỉnh/phường chuẩn hóa/không hợp lệ, weight/dimension/value range và Shop ngoài quyền; quote phải kiểm tra quyền Shop ở use case/adaptor orchestration phù hợp, không chỉ UI.
- [ ] Debounce quote request, abort/ignore response cũ; fee preview không bao giờ trở thành giá server tin cậy khi create.
- [ ] Dùng reference data chung cho pickup/delivery, giữ registration T02.03 không regression; backend giữ JSON authoritative trong wwwroot/data.

**Verify:** reference/quote tests, fee/domain suites liên quan và register E2E pass; không có protected data trong anonymous reference response.

### T04.02 — Create/draft/edit/submit/cancel API

**Files:** tạo `BrowserShopShipmentCommandEndpoints.cs`, `BrowserShipmentCommandContracts.cs`; đọc Application/CreateShipment, DraftShipments, CancelShipmentForCurrentShop; test `BrowserShopShipmentCommandTests.cs`.

**Interfaces:** `POST .../shops/{shopId}/shipments`, `POST .../shipments/drafts`, `PUT .../shipments/{id}`, `POST .../{id}/submit`, `POST .../{id}/cancel`; request DTO không có CreatedByUserId/fee/role tin cậy.

- [ ] Test success/validation/inactive Shop/staff không ManageShipments/cross-Shop; request giả actor không được thay người thực hiện.
- [ ] Map từng operation về use case hiện có; status hợp lệ, draft không auto assign và submit tính lại phí theo behavior thật; không gộp workflow partner vào UI.
- [ ] Chuyển create limiter từ Razor sang HTTP ingress dùng service store hiện tại; 429 giữ Retry-After. Không bỏ limiter UI cũ khi Blazor còn hoạt động.
- [ ] Map invalid transition/conflict/concurrency/capacity; không thêm automatic retry tạo shipment khi connection đứt sau commit; không đổi transaction/outbox của use case.

**Verify:** browser command tests + ShipmentBusinessRuleTests/ShopProductionHardeningTests liên quan; audit actor là user đăng nhập.

### T04.03 — Shipment form và action flows React

**Files:** tạo `features/shipments/ShipmentFormPage.tsx`, `ShipmentForm.tsx`, `shipmentFormMapping.ts`, `ShipmentActions.tsx`, tests; `e2e/shipment-lifecycle-shop.spec.ts`.

**Interfaces:** dùng quote T04.01 và mutation T04.02; create route dùng query `shipmentId`/`shopId` tương thích link sửa hiện có, mapping chi tiết ghi ở inventory.

- [ ] AntD Form chia sender/receiver/address/parcel/COD/note, prefill đúng Shop, dependent province/ward, validation và focus field lỗi đầu tiên.
- [ ] Hỗ trợ create, save draft, edit draft/pending pickup, submit draft và cancel có confirmation; permission/active/status quyết định action, server kiểm tra lại.
- [ ] Disable action khi pending, invalidate đúng dashboard/list/detail sau success; 409 yêu cầu reload trước thử lại; timeout hiển thị hướng kiểm tra đơn đã tạo, không tự gửi lại.
- [ ] Giữ số liệu preview, kết quả server và tracking rõ ràng; không format tiền thành string locale rồi gửi ngược như số.

**Verify:** E2E create/draft/edit/submit/cancel, double click chỉ một request đang chạy, fee từ server và cross-Shop/staff negative paths pass.

## Phase 5 — CSV import, export và label

### T05.01 — Import API và batch contract

**Files:** tạo `BrowserShipmentImportEndpoints.cs`, contracts; đọc `Application/Shipments/ImportShipments`, `ShipmentImportBatchProcessor`; test `BrowserShipmentImportTests.cs`.

**Interfaces:** `POST .../shops/{shopId}/shipment-imports/preview` multipart CSV; `POST .../shipment-imports/confirm` nhận rows đã sửa; `GET .../shipment-imports/{batchId}`; gọi IPreview/IConfirm/IGetShipmentImportBatchService.

- [ ] Server kiểm tra file tối đa `2_000_000` bytes cho phần file, có multipart request limit bao gồm overhead; reject oversized/chất liệu không hợp lệ an toàn, không log raw CSV.
- [ ] Test parse/row validation, duplicate row/client code theo behavior hiện tại, cross-Shop và thiếu ManageShipments; confirm phải revalidate, không tin preview client.
- [ ] Move preview/confirm limiter vào endpoint; giữ threshold và 429; batch status đọc qua Application, không query DbContext từ endpoint.
- [ ] Giữ worker/lease/outbox và response sync/async hiện có; lỗi row phải có row number/code không làm lộ tenant khác.

**Verify:** Browser import + Application import cases; LocalDB test liên quan nếu chạm transaction/worker, không thay bằng UI fake.

### T05.02 — Import preview/confirm/polling React

**Files:** tạo `features/shipment-import/api.ts`, `queries.ts`, `ShipmentImportPanel.tsx`, `ImportPreviewTable.tsx`, tests; `e2e/shipment-import.spec.ts`.

**Interfaces:** preview → edit/confirm → batch polling mỗi 2 giây → kết quả; poll chỉ khi batch chưa terminal và view đúng user/Shop.

- [ ] AntD Upload chọn CSV không auto-upload ngoài flow; mẫu CSV đặt trong panel import; row error/success/count hiển thị riêng.
- [ ] Test sửa row sau preview vẫn được server validate lại; chặn confirm lặp trong khi request pending, không tự retry mutation khi timeout.
- [ ] Poll dừng khi terminal/unmount/logout/đổi Shop; abort request cũ, không cập nhật UI Shop mới bằng batch cũ; error có retry thủ công.

**Verify:** fake-timer component tests và E2E preview-invalid/confirm/async-complete/download errors; không chờ thật nhiều giây trong unit test.

### T05.03 — Download/export/label qua cookie

**Files:** tạo `features/shipments/downloads.ts`, buttons; dùng `ShopShipmentFileEndpoints.cs` hiện tại, chỉ sửa endpoint nếu contract adapter thật sự cần; test `BrowserFileDownloadTests.cs`, `e2e/downloads.spec.ts`.

**Interfaces:** giữ `/shop/files/shipments/export.csv`, `/cod-report.csv`, `/shipments/{shipmentId}/label.pdf`, `/import-batches/{batchId}/errors.csv`; giữ query Shop/filter hiện có.

- [ ] Dùng requestDownload giữ cookie, filename Content-Disposition và MIME; xử lý 400/401/403/429/HTML login response an toàn, revoke object URL sau tải.
- [ ] Test quyền ExportData/ViewCod/label và Shop isolation thực qua Application; không tạo URL chỉ dựa vào UI gate.
- [ ] Filter export trùng filter list, CSV giữ escaping/formula protection hiện có, PDF là binary không đi qua JSON parser.

**Verify:** browser download event nhận file đúng loại/tên, request Shop/filter đúng, denied/rate-limit có feedback; không đổi public file contract ngoài phạm vi.

## Phase 6 — Operations workspace

### T06.01 — Operations read API và permission projection

**Files:** tạo `BrowserOperationsReadEndpoints.cs`, contracts; dùng GetOperationsShipments/GetPendingPickupShipments/GetActiveShippers/GetCodSettlementCandidates; test `BrowserOperationsReadTests.cs`.

**Interfaces:** GET `/api/v1/app/operations/shipments`, `/pending-pickups`, `/shippers`, `/cod/settlement-candidates`; lọc/page/sort như current workspace.

- [ ] Test Admin/Operator được đọc theo OperationPermissions, Shop/Shipper denied; COD settlement candidate và audit field theo permission, không chỉ menu role.
- [ ] Đối chiếu service nào chưa tự kiểm quyền (ví dụ lookup shipper); bổ sung kiểm tra vào use case hoặc focused authorized query trước khi expose, không mở raw internal service.
- [ ] DTO chỉ gồm capacity/working area/warning field UI cần; pagination tại SQL/Application hiện có, không full-table fetch để React lọc.

**Verify:** Browser Operations read + OperationsP2Tests liên quan; permission checks tồn tại ở use case.

### T06.02 — Assignment command API

**Files:** tạo `BrowserOperationsAssignmentEndpoints.cs`, contracts; đọc AssignShipperToShipment, AutoAssignShipment, BulkRetryAutoAssignment, ReassignShipment, CancelShipmentAssignment; test `BrowserOperationsAssignmentTests.cs`.

**Interfaces:** POST `/operations/shipments/{id}/assign`, `/auto-assign`, `/reassign`, `/cancel-assignment`; POST `/operations/assignments/bulk-retry` với IDs/filters đúng use case, không actor trong body.

- [ ] Test assign pending pickup, reassign/cancel chỉ Assigned, active/area warning/capacity, duplicate/inactive selection và role denied.
- [ ] Giữ capacity guard/transaction/auto retry tối đa ba attempt bên server; browser không thêm loop auto-retry.
- [ ] Bulk result giữ kết quả từng shipment, partial failure có thể review; không hiển thị mọi row success khi chỉ một phần thành công.

**Verify:** Browser assignment + OperationsP2Tests; chạy `ConcurrencyAndRegistrationTransactionTests` cho capacity khi adapter thay đường gọi, giữ SQL locking behavior.

### T06.03 — Status và COD API cho Operations

**Files:** tạo `BrowserOperationsActionEndpoints.cs`, contracts; dùng UpdateShipmentStatus/MarkCodCollected/MarkCodSettled; test `BrowserOperationsActionTests.cs`. Backend ProofOfDelivery/tests hiện có được giữ, không tự thêm proof UI/API khi migration tương đương.

**Interfaces:** POST `/operations/shipments/{id}/status`, `/cod/collect`, `/cod/settle`; các path có prefix `/api/v1/app`. Không có GET proof use case trong inventory UI để giả định expose sẵn.

- [ ] Test failure reason/note/GPS validation theo UpdateShipmentStatusCommand, invalid transition và actor audit; giữ backend proof capability/tests nhưng chỉ mở rộng proof UI/API trong task riêng khi được đặt trong scope.
- [ ] Test Operator thu COD được phép nhưng settle bị 403; Admin settle được; delivered/pending/collected invariant giữ nguyên.
- [ ] Đảm bảo request duplicate/stale trả conflict đúng use case; mutation không được retry tự động; giữ outbox/audit/notification behavior.

**Verify:** browser action + COD/domain/Application permission tests, không suy luận đúng từ status nút UI.

### T06.04 — Operations React workspace và dialogs

**Files:** tạo `features/operations/api.ts`, `queries.ts`, `OperationsPage.tsx`, `OperationsFilters.tsx`, `AssignmentTable.tsx`, `AssignShipperModal.tsx`, `ReassignCancelModal.tsx`, `BulkRetryPanel.tsx`, `CodSettlementPanel.tsx`, tests; `e2e/operations.spec.ts`.

**Interfaces:** dùng T06.01–03; đối chiếu toàn bộ component Operations đang có, OperationsAssignmentUiModels và OperationsDisplay.

- [ ] Port filter/summary/list/selection/row messages, capacity/area warning; không ghi đè sửa đổi UI chưa commit khi đọc baseline.
- [ ] Dialog giữ selected shipment, lý do và confirmation; clear selection không còn hợp lệ sau reload/filter/Shop scope thay đổi.
- [ ] Action status/COD theo permission và trạng thái, refresh đúng query sau mutation; row pending không khóa vô cớ toàn workspace.
- [ ] Test bulk partial failure, stale capacity, 409 reload, keyboard modal/focus-return, mobile table controls.

**Verify:** Vitest behavior port từ OperationsAssignmentUiModelsTests và E2E Admin/Operator đầy đủ; alias `/operations/assignment` được giữ trong route map.

## Phase 7 — Shipper mobile workspace

### T07.01 — API chỉ cho shipper đăng nhập

**Files:** tạo `BrowserShipperEndpoints.cs`, contracts; dùng GetAssignedShipmentsForShipper, GetShipperWorkingAreas, GetShipperCodDailySummary, SetShipperAvailability, UpdateShipmentStatus, MarkCodCollected; test `BrowserShipperTests.cs`.

**Interfaces:** GET `/api/v1/app/shipper/me/workspace`, `/profile`, `/working-areas`, `/cod-summary`; PUT `/availability`; POST `/shipments/{id}/status`, `/cod/collect` dưới `/shipper/me`. Profile là focused self read thay vì gọi lookup toàn bộ active shipper từ browser.

- [ ] Actor luôn lấy claims; không nhận `shipperUserId` để tự chọn workspace người khác. Không expose danh sách mọi shipper chỉ để lấy profile bản thân.
- [ ] Test assignment ngoài quyền, user inactive, unassigned shipment, invalid status/note/failure reason và COD chưa delivered.
- [ ] DTO giữ stage/filter/page/availability/capacity UI thực sự dùng; quyền và active assignment kiểm tra lại trong Application.

**Verify:** browser shipper + Application shipper/status/COD cases, người A không đọc/cập nhật shipment của B.

### T07.02 — Shipper React ưu tiên điện thoại

**Files:** tạo `features/shipper/api.ts`, `queries.ts`, `ShipperWorkspacePage.tsx`, `ShipmentTaskCard.tsx`, `StatusActionForm.tsx`, tests; `e2e/shipper.spec.ts`.

**Interfaces:** dùng T07.01, shared status/error/confirm; route `/shipper/shipments` qua routePaths.

- [ ] Stage tabs/filter/page và COD daily summary; card/action stack ở 390px, nút đủ kích thước, không phải kéo ngang mới thấy action chính.
- [ ] Status flow, note/failure reason, confirm thu COD và availability toggle; không thêm form proof hoặc yêu cầu geolocation ngoài UI/contract hiện có.
- [ ] Pending/error/success theo shipment, refresh tránh double collect; failed network cho phép kiểm tra lại trạng thái trước gửi lại.

**Verify:** component tests và E2E viewport mobile/desktop; delivered COD pending vẫn có action thu tiền.

### T07.03 — Kiểm chứng lifecycle qua Shop → Operator → Shipper → Admin

**Files:** tạo `e2e/cross-role-lifecycle.spec.ts`; mở rộng test Browser phù hợp và validation log; đọc ShipmentStateMachineTests/CodTransactionTests.

**Interfaces:** dùng API/UI đã có; fixture riêng cho mỗi flow, không sửa database dùng chung hoặc bật seed production.

- [ ] Flow create → assign → pickup → in transit → delivering → delivered → collect → settle, assertions cả trạng thái UI và server response.
- [ ] Delivered có COD pending giữ assignment active; collect kết thúc assignment; delivered không COD/returned/cancelled có behavior kết thúc tương ứng.
- [ ] Flow failed delivery có reason/note rồi retry/return; state terminal không bị mutation; Operator không settle.
- [ ] Kiểm tra audit/history và side effect bằng integration tests hiện có, không chỉ mock trình duyệt.

**Acceptance:** luồng nghiệp vụ xuyên role có bằng chứng chạy thật; failure trong Domain/Application không được chữa bằng cách nới UI guard.

## Phase 8 — Shop settings và reporting

### T08.01 — Profile và tạo thêm Shop

**Files:** tạo `BrowserShopProfileEndpoints.cs`; `features/shop-settings/ShopProfilePage.tsx`, API/queries/tests; `e2e/shop-profile.spec.ts`; dùng GetShopProfile/UpdateShopProfile/CreateAdditionalShop.

**Interfaces:** GET/PUT `/api/v1/app/shops/{shopId}/profile`, POST `/api/v1/app/shops` tạo thêm Shop; context refetch sau thành công.

- [ ] Test owner/staff ManageShopProfile, masked profile, validation địa chỉ/phone và Shop inactive theo use case.
- [ ] Form AntD, chọn địa chỉ từ reference data; tạo Shop mới cập nhật accessible shops, không tự cấp quyền bằng state client.
- [ ] Đổi profile không làm đổi address snapshot của shipment cũ; actor và audit giữ nguyên behavior.

**Verify:** Browser profile + ShopProfileManagementTests và E2E context sau tạo thêm Shop.

### T08.02 — Shop staff và permission management

**Files:** tạo `BrowserShopStaffEndpoints.cs`; `features/shop-settings/ShopStaffPage.tsx`, `StaffPermissionForm.tsx`, tests; `e2e/shop-staff.spec.ts`; dùng IShopStaffManagementService.

**Interfaces:** GET/POST `/shops/{shopId}/staff`, PUT `/shops/{shopId}/staff/{staffUserId}`; bind ShopStaffResponse.UserId vào UpdateShopStaffAccessCommand.StaffUserId, không dùng MembershipId. Action active/status theo command hiện tại; không invent quyền quản lý staff mới chỉ từ Shop role.

- [ ] Test owner/admin behavior đúng service, staff không được tự nâng quyền hoặc sửa membership Shop khác; role/flags được validate server.
- [ ] UI role template/permission checkbox map tên chính xác; có confirmation deactivate/thay quyền nhạy cảm và feedback safe.
- [ ] Sau thay quyền, refetch context/session liên quan; action bị thu hồi phải bị server từ chối dù tab cũ vẫn hiện nút.

**Verify:** ShopStaffPermissionTests + browser endpoint + E2E staff hạn chế/COD/PII/export denied.

### T08.03 — COD report, notifications và Shop audit

**Files:** tạo `BrowserShopSettingsReadEndpoints.cs`, `BrowserShopNotificationEndpoints.cs`; `ShopCodReportPage.tsx`, `ShopNotificationsPage.tsx`, `ShopAuditPage.tsx` cùng feature; tests `BrowserShopSettingsTests.cs`, `e2e/shop-settings.spec.ts`.

**Interfaces:** GET `/shops/{shopId}/cod-report`, `/notifications`, `/audit`; notification read/preferences mutations theo IShopNotificationService; COD export dùng T05.03.

- [ ] Test ViewCod/ViewAudit/ManageNotifications/ExportData tách biệt; date/filter/page và PII masking giữ đúng Application.
- [ ] Reports render count/amount từ DTO thật; notification read/settings update refresh badge/query; audit read-only, không lộ raw secret.
- [ ] Không tự thêm settlement quyền Shop; verify staff thiếu quyền không nhận field nhạy cảm từ API dù component không render.

**Verify:** Browser settings + Application reporting/notification/audit cases; E2E mỗi route và export theo quyền.

## Phase 9 — Administration và banner I/O

### T09.01 — Admin dashboard, COD và audit

**Files:** tạo `BrowserAdminReportingEndpoints.cs`, contracts; `features/administration/AdminDashboardPage.tsx`, `AdminCodPage.tsx`, `AdminAuditPage.tsx`, API/queries/tests; `e2e/admin-reporting.spec.ts`.

**Interfaces:** GET `/api/v1/app/admin/dashboard`, `/cod-report`, `/audit`; settle dùng command đã bảo vệ ở T06.03; IGetAdminDashboardService/IGetAdminCodReportService/IGetAdminAuditLogsService.

- [ ] Test Admin allowed, Operator/Shop/IntegrationAdmin denied đúng quyền; dashboard scopes tài chính/capacity/webhook không bị trộn số liệu khác thời gian.
- [ ] Port KPI/COD/capacity/integration summary, error/empty đúng dashboard review; audit filter/page, settle confirmation và server conflict.
- [ ] Giữ regression query translation của AdminDashboardMetricsRepository; không thay SQL bằng tính toàn bảng trên React.

**Verify:** Browser admin reporting + AdminDashboardAndCodReportServiceTests + AdminDashboardMetricsRepositoryTests khi cần, E2E alias `/admin`/`/admin/dashboard`.

### T09.02 — User/Shop administration

**Files:** tạo `BrowserAdminUserEndpoints.cs`, `BrowserAdminShopEndpoints.cs`; `AdminUsersPage.tsx`, `AdminShopsPage.tsx`, forms/tests; `e2e/admin-users-shops.spec.ts`.

**Interfaces:** admin users list/create/active/password-reset/capacity/working-area và shops list/active map đúng IGetAdminUsers/ICreateInternalUser/IResetUserPassword/ISetUserActiveStatus/ISetShipperCapacity/ISetShipperWorkingAreas/IGetAdminShops/ISetShopActiveStatus services.

- [ ] Test Admin-only tại HTTP và Application; validation role/password/capacity/area, inactive user/Shop và cross-scope action.
- [ ] Không trả password/hash/security stamp; password reset input không giữ trong cache/log/form sau success. Giữ thông báo lỗi safe, không hiển thị raw exception.
- [ ] Confirmation cho active/reset/capacity, refresh đúng danh sách; không thay capacity locking hoặc assignment guard.

**Verify:** Browser admin user/shop tests + Application admin action/audit tests và E2E negative role paths.

### T09.03 — Hub, route và fee system configuration

**Files:** tạo `BrowserAdminHubEndpoints.cs`, `BrowserAdminConfigurationEndpoints.cs`; `AdminHubsPage.tsx`, `AdminSystemConfigPage.tsx`, forms/tests; `e2e/admin-config.spec.ts`.

**Interfaces:** hub CRUD/active theo AdminHubs services; configuration read/write theo IAdminSystemConfigurationService. Request giữ province/route/fee version semantics hiện có.

- [ ] Test Admin-only, invalid ranges/duplicate active/conflict; server khóa và version active vẫn authoritative.
- [ ] UI trình bày version/phạm vi áp dụng, confirm cập nhật và reload khi stale; không thay snapshot shipment đã tạo.
- [ ] Giữ invalidation route/fee/hub cache hiện có và configuration-generation Redis; không cache cấu hình vô thời hạn ở frontend.

**Verify:** Browser config + SystemConfigurationP3Tests; LocalDB SystemConfigurationConcurrencyTests; Redis cache test chạy khi có môi trường quy định.

### T09.04 — Banner authorization, upload storage và management UI

**Files:** sửa `Application/Banners/IBannerService.cs`, `BannerService.cs`, mọi caller (kể cả Razor còn tồn tại); tạo `Application/Banners/IBannerImageStorage.cs`, `IUploadBannerImageService.cs`, `UploadBannerImageService.cs`, `Infrastructure/Persistence/Storage/LocalBannerImageStorage.cs`, browser banner endpoints/contracts; sửa DI; tạo `AdminBannersPage.tsx`, tests Application/Infrastructure/Web và `e2e/admin-banners.spec.ts`.

**Interfaces:** giữ GetActiveBannersAsync anonymous-safe; thêm `Guid actorUserId` và CancellationToken vào các thao tác GetAll/Create/Update/Toggle/Delete; upload `UploadAsync(Guid actorUserId, Stream content, string fileName, string contentType, CancellationToken)` trả Result DTO chứa image URL. Storage port có nhu cầu cụ thể là chuyển I/O khỏi Razor.

- [ ] Thêm regression test mutation/list-admin/upload từ non-Admin bị từ chối tại use case; cập nhật toàn bộ caller cũ để coexist vẫn compile, không giữ overload bypass authorization.
- [ ] Tách ghi stream/file name/path khỏi component và endpoint vào Infrastructure adapter; endpoint chỉ nhận multipart giới hạn, derive actor, gọi use case. Giữ 5MB và định dạng hiện có đã inventory, validate nội dung/path/name ở server.
- [ ] File upload runtime nằm ở storage có persistence xuyên release; giữ URL ảnh cũ. Copy static frontend không được ghi đè/xóa upload; không đổi sang external cloud service trong migration.
- [ ] UI dùng AntD Upload/Form/Table, preview/order/active/delete confirmation; không tạo backend metadata thành công khi upload thất bại.
- [ ] Test lỗi size/extension/path traversal/storage failure/unauthorized và restart/publish giữ ảnh; public endpoint chỉ trả active banners.

**Verify:** banner Application authorization, storage integration và browser tests; E2E upload/create/update/toggle/delete. Nếu format hiện có không thể phục vụ an toàn theo policy hiện tại, ghi thành vấn đề cần giải quyết riêng trước khi mở upload, không tự âm thầm đổi định dạng.

## Phase 10 — Integration management portal

### T10.01 — API clients, scopes và key lifecycle

**Files:** tạo `BrowserIntegrationClientEndpoints.cs`, contracts; `features/integrations/IntegrationClientsPage.tsx`, `ClientScopeForm.tsx`, `OneTimeSecretModal.tsx`, tests; `e2e/integration-clients.spec.ts`.

**Interfaces:** `/api/v1/app/integrations/...` gọi IApiClientManagementService và IIntegrationScopeService; giữ global/Shop/province management scope. Không dùng API key partner làm credential đăng nhập React.

- [ ] Test Shop/Admin/IntegrationAdmin theo phạm vi quản lý thật, staff ManageIntegrations và cross-Shop/province denied; role đơn thuần chưa đủ.
- [ ] Create/rotate/revoke/active/scope/env theo service hiện có; key chỉ hiện một lần trong modal, no-store, không đưa query cache/storage/log/URL.
- [ ] Copy secret có confirmation feedback; đóng modal/logout/đổi scope phải clear secret; rotate timeout không tự retry làm mất key vừa tạo.

**Verify:** Browser integration client + PartnerIntegrationP3Tests/PartnerApiServiceTests liên quan; E2E one-time display và authorization; partner endpoints không đổi.

### T10.02 — Webhook management, delivery history và audit

**Files:** tạo `BrowserIntegrationWebhookEndpoints.cs`, contracts; `WebhookSettingsPanel.tsx`, `WebhookDeliveryTable.tsx`, `IntegrationAuditPanel.tsx`, tests; `e2e/integration-webhooks.spec.ts`.

**Interfaces:** browser endpoints gọi IWebhookManagementService; URL/event/active/test/retry/secret actions đúng service; audit/dashboard data theo scope hiện có.

- [ ] Test unauthorized/scope denied, invalid webhook URL, test/retry validation; không có fetch webhook URL từ browser để thay transport bảo mật server.
- [ ] Giữ DNS/connect-time validation, signing, no redirect/no proxy, timeout/size và retry policy; UI chỉ gọi command rồi đọc kết quả.
- [ ] SigningSecret chỉ là write-only input của upsert hiện có; không thêm reveal/rotate API cho secret đã lưu. Clear input sau save; error/delivery response chỉ hiển thị safe DTO, không raw body/PII không được phép. One-time reveal/rotate ở T10.01 áp dụng API client key.
- [ ] Retry có confirmation và không client auto retry; refresh delivery/audit sau command không làm request gửi trùng.

**Verify:** Browser webhook + PartnerIntegrationP3Tests và Infrastructure webhook/security suites liên quan; E2E settings/test/retry/audit theo scope.

## Phase 11 — Public pages, QA, release và loại bỏ Blazor

### T11.01 — Public landing, FAQ và contact

**Files:** tạo `Endpoints/Browser/BrowserPublicContentEndpoints.cs`; `features/public/HomePage.tsx`, `FaqPage.tsx`, `ContactPage.tsx`, tests; chuyển ảnh tĩnh cần dùng vào frontend public; `e2e/public-pages.spec.ts`.

**Interfaces:** GET `/api/v1/public/banners` gọi GetActiveBannersAsync, chỉ safe public fields; các route `/`, `/faq`, `/contact` qua PublicLayout.

- [ ] Port nội dung/assets/links/responsive hiện có, dùng AntD phù hợp mà không ép landing vào dashboard layout.
- [ ] Không thêm submit contact giả hoặc backend chức năng chưa có; metadata/title/link behavior giữ tương đương.
- [ ] Banner active lấy từ API, fallback/error không làm crash landing; kiểm tra URL/static asset/image cũ và mới.

**Verify:** public E2E anonymous và responsive; không ảnh hưởng auth/Shop route; ghi giới hạn SPA SEO, không ngầm thêm SSR.

### T11.02 — Public tracking summary và rate limit

**Files:** tạo `BrowserPublicTrackingEndpoints.cs`, contracts; `features/public/TrackingPage.tsx`, API/tests; `e2e/public-tracking.spec.ts`; đọc IGetPublicTrackingService và Tracking.razor.

**Interfaces:** GET `/api/v1/public/tracking/{trackingCode}` trả summary + timeline safe + `serverTimeZoneId` cho hiển thị thời điểm; gọi GetAsync(trackingCode, phoneLast4: null, requestAborted).

- [ ] Giữ behavior summary hiện tại: draft/not found không lộ đơn, tên mask, không phone/address đầy đủ; không đưa full payload xuống rồi ẩn bằng CSS.
- [ ] Move public tracking limiter vào HTTP path với IP lấy từ trusted forwarded headers hiện có; giữ threshold/store, retry-after/error; không dùng `unknown-circuit` làm key chung.
- [ ] Không render form phone-last-four chưa nối backend; verified flow là task chức năng riêng ngoài plan này, có acceptance/abuse tests riêng khi được yêu cầu.
- [ ] AntD search/result/timeline, trạng thái/rate-limit/error/localized public message và keyboard/mobile hoạt động.

**Verify:** Public tracking Application tests + browser summary/rate-limit/no-PII tests và E2E found/not found/429; không nhận claim giả từ client.

### T11.03 — Cross-feature regression, accessibility và parity sign-off

**Files:** mở rộng `e2e/*`, frontend feature tests và `docs/Frontend/react-migration-validation.md`; inventory từ Phase 0. Không tự khôi phục `test/manual/*` đang bị xóa trong working tree; scenario cần giữ được ghi ở validation document mới.

**Interfaces:** mỗi hàng inventory có test/QA evidence hoặc khoảng trống ghi rõ; không coi manual report mẫu là kết quả test thật.

- [ ] Chạy role matrix Admin/Operator/Shop owner/Shop staff/Shipper/IntegrationAdmin; anonymous/expired session/inactive user/Shop, revoked permission và cross-Shop tampering.
- [ ] Kiểm thử error/loading/empty/partial failure/429/409/network timeout, browser back/reload, stale request/Shop switch, download/upload và query persistence.
- [ ] Kiểm tra 390/768/1024/1440px, zoom 200%, keyboard/focus/label/contrast/AntD portals; shipper mobile, bảng scroll đúng region, status có text.
- [ ] Regression timezone/UTC cuối ngày/leap day và offset/DST; money scale/range và enum không hợp lệ. Không mở thêm timezone feature trong lúc sửa lỗi parity.
- [ ] Lazy load feature theo route, kiểm tra request waterfall/duplicate/size từ build, không tải Admin/Integration bundle cho public route; không đặt ngưỡng hiệu năng tùy ý khi chưa có baseline.
- [ ] Chuyển assertions có giá trị từ DashboardRenderingTests/DashboardDrillDownTests/OperationsAssignmentUiModelsTests sang React/API tests; giữ test backend chưa phụ thuộc Blazor.

**Verify:** chạy toàn bộ mục 7 và đối chiếu inventory; không có P0/P1 regression chưa xử lý trước root cutover, giới hạn còn lại ghi rõ trong validation log.

### T11.04 — Publish artifact và CI full stack

**Files:** hoàn thiện Web csproj publish target, `.github/workflows/frontend-ci.yml`, workflow .NET nếu cần, thêm `scripts/Publish-WebWithFrontend.ps1`; cập nhật deploy example/docs; test published-host smoke.

**Interfaces:** script build frontend rồi `dotnet publish` Web với `BrowserSpa:Enabled` tương ứng môi trường; runtime config quyết định basename, asset prefix cố định để cùng dist dùng được ở `/app` hoặc root.

- [ ] Từ checkout sạch chạy npm ci/typecheck/lint/unit/build, .NET build/test + browser OpenAPI/types drift check; giữ nguyên partner drift/compatibility check.
- [ ] Publish ra thư mục workspace riêng; start published process với môi trường test được chỉ định, không migrate/seed production; Playwright smoke trên process đó.
- [ ] Cache policy: index/runtime config revalidate, hashed assets immutable; assets mất trả 404; gzip/compression theo host hiện có nếu được cấu hình, không thêm hạ tầng không cần thiết.
- [ ] Kiểm tra release upload banner không bị ghi đè, health/worker/Data Protection/Redis configuration và design-time OpenAPI vẫn đúng; logs không chứa credential/PII.
- [ ] CI E2E dùng LocalDB/test fixture và secret môi trường; install browser trong CI, artifacts trace/screenshot chỉ khi fail và không chứa key/password/storage state.

**Verify:** full publish artifact phục vụ `/app` được, deep link refresh và file/API/health đúng; lockfile/contracts không drift. `dotnet publish` không phụ thuộc npm tải ngầm trong design-time host.

### T11.05 — Root route cutover và rollback rehearsal

**Files:** sửa BrowserSpaHostingExtensions/options, route config, Razor route coexist cần thiết, deployment docs; test `BrowserRootRoutingTests.cs`, `e2e/root-cutover.spec.ts`.

**Interfaces:** `BrowserSpa:Enabled=true`, `BrowserSpa:ServeAtRoot=true`; chỉ một owner cho từng URL. Bản release chuyển route giữ Blazor code cho rollback, bản cleanup ở T11.06 mới xóa.

- [ ] Khi root mode bật, không map Razor routes trùng React; giữ `/api`, `/auth` cũ còn cần, `/culture`, `/shop/files`, `/health`, `/openapi` riêng. Unknown navigation trả React NotFound; API/asset unknown giữ HTTP status đúng.
- [ ] Chuyển routePaths về root qua runtime config; giữ `/admin/dashboard` và `/operations/assignment` alias, old shipment edit/query link và auth returnUrl. Redirect `/app/...` legacy sang route root có query được validate.
- [ ] Chạy smoke mọi route cũ sau refresh/deep link và session cookie; không yêu cầu reset tài khoản/dữ liệu.
- [ ] Rehearse rollback: tắt root mode để Blazor phục vụ URL cũ hoặc dùng artifact trước đó; xác nhận release trước/sau cùng schema, upload/Data Protection store, không cần rollback database.
- [ ] Ghi exact artifact/config/commands và bằng chứng staging; deployment thực tế chỉ trong môi trường người dùng đặt trong phạm vi, không tự suy ra quyền production deploy từ yêu cầu lập plan.

**Acceptance:** parity và publish checks pass trước khi switch; rollback đã thử được. Chưa xóa Blazor trong cùng checkpoint cutover.

### T11.06 — Gỡ Blazor, dọn dependencies có chứng cứ và cập nhật tài liệu

**Files:** xóa Razor components/scoped CSS/JS chỉ còn phục vụ Blazor; sửa Program.cs, Web csproj, test Web phụ thuộc renderer; hoàn thiện README, docs/Frontend, docs/Backend/development.md, ADR-004, `.agents/project.md`, standards/commands/known-issues và deploy docs theo thay đổi thực tế. Giữ ADR-002 làm lịch sử quyết định backend; nếu có thay đổi quyết định cần ADR mới/superseding rõ ràng.

**Interfaces:** Web cuối cùng chỉ host HTTP/static/health/OpenAPI/workers; tất cả UI React. Giữ Application OperationScopedServiceProxy trừ task refactor riêng sau này.

- [ ] Chỉ bắt đầu sau cutover validation/rollback checkpoint đạt; ghi bản release trước cleanup để rollback bằng artifact khi Blazor không còn source trong bản mới.
- [ ] `rg` mọi caller trước khi xóa AddRazorComponents/InteractiveServer/CascadingAuthenticationState/Blazicons/Bootstrap/ReconnectModal và UI-only helpers/resources.
- [ ] Giữ endpoint/Identity/persistence/worker/rate-limiter/Data Protection/static administrative JSON và upload runtime còn được dùng; UiDisplay/Resources/DashboardPresentation chỉ xóa khi đã có replacement và không còn caller backend.
- [ ] Xóa HtmlRenderer tests chỉ sau khi assertion tương đương đã có ở React/API; giữ Partner/auth/security/Application/Infrastructure tests còn giá trị. Auth form endpoint cũ chỉ bỏ sau khi không còn form/caller và đã điều chỉnh regression liên quan.
- [ ] Hoàn thiện tài liệu setup Node/npm + .NET, dev proxy, contracts/types, AntD guideline, testing/publish/rollback đã được cập nhật dần ở các task trước; không dồn cập nhật agent context đến cleanup. Dashboard review giữ lịch sử và link behavior mới, không tuyên bố visual QA nếu chưa chạy.
- [ ] Chạy full validation mục 7; inspect final diff không migration/unrelated/user file/build output/secret; xác nhận no Blazor runtime dependency trong published UI bằng network smoke.

**Acceptance:** React phục vụ đủ inventory; backend guardrails không đổi; docs phản ánh source, solution/publish/tests pass có bằng chứng; không còn bundle/circuit Blazor được browser tải.

## 7. Lệnh validation chuẩn khi bắt đầu thực thi

### 7.1 Backend baseline và task scope

Chạy từ repository root:

```powershell
dotnet restore Mini-logistics-manegemant-system.slnx
dotnet build Mini-logistics-manegemant-system.slnx -c Release
dotnet test test/MiniLogistics.Web.Tests/MiniLogistics.Web.Tests.csproj -c Release --filter FullyQualifiedName~Browser
dotnet test test/MiniLogistics.Application.Tests/MiniLogistics.Application.Tests.csproj -c Release
dotnet test Mini-logistics-manegemant-system.slnx -c Release
git diff --check
git diff -- docs/Service/partner-api.openapi.json
```

- Narrow test trước theo class task, ví dụ `--filter FullyQualifiedName~BrowserShopShipmentCommandTests`; chỉ chạy filter Browser khi đã có tests, không coi zero matched tests là pass.
- Toàn solution trước checkpoint/release; LocalDB/Redis requirements theo `.agents/commands.md`. Ghi test bị skip/fail do môi trường, không coi là pass.
- Browser contract generation sửa Web csproj để sinh cả document; vẫn dùng `dotnet build` chuẩn, không tắt OpenAPI generation hoặc chạy hosted worker ở design time.

### 7.2 Frontend (sau khi đã scaffold)

Chạy từ repository root; phiên bản Node theo `.node-version` và lockfile:

```powershell
npm --prefix src/MiniLogistics.Frontend ci
npm --prefix src/MiniLogistics.Frontend run api:generate
npm --prefix src/MiniLogistics.Frontend run typecheck
npm --prefix src/MiniLogistics.Frontend run lint
npm --prefix src/MiniLogistics.Frontend test
npm --prefix src/MiniLogistics.Frontend run build
npm --prefix src/MiniLogistics.Frontend run test:e2e
git diff --check
git diff -- docs/Service/browser-api.openapi.json src/MiniLogistics.Frontend/src/shared/api/generated/browser-api.d.ts
```

- Vitest hẹp: `npm --prefix src/MiniLogistics.Frontend test -- src/features/shipments/ShipmentForm.test.tsx` khi file test đó được tạo ở T04.03.
- Playwright script cấu hình start/reuse backend test và frontend đúng mode; test publish dùng script T11.04 và URL published process, không chỉ Vite dev.
- Không chạy E2E mutation lên database sản xuất; test credential và auth storage không commit.
- `git diff` contract được dùng để review; CI regenerate rồi yêu cầu `git diff --exit-code` trên artifact đã commit.

## 8. Route parity checklist cuối cùng

Mọi route sau giữ cùng ý nghĩa ở root sau T11.05; trong coexist có prefix `/app` cho React. Đây là checklist route, không thay inventory action chi tiết ở T00.01.

| Actor/nhóm | Route | Task chịu trách nhiệm |
| --- | --- | --- |
| Public | `/`, `/faq`, `/contact` | T11.01 |
| Tracking | `/tracking` + query cũ | T11.02 |
| Auth | `/login`, `/register-shop` | T02.02–03 |
| Shop dashboard | `/dashboard` | T03.03 |
| Shipment list/detail | `/shipments`, `/shipments/{Id:guid}` | T03.02 |
| Shipment create/edit/draft | `/shipments/create` + query cũ | T04.03 |
| Shop profile | `/profile` | T08.01 |
| Shop staff | `/shop/staff` | T08.02 |
| Shop settings/report | `/shop/cod-report`, `/shop/notifications`, `/shop/audit` | T08.03 |
| Operations | `/operations/assignments`, `/operations/assignment` | T06.04 |
| Shipper | `/shipper/shipments` | T07.02 |
| Admin overview/report | `/admin`, `/admin/dashboard`, `/admin/cod`, `/admin/audit-logs` | T09.01 |
| Admin users/shops | `/admin/users`, `/admin/shops` | T09.02 |
| Admin hub/config | `/admin/hubs`, `/admin/system-config` | T09.03 |
| Admin banner | `/admin/banners` | T09.04 |
| Integrations | `/partner/integrations` | T10.01–02 |
| System UI | `/Error`, `/not-found`, unknown navigation | T01.03 + T11.05 |
| Backend routes | Partner/auth/culture/files/health/OpenAPI | T01.02 + T11.04–06 |

## 9. Definition of Done cho toàn bộ refactor

- [ ] 40 task có trạng thái/bằng chứng rõ; inventory action/route không còn chức năng bị mất hoặc chỉ mock.
- [ ] React + TypeScript strict + Ant Design là frontend duy nhất; deep link/refresh/back/query/alias và mobile đều hoạt động.
- [ ] Identity/session/CSRF/401/403, Shop permission/isolation và PII masking được kiểm tra bằng HTTP/Application/browser phù hợp.
- [ ] Shipment lifecycle, capacity, COD, fee snapshot, import worker, audit/outbox/webhook và Partner contracts giữ nguyên.
- [ ] Không còn file I/O ở UI; banner upload qua use case/Infrastructure, không mất upload khi publish.
- [ ] Error/loading/empty/concurrency/timeout không gây sai số liệu hoặc mutation retry ngầm; cache không lẫn user/Shop.
- [ ] Browser OpenAPI/types sinh lại tái lập được; Partner OpenAPI không drift ngoài thay đổi được yêu cầu.
- [ ] Frontend/backend tests, publish smoke và visual/browser QA có kết quả thực tế; giới hạn môi trường được ghi chính xác.
- [ ] CI/development/publish/runbook/agent context đã cập nhật; không có secret/build output/unrelated diff; rollback artifact đã xác định.

## 10. Tài liệu tham chiếu kỹ thuật

Các nguồn chính thức đã đối chiếu khi lập kế hoạch; pin version thực tế tại T00.02 thay vì sao chép version từ tài liệu đang dùng `latest`.

- [Ant Design với Vite](https://ant.design/docs/react/use-with-vite/) — tích hợp build React/TS.
- [Ant Design theme](https://ant.design/docs/react/customize-theme/) — cấu hình token qua ConfigProvider.
- [Ant Design localization](https://ant.design/docs/react/i18n/) — locale cho component.
- [Vite guide](https://vite.dev/guide/) — dev/build và yêu cầu môi trường.
- [React Router declarative](https://reactrouter.com/start/declarative/installation) — SPA routing.
- [TanStack Query](https://tanstack.com/query/latest/docs/framework/react/overview) — server state/cache/mutation.
- [openapi-typescript](https://openapi-ts.dev/introduction) — sinh TypeScript types từ OpenAPI 3.x.
- [Vitest](https://vitest.dev/guide/) và [React Testing Library](https://testing-library.com/docs/react-testing-library/intro/) — behavior tests.
- [Playwright](https://playwright.dev/docs/intro) — browser/E2E/publish smoke.
- [ASP.NET Core antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0) — cookie mutation CSRF model.
- [ASP.NET Core 10 API authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/api-endpoint-auth?view=aspnetcore-10.0) — API 401/403 và metadata, khác redirect page.
