# Frontend

## Trạng thái migration

Đã chọn **React + TypeScript + Ant Design**; phần dưới mô tả **Blazor đang tồn tại**, không phải yêu cầu dùng Blazor cho frontend mới. Phase 1 có scaffold/lockfile/test/CI, hosting opt-in `/app`, theme, layouts và locale; chưa thay trang nghiệp vụ. Xem [kế hoạch](../../refactorfe.md), [phạm vi và trạng thái](react-migration-contracts.md), [ADR-004](decisions/adr-004-react-typescript-ant-design.md) và [validation log](react-migration-validation.md).

Frontend mới theo feature, gọi HTTP browser API rồi đến Application use case; backend và business invariants được giữ. Hosting cùng origin/coexist đã triển khai; Identity JSON session/CSRF/API vẫn là Phase 2. Xem [inventory](react-migration-inventory.md), [toolchain pins](react-migration-toolchain.md) và [lệnh dev/test/publish](../Backend/development.md). Hướng dẫn React/Ant Design và browser security nằm tại [engineering standards](../../.agents/standards.md).

## Runtime hiện tại

Các trang nghiệp vụ nằm trong `src/MiniLogistics.Web`, dùng **Blazor Web App Interactive Server**. Component chạy trên server, nhận thao tác qua Blazor circuit và gọi Application service trực tiếp. `src/MiniLogistics.Frontend` có pipeline riêng cho React shell; khi Enabled=true, Web phục vụ `/app` và assets từ bundle, URL cũ vẫn Blazor.

React shell dùng `AppProviders` (QueryClient/I18n/ConfigProvider/AntD App), token từ theme cũ và CSS Modules cho layouts; không tải Bootstrap/Blazor CSS. AppLayout nhận navigation từ caller; hiện chỉ có Trang chính, không tạo demo role hoặc menu nghiệp vụ chưa được xác thực. PublicLayout chuẩn bị cho trang public ở phase sau. PageHeader quản lý title/focus, StatusTag giữ text cùng màu, AsyncContent phân biệt loading/error/empty và retry do người dùng. Locale vi/en đồng bộ AntD/Dayjs, chọn ngôn ngữ qua culture cookie HttpOnly hiện có; session integration vẫn ở Phase 2.

Font stack đã khai báo Be Vietnam Pro/Segoe UI/Arial; React chưa nạp font Be Vietnam Pro riêng, nên browser chưa có font này dùng fallback. Nạp font và kiểm tra typography đầy đủ là mục visual/assets còn lại từ review Phase 1.

Setup/build chung tại [Backend Development](../Backend/development.md). Quy tắc trạng thái, phí và phân quyền tại [Business rules](../Backend/business-rules.md).

Đánh giá giao diện, thay đổi dashboard và các bước visual QA được duy trì tại [Dashboard UI/UX review](dashboard-ui-review.md). Dashboard Shop có KPI drill-down theo Shop/status/date; chart dùng count thật, COD chỉ hiển thị theo permission. Bộ lọc nâng cao của danh sách đơn được mở theo nhu cầu.

## Cấu trúc và render

| Vị trí trong Web | Vai trò |
| --- | --- |
| `Components/App.razor` | HTML shell, stylesheet, font và `Routes` với `InteractiveServer` |
| `Components/Routes.razor` | `AuthorizeRouteView`, layout mặc định, focus khi điều hướng |
| `Components/Layout/` | MainLayout/NavMenu, LandingLayout, ReconnectModal |
| `Components/Pages/` | Pages theo Public, Shop, Shipments, Operations, Shipper, Administration, Integrations |
| `Components/Shared/` | Header, metric card, alert, loading, confirm dialog, icon |
| `Services/` | UiDisplay, dữ liệu hành chính, rate limiter và adapter web |
| `Resources/` | Localization resource |
| `wwwroot/` | `app.css`, ảnh, dữ liệu địa chỉ và static assets |

Không inject repository hoặc `MiniLogisticsDbContext` vào component. Lưu form state/DTO; gọi use case và reload DTO sau mutation. Application service proxy tạo scope theo operation, không giữ EF state suốt circuit.

## Màn hình theo actor

| Khu vực | Route chính | Chức năng |
| --- | --- | --- |
| Public | `/`, `/tracking`, `/login`, `/register-shop`, `/faq`, `/contact` | Landing, tracking an toàn, tài khoản Shop |
| Shop | `/dashboard`, `/shipments`, `/shipments/create`, `/shipments/{Id:guid}` | Dashboard, danh sách, tạo/draft/sửa, chi tiết, import/export/label |
| Shop settings | `/profile`, `/shop/staff`, `/shop/cod-report`, `/shop/notifications`, `/shop/audit` | Hồ sơ, staff, COD, thông báo, audit |
| Operations | `/operations/assignments` | Assign/reassign, theo dõi vận hành, hỗ trợ trạng thái và COD |
| Shipper | `/shipper/shipments` | Workspace assignment, pickup/delivery, thu COD; backend proof capability chưa được nối thành form trên page |
| Admin | `/admin`, `/admin/dashboard`, `/admin/users`, `/admin/shops`, `/admin/hubs`, `/admin/system-config`, `/admin/cod`, `/admin/audit-logs`, `/admin/banners` | Dashboard, user/config, đối soát, audit, banner |
| Integration | `/partner/integrations` | Client/key/scope, webhook, test/retry và audit |

Role gate nằm tại page; permission/membership quyết định action chi tiết. Use case vẫn là nơi bắt buộc kiểm tra quyền và Shop isolation. Integration portal có role Shop/Admin/IntegrationAdmin, nhưng quyền thực tế còn phụ thuộc scope quản lý.

## Luồng UI cần hiểu

**Tạo đơn:** form nhận địa chỉ, parcel, giá trị và COD → gọi route/fee service để preview → submit command → server tính lại và lưu → hiển thị kết quả/tải lại. Không dùng giá preview làm dữ liệu được tin cậy. Draft và sửa trước pickup gọi use case riêng của feature `Shipments`.

**Shipper:** hiển thị assignment còn hoạt động. `Delivered + PendingCollection` vẫn có trong workspace để thu COD; sau thu tiền hoặc `Returned`/`Cancelled` thì assignment kết thúc. Các action hiển thị theo trạng thái, server kiểm tra lại khi submit.

**Public tracking:** page hiện gọi `GetAsync(trackingCode)` và nhận summary. Có model xác minh trong component và Application hỗ trợ phone-last-four, nhưng input này chưa được truyền vào lời gọi hiện tại. Không mô tả verified tracking là flow UI hoàn chỉnh.

**Authentication hiện tại:** browser form đi qua `/auth/*` endpoints để thay đổi cookie; POST yêu cầu antiforgery. Giữ flow redirect/form cho các trang Blazor trong giai đoạn coexist; không giả định cookie mới tự cập nhật circuit đang mở. JSON session/CSRF endpoint cho React sẽ được bổ sung riêng theo Phase 2, không thay flow cũ trước checkpoint.

## Quy ước UI Blazor hiện tại

Các primitive, form, CSS isolation và localization API dưới đây áp dụng cho UI Blazor. Khi chuyển một feature, dùng Ant Design và giữ các yêu cầu UX/theme/accessibility tương đương theo kế hoạch.

- Dùng CSS variables/theme trong [app.css](../../src/MiniLogistics.Web/wwwroot/app.css); typography Be Vietnam Pro, màu sea-blue hiện có. CSS riêng đặt ở `.razor.css` khi cần.
- Tái sử dụng `UiPageHeader`, `UiAlert`, `UiLoadingState`, metric card và `ConfirmActionDialog` trước khi thêm primitive mới.
- Form dùng `EditForm`, validation message và label rõ; loading/disabled chặn submit lặp; danh sách có empty/error state. Dùng text cùng màu cho trạng thái.
- Route focus qua `FocusOnNavigate`; action phải dùng được bằng bàn phím. Giữ layout table/form dùng được trên mobile.
- Dùng `IStringLocalizer` và resource hiện có. Host hỗ trợ `vi-VN` mặc định và `en-US`; `/culture/set` đặt culture cookie và redirect local.
- Dữ liệu UTC đi qua `UiDisplay.FormatLocalDateTime`; hiện format theo timezone server qua `ToLocalTime()`. Không mặc định đó là timezone browser.
- Map typed error bằng helper hiện có; với `Application.ConcurrencyConflict`, hướng người dùng reload trước khi thử lại.
- Không nhúng API key đối tác làm credential cho ứng dụng browser hoặc gọi Partner API trực tiếp từ website Shop; website bên ngoài gọi backend của Shop theo [Integration guide](../Service/integration-guide.md). Portal quản trị có quyền vẫn được hiển thị key mới tạo một lần để người dùng sao chép; không lưu key vào URL, persistent storage, query cache hoặc log.

`HttpContext` có thể không còn là request hiện tại trong một circuit dài; không dựa vào nó để giữ identity/IP/state xuyên operation. Public/Shop rate limiter hiện process-local, chưa an toàn khi scale nhiều node.

## Kiểm tra khi sửa UI

Chạy Web tests và suite Application của use case bị ảnh hưởng. Kiểm tra trình duyệt theo role: navigation, quyền action, loading/error/empty, form validation, responsive và reload sau conflict. Web tests hiện có endpoint/config/helper, HtmlRenderer dashboard và HTTP drill-down; render server không thay thế kiểm tra tương tác/browser. Khi migrate, giữ các assertion có giá trị trong React/API tests trước khi bỏ Blazor tests.

Scenario và bằng chứng mới của migration được ghi trong [validation log](react-migration-validation.md). Manual reports cũ đang bị xóa trong working tree; không khôi phục chúng trong task chuẩn bị tài liệu và không dùng báo cáo mô phỏng làm bằng chứng ứng dụng đã được kiểm tra.
