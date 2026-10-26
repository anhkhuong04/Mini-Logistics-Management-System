# Phát triển local

Các lệnh .NET dưới đây dành cho runtime backend/Blazor hiện tại. Scaffold React/TypeScript/Ant Design nằm ở `src/MiniLogistics.Frontend`, dùng Node 24.22.1/npm 11.19.1 theo [toolchain](../Frontend/react-migration-toolchain.md). Xem [migration status](../Frontend/react-migration-contracts.md) để biết phạm vi hosting/API đã có.

Frontend: chạy `npm --prefix src/MiniLogistics.Frontend ci`, sau đó `run dev`; validation dùng `run typecheck`, `run lint`, `test`, `run build`. `run test:e2e` dùng Playwright Chromium đã cài. Build Web sinh cả Partner OpenAPI và `docs/Service/browser-api.openapi.json`; chạy `npm --prefix src/MiniLogistics.Frontend run api:generate` để cập nhật types browser, kiểm tra diff trước commit.

T01.02 đã có hosting coexist, mặc định tắt. Xuất dev certificate bằng `npm --prefix src/MiniLogistics.Frontend run dev:cert` (file chỉ ở `.agent/https` bị ignore); trust certificate trên máy bằng `dotnet dev-certs https --trust` nếu cần. Bật `$env:BrowserSpa__Enabled = 'true'`, chạy Web với HTTPS profile bên dưới và Vite ở terminal khác, truy cập `https://localhost:5173/app/`. Vite xác thực TLS khi proxy đến backend7195; override bằng `MINILOGISTICS_BACKEND_URL`, `MINILOGISTICS_HTTPS_CERT`/`MINILOGISTICS_HTTPS_KEY` nếu cần. Các URL Blazor cũ vẫn được proxy tới Web.

Sau `run build`, publish cùng bundle bằng `dotnet publish src/MiniLogistics.Web/MiniLogistics.Web.csproj -c Release -p:PublishBrowserSpa=true -o <output>`; dist được copy tới `<output>/frontend`, thiếu index.html làm publish fail. Bật `BrowserSpa__Enabled=true` tại runtime; `BrowserSpa__DistPath` chỉ cần override khi bundle ở nơi khác. Không bật `ServeAtRoot` trước Phase 11. Build/test/publish mặc định không cần Node hoặc dist.

Playwright yêu cầu `dev:cert`, Web Release build và Chromium đã cài; tự chạy HTTPS backend/Vite trong environment `BrowserTests`, bỏ Infrastructure hosted workers để kiểm tra shell/routing mà không truy cập database. Đây không phải môi trường xác thực nghiệp vụ/session. Để kiểm tra published process đã chạy riêng, đặt `MINILOGISTICS_E2E_BASE_URL=https://localhost:<port>/app/`; Playwright khi đó không tự mở server. Chỉ browser test bỏ kiểm tra certificate local; proxy vẫn kiểm tra TLS.

Phase 2 có fixture browser riêng cho Identity cookie thật, register/login/logout/Shop context: build frontend trước, đặt `$env:MINILOGISTICS_BROWSER_E2E='1'`, chạy `dotnet test test/MiniLogistics.Web.Tests/MiniLogistics.Web.Tests.csproj -c Release --filter FullyQualifiedName~BrowserRealBrowserTests`, rồi xóa biến opt-in. `MINILOGISTICS_NODE` là optional absolute path tới Node đúng pins, `PLAYWRIGHT_BROWSERS_PATH` cho browser install path riêng. Fixture tự tạo HTTPS Kestrel/Identity/Application với EF InMemory tách biệt, không truy cập database người dùng; không chứng minh SQL transaction. Trace credential/cookie bị tắt. Lệnh shell E2E thường skip3 scenario cần fixture; suite .NET mặc định skip1 fixture test. Xem [supported commands](../../.agents/commands.md) cho đầy đủ bước.

React auth ở `/app/login`, `/app/register-shop`; JSON endpoints `/api/v1/app/auth/{csrf,login,register-shop,logout,me}`, Shop context `/api/v1/app/shop-context?shopId=...`, reference `/api/v1/public/reference-data/provinces`. Mutation dùng header X-CSRF-TOKEN từ csrf GET, refresh sau identity change. Cookies/session/PII responses no-store. Feature chưa chuyển renderer mở URL Blazor cũ; không bật root cutover.

Chạy lệnh từ repository root. Dùng `Mini-logistics-manegemant-system.slnx`, không dùng file `.csproj` lạc ở root.

## Điều kiện và cấu hình

- .NET 10 SDK; SQL Server LocalDB cho cấu hình development mặc định và Infrastructure tests trên Windows.
- `dotnet-ef` khi thao tác migration. Redis khi kiểm tra Redis mode/production-like.
- `appsettings.Development.json` của Web chứa mặc định local; override bằng environment/provider, không commit connection/credential cá nhân.
- Seed là opt-in; chạy app bình thường không tự migrate/seed.

## Chạy và kiểm tra

```powershell
dotnet restore Mini-logistics-manegemant-system.slnx
dotnet build Mini-logistics-manegemant-system.slnx -c Release
dotnet test Mini-logistics-manegemant-system.slnx -c Release

dotnet run --project src/MiniLogistics.Web -- --migrate
dotnet run --project src/MiniLogistics.Web --launch-profile https
```

HTTPS profile dùng `https://localhost:7195`. `/health/live` kiểm tra liveness; `/health/ready` kiểm tra dependency. Ngoài Production, OpenAPI phục vụ tại `/openapi/v1.json`.

Build chuẩn sinh `docs/Service/partner-api.openapi.json`. Design-time host không đăng ký runtime hosted workers (`registerHostedServices: false`); không còn cần workaround `OpenApiGenerateDocuments=false` nêu trong tài liệu cũ.

Chạy suite hẹp trước khi kiểm tra toàn solution:

```powershell
dotnet test test/MiniLogistics.Domain.Tests/MiniLogistics.Domain.Tests.csproj -c Release
dotnet test test/MiniLogistics.Application.Tests/MiniLogistics.Application.Tests.csproj -c Release
dotnet test test/MiniLogistics.Infrastructure.Tests/MiniLogistics.Infrastructure.Tests.csproj -c Release
dotnet test test/MiniLogistics.Web.Tests/MiniLogistics.Web.Tests.csproj -c Release
```

Dùng `--filter FullyQualifiedName~...` để chọn test; `--no-build` chỉ sau build cùng configuration thành công. Infrastructure tests cần LocalDB, dù app local có thể dùng SQL Server khác. Không thay bằng InMemory để kết luận transaction/concurrency đúng.

## Seed demo

Đặt `Seeding__Enabled=true` và các secret local: `Seeding__DemoPartnerApiKey`, `Seeding__DemoAdminPassword`, `Seeding__DemoShopPassword`, `Seeding__DemoShipperPassword`, `Seeding__DemoOperatorPassword`.

```powershell
dotnet run --project src/MiniLogistics.Web -- --migrate --seed
```

Flag migrate/seed chạy tác vụ database rồi thoát; chạy lại app để mở UI. Không bật demo seed trong Production.

## Migration

```powershell
dotnet ef migrations add <MigrationName> `
  --project src/MiniLogistics.Infrastructure `
  --startup-project src/MiniLogistics.Web `
  --output-dir Persistence/Migrations

dotnet ef migrations has-pending-model-changes `
  --project src/MiniLogistics.Infrastructure `
  --startup-project src/MiniLogistics.Web

dotnet ef database update `
  --project src/MiniLogistics.Infrastructure `
  --startup-project src/MiniLogistics.Web
```

Review cả migration, designer và snapshot: default/backfill, destructive change, constraints/index và delete behavior. Chỉ apply vào database đã xác định trong phạm vi công việc.

## Thay đổi hợp đồng tích hợp

Cập nhật endpoint/DTO, generated OpenAPI, [API reference](../Service/partner-api.md), [changelog](../Service/partner-api-changelog.md), Postman và example khi hợp đồng đổi. CI kiểm tra artifact drift và breaking change; không hand-edit OpenAPI.

## Administration và ảnh banner (Phase 9)

React quản trị tại `/app/admin` dùng cookie/CSRF và API `/api/v1/app/admin`; active Admin được kiểm trong Application. Giữ các URL Blazor trong coexist. Cấu hình route/fee vẫn tạo version và invalidation cache trên server; khi bắt đầu cấu hình vùng tuyến, phải chuẩn bị đủ danh sách tỉnh phục vụ vì bản đồ cấu hình thay thế toàn bộ fallback mặc định.

Đặt `BannerImages__RootPath` thành đường dẫn tuyệt đối ngoài thư mục release, trỏ đến storage bền vững có quyền đọc/ghi và backup. Mặc định dùng LocalApplicationData của account chạy host, không dùng release wwwroot. Trước khi bỏ release cũ, copy các ảnh runtime cũ từ `wwwroot/images/banners` vào storage giữ nguyên tên; `/images/banners/...` vẫn giữ URL. Không đưa storage vào bước xóa/copy frontend khi publish. Upload giới hạn5MiB, sáu extension cũ; SVG chỉ nhận subset an toàn, không script/style/event/external resource. Xóa metadata không xóa file ảnh.

Kiểm tra một URL ảnh đã upload trước publish, publish SPA theo lệnh ở trên, xác nhận byte ảnh không đổi sau publish và restart host. HTTP/use-case test: `BrowserAdminTests`, `BrowserBannerTests`; storage: `BannerImageStorageTests`. Xem [contract](../Frontend/react-migration-contracts.md#phase-9--administration-và-banner-io) và [evidence](../Frontend/react-migration-validation.md).

## Chẩn đoán thường gặp

| Hiện tượng | Kiểm tra |
| --- | --- |
| Ready unhealthy | SQL, Redis nếu bật Redis mode, Data Protection và production config theo [Runbook](../Service/operations.md) |
| Seed không chạy | `Seeding__Enabled`, đủ secret, flag sau `--` |
| Lỗi concurrency trên UI | Reload qua use case; không giữ entity/service EF qua operation hoặc retry input stale |
| OpenAPI generation lỗi | Xem lỗi composition/config trong output; không tắt generation làm cách build chuẩn |
| Địa chỉ không hợp lệ | Đối chiếu `wwwroot/data/vietnam-administrative-divisions.json` và route config active |

Load profile tại [test/load](../../test/load/README.md); scenario và evidence migration ở [validation log](../Frontend/react-migration-validation.md). Manual reports cũ đang bị xóa trong working tree, không còn là tài liệu chạy kiểm tra hiện hành. Quy tắc làm việc của agent nằm trong [AGENTS.md](../../AGENTS.md), không sao chép vào tài liệu developer.
