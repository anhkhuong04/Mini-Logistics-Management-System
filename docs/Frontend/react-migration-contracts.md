# Frontend migration — phạm vi, contract readiness và trạng thái

Ngày đối chiếu: 25/10/2026. Tài liệu này sở hữu quyết định contract/readiness; [refactorfe.md](../../refactorfe.md) sở hữu thứ tự task và tiêu chí nghiệm thu. [Inventory](react-migration-inventory.md) sở hữu route/action, service/field bindings và adapter đích; [toolchain](react-migration-toolchain.md) sở hữu exact version pins. Phase 1–5 đã triển khai browser HTTP/auth/reference/Shop context, Shop dashboard/read/quote/mutation, CSV import và cookie downloads; nghiệp vụ Phase 6+ vẫn là thiết kế.

## Hiện trạng so với đích

| Nội dung | Hiện có trong source | Đích hoặc đề xuất |
| --- | --- | --- |
| Frontend | Blazor Interactive Server trong MiniLogistics.Web | React + TypeScript + Ant Design đã được chọn; [ADR-004](decisions/adr-004-react-typescript-ant-design.md) |
| Project layout | Bốn project .NET và React foundation/auth/context/dashboard/shipments/shipment-import `src/MiniLogistics.Frontend` | Feature còn lại theo Phase 6+ |
| UI transport | JSON auth/reference/Shop read/quote/command adapters gọi Identity/Application; Blazor cũ giữ nguyên | Browser HTTP nghiệp vụ còn lại theo phase tương ứng |
| Auth | Identity cookie, JSON `/api/v1/app/auth/*`, React login/register/session; form `/auth/*` cũ giữ nguyên | Cùng cookie trong coexist; use-case authorization vẫn là nguồn quyền |
| API/contract | Partner giữ nguyên; browser auth/context/reference/Shop read/quote/command/import có OpenAPI3.1.1 và generated TS types riêng; file routes cũ dùng cookie | Endpoint Operations/COD/admin/integration còn ở Phase 6+ |
| Hosting | T01.02: opt-in `/app`, runtime config và `/frontend-assets`; HTTPS Vite proxy, conditional publish | Root cutover còn ở Phase 11; mặc định SPA tắt, route Blazor giữ nguyên |
| Toolchain | .NET/xUnit/CI partner; React/Vite/Vitest/Playwright scaffold, npm lockfile và frontend CI | Node/npm target đã dùng dạng portable để kiểm chứng; chưa thay Node global |
| Shell | AntD theme/layout, vi/en, Router basename runtime, navigation từ Session/Shop permission; unknown route NotFound | Feature chưa migrate dùng document navigation sang Blazor; root cutover Phase 11 |

Lựa chọn React/TypeScript/Ant Design không làm thay đổi backend modular monolith. Các lựa chọn hỗ trợ dưới đây do người thực hiện chốt trong T00.02 theo phạm vi người dùng yêu cầu; không ghi chúng thành lựa chọn người dùng đã phát biểu. Chỉ cập nhật cột hiện trạng khi có source và evidence.

## Nguồn chính theo chủ đề

- Kiến trúc/nghiệp vụ/quyền backend: [Backend](../Backend/README.md), [business rules](../Backend/business-rules.md), [.agents/project.md](../../.agents/project.md).
- Quy tắc legacy Blazor và React migration: [.agents/standards.md](../../.agents/standards.md); không nhân bản toàn bộ quy tắc ở đây.
- Route hiện tại theo actor: [Frontend README](README.md#màn-hình-theo-actor). Route parity và API đích: plan mục 4 và 8.
- Workflow/coexist/documentation updates: [.agents/workflows.md](../../.agents/workflows.md#frontend-migration).
- Lệnh hiện đang hỗ trợ: [.agents/commands.md](../../.agents/commands.md); lệnh tương lai trong plan không phải bằng chứng npm đã chạy được.
- Partner contract: [API reference](../Service/partner-api.md) và [OpenAPI hiện tại](../Service/partner-api.openapi.json), tiếp tục giữ nguyên khi thêm browser contract riêng.
- Evidence thực tế: [validation log](react-migration-validation.md), không dùng checkboxes kế hoạch hoặc báo cáo mô phỏng như kết quả kiểm thử.

## Phần tài liệu Phase 0 đã chuẩn bị

- Đã liên kết kế hoạch/status từ entry point agent và developer; phân biệt trạng thái hiện tại, quyết định đích và đề xuất triển khai.
- Đã giới hạn EditForm/Bootstrap/Blazicons/circuit cho legacy Blazor, ghi chuẩn React/Ant Design và quyền/cookie-CSRF/cache/error ở đúng tài liệu agent.
- Đã thêm workflow cập nhật tài liệu theo từng task; gỡ Blazor là checkpoint cuối, không phải thời điểm đầu tiên cập nhật agent context.
- Đã ghi quyết định frontend bằng ADR mới; giữ ADR-002 và các ADR tích hợp nguyên vẹn.
- Đã thay các liên kết manual report không còn tồn tại bằng validation log mới; không khôi phục file bị người dùng xóa.

Đây là checkpoint tài liệu trước lượt chạy baseline. Kết quả T00.01/T00.02 tiếp theo được ghi ở bảng dưới và validation log; không suy ra trạng thái task chỉ từ sự tồn tại của tài liệu.

## Kết quả Phase 0

| Task | Đầu ra cần hoàn thiện | Trạng thái |
| --- | --- | --- |
| T00.01 | Inventory từng action: route/query → Application service → quyền → endpoint → React page → regression test | 28 page/30 route, 93 page/service bindings, 78 method contracts và nested record fields; xem inventory |
| T00.01 | Restore/build/test baseline trên working tree thực tế, LocalDB/Redis và failure có sẵn | Build 0 warning/error; 320 pass, 0 fail, 1 Redis test skip; xem validation log |
| T00.01 | Giá trị file-size/format/polling và behavior freeze | Đã đối chiếu source, ghi bảng dưới |
| T00.02 | Exact version matrix và peer/engine compatibility | 27 package pins; 51 engine/peer checks đạt, npm strict-peer dry-run đạt với cảnh báo Node local; không install |
| T00.02 | Request/response/error/enum/UTC/decimal/permission | Đã chốt bên dưới và trong inventory; schema generation/HTTP tests vẫn là task implementation |
| T00.02 | Route ownership/config/alias/query/returnUrl/test fixture policy | Đã chốt thiết kế bên dưới; runtime verification ở T01.02/T11.04–05 |

Baseline đã đọc cả Operations chưa commit; HEAD không đại diện đầy đủ working tree. Phase 0 hoàn tất phân tích/baseline/thiết kế, không có nghĩa React đã chạy hoặc được phép deploy production. Node local hiện `22.18.0` không thỏa bộ pins mới; T01.01 phải chuẩn bị Node/npm theo toolchain trước khi install/build.

## Contract browser đã chốt để triển khai

### Phase 3 đã triển khai

- `GET /api/v1/app/shops/{shopId}/dashboard` nhận `fromUtc/toUtc`; `countByStatus` dùng enum-name keys và integer counts, mọi amount/ratio decimal là string.
- `GET .../shipments` gọi `SearchAsync` có phân trang: status/tracking/receiver/date/COD/sort/page. Page mặc định1/size25, size1–100; reject giá trị invalid, duplicate hoặc offset tràn int. Permission cho PII/COD filter do Application kiểm tra.
- `GET .../shipments/{shipmentId}` trả snapshot/history DTO hiện có, không có assignment/proof giả. Cross-Shop actor403, shipment ngoài selected Shop404; inactive Shop vẫn xem lịch sử; masking do Application thực hiện.
- React `/app/dashboard`, `/app/shipments`, `/app/shipments/:id` dùng API trên; state theo URL, cache theo user/Shop/filter, abort stale requests. KPI failure không hiển thị0, latest failure không giả empty, staff không ViewCod không thấy COD. Drill-down giữ UTC end-of-day7 fractional digits và kiểm tra request thật trong Chromium.
- Tạo/sửa/hủy đã nối React ở Phase 4, file/import đã nối React ở Phase 5; route root cũ và Blazor regression tests được giữ. Icon dùng `@ant-design/icons` đã pin; không thêm package icon trùng.

### Transport và schema

Phase 5: `POST /api/v1/app/shops/{shopId}/shipment-imports/preview` nhận multipart với đúng1 part `file`, CSV UTF-8 tối đa2.000.000 bytes; tổng request3.000.000 bytes. `POST .../confirm` nhận `{rows: ShipmentImportRowDraft[]}` bằng DecimalString, max500, row identity hợp lệ/duy nhất; Application revalidate. `GET .../{batchId}` đọc batch có ManageShipments/Shop isolation. Limiter preview10/confirm5 mỗi phút, Retry-After giữ nguyên. Chỉ2 endpoint import tăng body limit, foundation vẫn64KiB.

Preview row thêm optional `invalidNumericFields: string[] | null` từ parser, không suy luận lỗi field bằng message. Numeric parse lỗi được để trống khi edit, cần nhập lại trước confirm; Domain rounding và worker payload giữ nguyên. Confirm vẫn200 có thể trả sync rows hoặc async BatchId/status Pending/Processing/Completed/CompletedWithErrors/Failed. React poll2s, dừng terminal/lỗi/unmount/đổi scope, có lookup Batch ID/manual retry đọc; unknown confirm không replay, thu gọn giữ guard.

File routes `/shop/files/...` giữ URL/query/MIME/Content-Disposition và legacy400/redirect errors, client requestDownload xử lý lỗi an toàn, kiểm MIME và revoke object URL. Export bỏ page nhưng giữ toàn bộ filter/sort list/UTC ticks; CSV text neutralize formula prefix và giữ escaping. Template/lỗi preview tải Blob local; batch errors tải server. ExportData, ViewCod, ViewFullPii/label và ManageShipments/batch errors do use case kiểm tra.

Phase 4: quote có authorized Application use case, normalize tỉnh/phường rồi gọi route/fee services. Browser create/draft/update/submit/cancel gọi use case cũ; create/draft dùng chung actor limiter hiện tại, trả429 + Retry-After; POST tạo resource trả201 + Location. Invalid transition/concurrency trả409. Browser ingress reject số vượt precision SQL hiện tại, weight làm tròn về0 và kích thước có volumetric weight vượt decimal(10,3); giữ rounding của Domain, không đổi schema hoặc Partner. Đây là giới hạn transport mới để tránh lỗi persistence, không phải tuyên bố mọi fee/config calculation đều nằm trong precision. React mutation form tại `/app/shipments/create` giữ query `shopId/shipmentId`; URL Blazor cũ và limiter cũ được giữ.

React form create/save draft/update/submit và detail submit/cancel gọi typed command client, không gửi actor/fee/status. Submit từ edit draft thực hiện update rồi submit như Blazor; không biến hai command thành transaction mới. Unknown outcome khóa retry và dẫn kiểm tra danh sách;409 khóa đến reload, mutation không auto retry. Snapshot bị masking không được dùng để sửa: editing UX cần ViewFullPii + ViewCod, server permission ManageShipments giữ nguyên. User/Shop/edit target mới remount form; response cũ không được điều hướng workspace mới. Quote là POST read-only có debounce/abort/retry=false và không được gửi vào command body.

- `/api/v1/app` dùng Identity cookie; `/api/v1/public` chỉ anonymous safe projections. Không dùng API key partner cho app nội bộ. Mọi endpoint cụ thể và C# binding ở [inventory](react-migration-inventory.md).
- Auth: GET `/auth/csrf` anonymous trả `{ requestToken: string }`; POST `/auth/login` anonymous nhận `{ email, password, rememberMe }`; POST `/auth/register-shop` anonymous nhận camelCase RegisterShopCommand; POST `/auth/logout` authenticated trả 204; GET `/auth/me` authenticated trả Session. Các path này có prefix `/api/v1/app`, không thay `/auth/*` form cũ.
- Session là `{ userId, displayName, roles: string[], operationPermissions: string[], locale: 'vi-VN' | 'en-US', serverTimeZoneId }`. ShopContext giữ field của GetShopContextResponse nhưng ShopPermission là tên quyền đơn lẻ trong array, không serialize cờ thành số/chuỗi gộp. Không phát hành `All` như quyền độc lập.
- Actor lấy ClaimTypes.NameIdentifier và active user; request body không có actor/role/permission tự khai. Route ID là target resource, không phải người thực hiện. `shipper/me` không nhận ID shipper khác. IAutoAssignShipmentService có optional `requestedByUserId`: endpoint thao tác thủ công phải truyền actor, giữ nhánh system/internal cho workflow server hiện có.
- Staff update path dùng `{staffUserId}` = ShopStaffResponse.UserId để bind command.StaffUserId; MembershipId là ID khác chỉ để hiển thị/tham chiếu membership, không thay thế target user trong use case. Reject body target ID khác route hoặc không phát hành field target trong body.
- Quote adapter là contract mới không serialize domain value objects: request `{ pickupAddress, deliveryAddress, weightKg, lengthCm, widthCm, heightCm, goodsValueAmount, currency }`, address shape theo ShipmentAddressDto; response `{ routeType, actualWeightKg, volumetricWeightKg, chargeableWeightKg, baseFeeAmount, extraWeightFeeAmount, insuranceFeeAmount, returnFeeAmount, shippingFeeAmount, currency }`. Map số tiền từ ShippingFeeQuote.Breakdown/Money.Amount, weight từ quote; mọi decimal dùng DecimalString. Authorize Shop/ManageShipments trước classify/calculate, không nhận route/fee client làm nguồn tin cậy.
- Success unwrap Result, DTO camelCase; read/update trả 200 DTO, create resource trả 201 + Location resource API, Result/void command trả 204. Ngoại lệ có kết quả từng dòng/batch/queue (bulk retry/import confirm/test webhook) trả 200 DTO; async import dùng batchId/status để poll, không tự thay thành job API mới. Create/rotate API key trả secret DTO một lần, không cache.
- Paged reads giữ `items/pageNumber/pageSize/totalCount/totalPages`; không biến list báo cáo có limit thành trang giả. Dashboard/history/detail chỉ trả field DTO hiện có, không thêm assignment/proof giả.
- Actor/path removal và field defaults/nullability: áp dụng schema table trong inventory. Domain entities, Identity internal records, hashed keys/protected blobs và tracked EF objects không phải browser payload.

### Decimal, ngày giờ và enum

**Ruling:** mọi property C# `decimal`/`decimal?` trong browser request/response (money, weight, dimensions, GPS, metric ratio) dùng **DecimalString** hoặc null. Money lưu decimal(18,2) có thể vượt số nguyên an toàn của JS; validator hiện chưa áp upper bound đảm bảo JS-safe. Không sử dụng number tạm rồi serialize thành string sau khi đã mất chính xác. Partner/file contracts giữ nguyên.

- DecimalString: invariant base-10 có dấu âm tùy validator, dấu chấm thập phân, không exponent/group separator/locale comma/NaN/Infinity. Parse bằng .NET decimal invariant ở adapter, reject overflow/invalid format 400; giữ giá trị chính xác, không round client. Các ràng buộc dương/không âm tiếp tục do validator hiện có quyết định.
- OpenAPI browser decimal là string (ví dụ pattern `^-?[0-9]+(?:\.[0-9]+)?$`), nullability theo DTO; TS dùng branded/string alias; AntD InputNumber dùng stringMode cho field decimal. Aggregate/tổng/fee tính trên server; UI hiển thị giá trị server. Test `9007199254740993.01`, `0.10`, null, số âm bị validator cấm, exponent/comma/overflow và round-trip trước khi mở mutation.
- Precision SQL hiện tại là thông tin persistence, không tự thêm upper bound nghiệp vụ mới trong API: money/fee decimal(18,2), actual/chargeable weight decimal(10,3), parcel dimensions decimal(10,2), extra weight fee step decimal(10,4). App validator chỉ yêu cầu weight/dimensions >0, money >=0. Khoảng trống input vượt SQL precision phải được ghi nhận/test ở T04.02; không tuyên bố mọi input decimal hợp lệ đều lưu được, không sửa schema trong Phase 0.
- UTC query dùng ISO 8601 có offset/Z và normalize UTC ở biên; không chuyển yyyy-MM-dd sang browser local một cách ngầm định. Giữ semantics khoảng ngày/drill-down hiện tại của DashboardPresentation và test cuối ngày UTC.
- `serverTimeZoneId` dùng IANA tương ứng TimeZoneInfo.Local; Intl.DateTimeFormat theo timezone này ở từng timestamp để giữ DST lịch sử, không dùng offset hiện tại cho mọi ngày. Không đổi sang browser timezone. Date-only businessDate là `yyyy-MM-dd`, không convert thành instant.
- Enum browser là tên string, reject số/undefined; request từ URL cũ được UI normalize theo legacy trước khi gọi API. ShipmentStatus: Draft/PendingPickup/Assigned/PickingUp/PickedUp/InTransit/Delivering/Delivered/DeliveryFailed/Returned/Cancelled. CodStatus: NotRequired/PendingCollection/Collected/Settled. RouteType: IntraProvince/IntraRegion/InterRegion.
- ShopStaffRole: Manager/Operator/Analyst/Viewer/Custom. Shipper stage: Pickup/Transit/Delivery/CodPending. FailureReasonCode giữ tên trong Domain; không tạo string reason ngoài enum. Integration scopes chuyển array Quote/CreateShipment/TrackShipment/CancelShipment/WebhookManage sang flags đã validate, không tự dùng role thay scope.

### Validation và status/error

- ProblemDetails browser dùng `application/problem+json`, `type/title/status/detail` safe và extensions `code/traceId/errors?`; `errors` map field path camelCase, nested path như `deliveryAddress.province`, row path như `rows[0].receiverPhone`. Frontend map sang Form.Item name path; không phân tích chuỗi error description để đoán field.
- Khi Application trả validation description tổng hợp, hiển thị lỗi cấp form; adapter có thể dùng lại validator cho command đã bind actor/path để trả structured field errors, không sao chép rules hoặc bỏ kiểm tra Application. Non-validation use-case error không gán giả vào field.
- 400 malformed JSON/decimal/enum/field validation/CSRF; 401 chưa đăng nhập hoặc sai credential; 403 thiếu quyền/inactive; 404 không tìm thấy trong scope; 409 business conflict/concurrency/capacity; 423 account lockout; 429 limiter kèm Retry-After giây; 503 dependency unavailable; 500 safe fallback không có exception detail. Giữ error code nghiệp vụ; CSRF dùng `Browser.CsrfValidationFailed`, lockout `Auth.LockedOut`, login sai `Auth.InvalidCredentials`, actor parse lỗi trả 401.
- Các endpoint browser phải trả 401/403 JSON cho fetch, không redirect thành HTML login. Cấu hình dựa metadata/challenge handler phù hợp .NET10, có regression test thực; riêng `/auth/*` legacy vẫn redirect. Browser/public error handler không được bị handler partner hiện bắt `/api` nuốt mất.
- Validation nguồn: CreateShipment/DraftShipment rules yêu cầu sender/receiver <=100, phone 9–15 digits (optional + và khoảng trắng chuẩn hóa), street <=200, ward/province/country <=100, note <=500, currency đúng 3 ký tự. RegisterShop khác: fullName <=150, ShopName <=200, addressLine <=300, email <=256. Không dùng giới hạn form tạo đơn cho form đăng ký.
- Password theo PasswordPolicy hiện tại: 8–100 ký tự, uppercase/lowercase/digit/special; Identity yêu cầu 4 unique chars, lockout 5 lần/15 phút. CreateInternalUser chỉ Operator/Shipper theo validator hiện tại, không biến dropdown thành cơ chế cấp Admin mới.
- Pending/Operations paging default25, shipper20, user25; server clamp như hiện tại nhưng browser adapter reject pageNumber<1, pageSize ngoài1–100, không cho truyền int.MaxValue để mở full scan. Legacy nội bộ giữ behavior cũ; URL không hợp lệ được normalize/hiển thị lỗi trước request. Notification limit1–200, audit Shop1–500 theo use case.

### Session, CSRF, cache và limiter

Phase 2 đã triển khai các auth/reference/context contracts ở trên. Browser group giới hạn body64KiB cho JSON foundation hiện tại; unsupported media trả415 JSON và body quá lớn413 JSON, malformed JSON vẫn400. Các upload endpoint Phase 5/9 phải khai báo lại limit theo feature. Browser document mô tả Identity cookie/CSRF; schema/types được sinh từ metadata, không chỉnh bằng tay.

- Same-origin .NET Identity HttpOnly cookie; không thêm JWT/localStorage token. GET CSRF tạo request token cùng antiforgery cookie, frontend gửi header `X-CSRF-TOKEN` cho POST/PUT/PATCH/DELETE bao gồm auth anonymous và multipart. Server validate rõ ràng cho JSON; không coi MapPost JSON tự có antiforgery.
- Login/logout/register đổi identity phải invalidate/refresh CSRF token; logout và 401 hủy request, clear Query cache và one-time secrets. 403 reload context/permission cho UX, không retry mutation. Client retry GET chỉ hữu hạn cho transport/5xx, không retry 4xx/429 tức thời; mutation retry=false.
- Session/context/PII/secret/auth responses no-store; không persist query cache. Query key gồm userId, ShopId nếu có, canonical filter/sort/page; không keepPreviousData xuyên user/Shop. Abort/ignore response A về sau khi đã chọn B.
- Shop permission: ViewShipments cho read/profile/notifications; ManageShipments cho create/edit/import/quote; ViewCod cho report; ExportData cho CSV; label cần ViewShipments + ViewFullPii; COD CSV cần ExportData + ViewCod. Staff CRUD owner-only. Field PII/COD vẫn masking ở use case.
- Operations đọc ShipmentView, CodViewPending cho candidates; assign/retry/bulk/reassign/cancel/status/collect/settle theo OperationPermissions, Operator không settle. Thêm actor-aware authorized entry cho read không có actor trước HTTP exposure. Shipper chỉ self active assignment; IntegrationAdmin theo global/Shop/province management scope.
- Giữ threshold cấu hình hiện tại, chuyển vị trí kiểm tra sang ingress khi có API; không thay limiter store trong migration. Error mới 429 có Retry-After; IP tracking lấy trusted forwarded chain.

## Giá trị baseline cần giữ

| Hạng mục | Giá trị hiện tại và source |
| --- | --- |
| Banner upload | 5 * 1024 * 1024 = 5,242,880 byte; .png/.jpg/.jpeg/.webp/.svg/.gif; AdminBanners.razor. Guard authorization/storage ở T09.04, không mặc định coi SVG an toàn chỉ do extension |
| CSV import | UI stream <=2,000,000 byte; validator CsvContent <=2,000,000 ký tự; row max500; confirm revalidate; UTF-8 và invalid text errors theo parser hiện có |
| Batch polling | 2 giây; dừng terminal/unmount/logout/đổi Shop, không thêm background poll vô hạn |
| Bulk retry | Max50 IDs duy nhất; RequestedByUserId lấy principal; giữ partial result từng shipment |
| Export | Shipment CSV tối đa10,000 row, page read100; label binary PDF, không parse JSON |
| Shop limits/phút | Create30, preview10, confirm5, export shipment10, COD export10, label30; ShopUiActionRateLimitOptions + appsettings |
| Public tracking | 20/phút theo cấu hình hiện tại; summary-safe; phoneLast4 không được nối trong UI hiện có |
| State/money/fee | State machine, fee snapshots và server recompute không đổi; Delivered + PendingCollection vẫn giữ assignment active đến collect |

## Route ownership và triển khai

- Defaults thiết kế: BrowserSpa:Enabled=false, BrowserSpa:ServeAtRoot=false. Khi enabled và chưa root, React chiếm `/app` và `/app/*` navigation; Blazor giữ URL cũ. `/frontend-assets/*` chỉ asset fingerprinted, thiếu file trả404.
- Root mode: chỉ map một UI route owner, không để fallback React và MapRazorComponents tranh cùng URL. APIs/auth/culture/files/health/openapi/images/data không được index fallback. Unknown browser navigation về React NotFound; API unknown và asset unknown giữ404 đúng content type.
- Runtime config công khai chỉ `{basePath, assetBasePath}`, ví dụ `/app` và `/frontend-assets`; Vite base cho assets cố định, React Router basename theo runtime. Một routePaths.ts sinh link bằng basePath, không hard-code `/app` ở feature.
- Route/query/alias mapping đầy đủ trong inventory. Giữ `shopId`, `shipmentId`, `code`, `fromUtc`, `toUtc`, `status`, `error` đúng trang hiện có; canonical `/admin` alias `/admin/dashboard`, `/operations/assignments` alias `/operations/assignment`. Edit vẫn `/shipments/create?shipmentId={id}&shopId={shopId}`.
- New list URL state thêm `trackingCode`, `receiverName`, `receiverPhone`, `minCodAmount`, `maxCodAmount`, `sortBy`, `sortDirection`, `pageNumber`, `pageSize`; API adapter bind rõ sang *Search/*Filter C# names, không phó mặc tên DTO trùng query.
- Deep link/reload/back cần published-host test; khi cutover `/app/...` redirect sang root giữ query đã validate. returnUrl chỉ path local trong app, cấm `//`, scheme, backslash và URL external; không redirect đến mutation endpoint.
- Dev: Vite HTTPS proxy đến .NET HTTPS profile hiện tại, `/api`, `/auth`, `/culture`, `/shop/files` đi qua proxy; không mở wildcard credential CORS. Production chung origin, không thêm Node server. Index/runtime config revalidate, hashed assets immutable; upload/data không bị frontend copy ghi đè.

## Test fixtures và checkpoint

- Backend baseline dùng fixture LocalDB tạo database tên ngẫu nhiên, migrate/seed test và cleanup theo test; không chạy app `--migrate/--seed` trên database người dùng. Redis integration chỉ chạy khi biến MINILOGISTICS_TEST_REDIS được đặt; không ghi giá trị connection vào evidence.
- E2E tương lai dùng fixture environment Development/Test riêng: active Admin/Operator, ownerA với ShopA/ShopA2, ownerB với ShopB, staff ViewShipments-only, staff ManageShipments, staff ViewCod/ExportData thiếu FullPii, inactive Shop/user, assigned shipperA/shipperB và IntegrationAdmin có phạm vi hạn chế. IDs sinh per run, credential môi trường/ephemeral; không ghi giá trị thật vào plan hoặc commit storage state.
- Fixtures và factories phải tạo quyền ở backend, không fake menu/claim để chứng minh tenant isolation. Scope/expired session/cross-Shop denial cần HTTP tests; SQL race/atomicity cần LocalDB tests hiện có; UI mocks chỉ chứng minh presentation.
- Baseline có 1 Redis skip được ghi rõ, không phải failing regression hoặc bằng chứng multi-node Redis đã pass. Phase 1 đã có evidence install/ci/typecheck/build/browser trong validation log; không suy ra auth/data workflows từ shell test.

## Các giới hạn phạm vi cần giữ

- Summary public tracking hiện chưa nối phone-last-four; verified flow là thay đổi riêng, không tự bật trong migration.
- Giữ ngữ nghĩa timezone server và UTC query khi chuyển renderer; không tự đổi sang timezone browser.
- Move limiter sang ingress không đồng nghĩa đã sửa atomicity nhiều instance; vấn đề store giữ ở [.agents/known-issues.md](../../.agents/known-issues.md).
- Banner use-case authorization/storage phải được xử lý trong T09.04 trước khi mở browser admin endpoint.
- Không đổi Partner API, migrations, transaction/outbox, audit hoặc worker lifecycle theo sở thích frontend.
- Khi thêm browser API, cần hoàn thiện browser session/CSRF threat model và tests trong task tương ứng; threat model partner hiện tại không tự bao phủ đường gọi mới.

## Quy tắc cập nhật sau mỗi checkpoint

Task thay behavior phải cập nhật tài liệu sở hữu tương ứng và evidence. Chỉ thêm supported commands sau khi script có thật và chạy được; chỉ link generated browser schema/types khi artifact đã sinh bằng tooling. Đến cutover/cleanup, đánh dấu legacy hướng dẫn đã được thay thế thay vì sửa lịch sử ADR hoặc xóa bằng chứng test có giá trị.
