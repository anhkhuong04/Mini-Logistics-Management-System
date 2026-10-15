# Frontend

UI nằm trong `src/MiniLogistics.Web`, dùng **Blazor Web App Interactive Server**. Không có SPA project hoặc frontend build pipeline riêng. Component chạy trên server, nhận thao tác qua Blazor circuit và gọi Application service trực tiếp.

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
| Shipper | `/shipper/shipments` | Workspace assignment, pickup/delivery/proof, thu COD |
| Admin | `/admin`, `/admin/dashboard`, `/admin/users`, `/admin/shops`, `/admin/hubs`, `/admin/system-config`, `/admin/cod`, `/admin/audit-logs`, `/admin/banners` | Dashboard, user/config, đối soát, audit, banner |
| Integration | `/partner/integrations` | Client/key/scope, webhook, test/retry và audit |

Role gate nằm tại page; permission/membership quyết định action chi tiết. Use case vẫn là nơi bắt buộc kiểm tra quyền và Shop isolation. Integration portal có role Shop/Admin/IntegrationAdmin, nhưng quyền thực tế còn phụ thuộc scope quản lý.

## Luồng UI cần hiểu

**Tạo đơn:** form nhận địa chỉ, parcel, giá trị và COD → gọi route/fee service để preview → submit command → server tính lại và lưu → hiển thị kết quả/tải lại. Không dùng giá preview làm dữ liệu được tin cậy. Draft và sửa trước pickup gọi use case riêng của feature `Shipments`.

**Shipper:** hiển thị assignment còn hoạt động. `Delivered + PendingCollection` vẫn có trong workspace để thu COD; sau thu tiền hoặc `Returned`/`Cancelled` thì assignment kết thúc. Các action hiển thị theo trạng thái, server kiểm tra lại khi submit.

**Public tracking:** page hiện gọi `GetAsync(trackingCode)` và nhận summary. Có model xác minh trong component và Application hỗ trợ phone-last-four, nhưng input này chưa được truyền vào lời gọi hiện tại. Không mô tả verified tracking là flow UI hoàn chỉnh.

**Authentication:** browser form đi qua `/auth/*` endpoints để thay đổi cookie; POST yêu cầu antiforgery. Dùng flow redirect/form hiện có khi thay đổi login/logout/register; không giả định cookie mới tự cập nhật circuit đang mở.

## Quy ước UI

- Dùng CSS variables/theme trong [app.css](../../src/MiniLogistics.Web/wwwroot/app.css); typography Be Vietnam Pro, màu sea-blue hiện có. CSS riêng đặt ở `.razor.css` khi cần.
- Tái sử dụng `UiPageHeader`, `UiAlert`, `UiLoadingState`, metric card và `ConfirmActionDialog` trước khi thêm primitive mới.
- Form dùng `EditForm`, validation message và label rõ; loading/disabled chặn submit lặp; danh sách có empty/error state. Dùng text cùng màu cho trạng thái.
- Route focus qua `FocusOnNavigate`; action phải dùng được bằng bàn phím. Giữ layout table/form dùng được trên mobile.
- Dùng `IStringLocalizer` và resource hiện có. Host hỗ trợ `vi-VN` mặc định và `en-US`; `/culture/set` đặt culture cookie và redirect local.
- Dữ liệu UTC đi qua `UiDisplay.FormatLocalDateTime`; hiện format theo timezone server qua `ToLocalTime()`. Không mặc định đó là timezone browser.
- Map typed error bằng helper hiện có; với `Application.ConcurrencyConflict`, hướng người dùng reload trước khi thử lại.
- Không đưa API key vào JavaScript/HTML. Website Shop bên ngoài gọi backend của Shop theo [Integration guide](../Service/integration-guide.md).

`HttpContext` có thể không còn là request hiện tại trong một circuit dài; không dựa vào nó để giữ identity/IP/state xuyên operation. Public/Shop rate limiter hiện process-local, chưa an toàn khi scale nhiều node.

## Kiểm tra khi sửa UI

Chạy Web tests và suite Application của use case bị ảnh hưởng. Kiểm tra trình duyệt theo role: navigation, quyền action, loading/error/empty, form validation, responsive và reload sau conflict. Web tests hiện kiểm tra endpoint/config/helper, không thay thế kiểm tra render/browser.

Scenarios có tại [Manual test cases](../../test/manual/manual-test-cases.md). Các báo cáo mô phỏng trong `test/manual` chỉ là tài liệu test mẫu, không chứng minh ứng dụng hiện tại đã được kiểm tra thực tế.
