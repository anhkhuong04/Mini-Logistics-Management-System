# Frontend

## Trạng thái migration

Phase 6 đã nối Operations React tại `/app/operations/assignments` và alias `/app/operations/assignment`: chờ phân công, theo dõi trạng thái/thu COD, đối soát Admin; URL Blazor cũ vẫn giữ. Bộ lọc/page ở URL, ngày theo múi giờ server, query có actor ở Application, capacity lấy từ tổng hợp server. Dialog dùng icon thư viện, cảnh báo khác vùng/tạm ngừng nhận đơn và guard tải; bulk giữ từng kết quả. Lỗi409/unknown không tự gửi lại, refresh chỉ mở khóa generation đã đọc lại. Đọc/ghi settlement giữ CodSettle như UI hiện tại; audit identity/GPS có AuditView. Đã kiểm chứng E2E/publish/baseline theo validation log, chưa chuyển root hoặc triển khai production.

Đã chọn **React + TypeScript + Ant Design**. Phase 1–7 có hosting opt-in `/app`, theme/layout/locale, JSON auth/CSRF/session, form React, Shop context theo quyền, dashboard và danh sách/chi tiết vận đơn. Phần mô tả Blazor bên dưới tiếp tục áp dụng cho URL cũ trong coexist. Xem [kế hoạch](../../refactorfe.md), [phạm vi và trạng thái](react-migration-contracts.md), [ADR-004](decisions/adr-004-react-typescript-ant-design.md) và [validation log](react-migration-validation.md).

Frontend mới theo feature, gọi HTTP browser API rồi đến Application use case; backend và business invariants được giữ. Auth/context/reference, Shop read/quote/mutation, CSV import, cookie download, Operations/COD và Shipper đã triển khai; settings/admin/integration còn ở Phase 8+. Icon dùng named imports từ `@ant-design/icons`6.3.4 đã pin, không hardcode SVG/emoji. Xem [inventory](react-migration-inventory.md), [toolchain pins](react-migration-toolchain.md) và [lệnh dev/test/publish](../Backend/development.md). Hướng dẫn browser security nằm tại [engineering standards](../../.agents/standards.md).

Shipper React `/app/shipper/shipments` dùng self API `/api/v1/app/shipper/me`: mobile cards, stage tabs/filter/page URL, profile/load/vùng làm việc, COD ngày UTC, đổi availability, status/note/failure reason và xác nhận thu COD. Không có proof/geolocation form. Actor/assignment được kiểm tại Application, profile không tải toàn bộ shippers. Unknown/409 khóa theo shipment đến refresh thành công; refresh cũ không mở khóa lỗi mới. Blazor cũ giữ nguyên trong coexist.

## Runtime hiện tại

Các trang nghiệp vụ nằm trong `src/MiniLogistics.Web`, dùng **Blazor Web App Interactive Server**. Component chạy trên server, nhận thao tác qua Blazor circuit và gọi Application service trực tiếp. `src/MiniLogistics.Frontend` có pipeline riêng cho React shell; khi Enabled=true, Web phục vụ `/app` và assets từ bundle, URL cũ vẫn Blazor.

React dùng AppProviders + SessionProvider, QueryClient/I18n/ConfigProvider/AntD App; không tải Bootstrap/Blazor CSS. SessionProvider đọc cookie thật, refresh CSRF khi đổi identity, clear cache khi logout/401/đổi user; ShopContextProvider abort/ignore request cũ và key theo user/Shop. AppLayout nhận navigation từ Session và Shop permission server trả; frontend guard chỉ là UX, Application tiếp tục kiểm tra quyền. PublicLayout dành cho trang public ở phase sau. PageHeader quản lý title/focus, AsyncContent phân biệt tải/lỗi/rỗng; locale vi/en đồng bộ culture cookie, AntD/Dayjs và auth copy.

Mở `/app/login` hoặc `/app/register-shop` để dùng form React. Redirect ưu tiên Admin/Operator→Operations, Shipper→workspace, Shop→`/app/dashboard`, IntegrationAdmin-only→integrations; returnUrl chỉ whitelist route local. `/app/shipments`, `/app/shipments/:id` và `/app/shipments/create?shopId=...&shipmentId=...` dùng React. Form hỗ trợ tạo/lưu nháp/sửa/gửi; detail có gửi/hủy với xác nhận và nhãn PDF; list có nhập/xuất CSV. Feature chưa chuyển mở Blazor. Shop selector dùng URL `shopId`; không persist session/cache/password vào storage.

Form lấy tỉnh/phường từ shared reference query, reset ward khi province đổi; quote350ms debounce có abort, chỉ là preview. Command không gửi fee/actor/role; phí authoritative do server tính lại. Sửa cần snapshot không bị che (ViewFullPii + ViewCod), tránh ghi đè PII/COD bằng masked values; ManageShipments-only vẫn tạo/gửi/hủy theo use case. Đổi Shop/user/edit target remount form; mutation không auto retry. Khi chưa rõ kết quả phải kiểm tra danh sách, khi409 phải tải lại trước thử lại.

Panel nhập CSV nằm tại danh sách đơn, có mẫu header, chọn file rồi kiểm tra (không auto-upload), sửa từng dòng và confirm. File UTF-8 tối đa2.000.000 bytes/500 dòng; ô numeric parse lỗi để trống đến khi nhập lại, không lấy0 làm giá trị thật. Server revalidate, worker tạo đơn; polling2 giây dừng khi terminal/lỗi/unmount/đổi user hoặc Shop, có tra cứu Batch ID đã biết và retry đọc thủ công. Thu gọn/mở lại không xóa pending/unknown guard. Danh sách có xuất CSV theo filter hiện tại; dashboard có COD CSV khi đủ quyền; detail non-Draft có nhãn PDF theo ViewFullPii; batch có CSV lỗi theo ManageShipments. Tải qua cookie, kiểm tra MIME, giữ filename và hủy object URL.

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

**Authentication hiện tại:** browser form Blazor đi qua `/auth/*`; React dùng JSON `/api/v1/app/auth/*`. POST đều yêu cầu antiforgery. Giữ flow redirect/form cho các trang Blazor trong coexist; không giả định cookie mới tự cập nhật circuit đang mở.

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
