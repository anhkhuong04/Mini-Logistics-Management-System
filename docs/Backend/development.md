# Phát triển local

Backend là ASP.NET Core .NET10 modular monolith; frontend hiện tại React + TypeScript + Ant Design tại `src/MiniLogistics.Frontend`. Dùng Node **24.22.1**/npm **11.19.1** theo [toolchain](../Frontend/react-migration-toolchain.md), .NET10 SDK và LocalDB trên Windows khi chạy Infrastructure tests. Trạng thái/evidence ở [migration status](../Frontend/react-migration-contracts.md) và [validation log](../Frontend/react-migration-validation.md).

```powershell
npm --prefix src/MiniLogistics.Frontend ci
npm --prefix src/MiniLogistics.Frontend run dev:cert
dotnet dev-certs https --trust
# Terminal 1:
dotnet run --project src/MiniLogistics.Web --launch-profile https
# Terminal 2:
npm --prefix src/MiniLogistics.Frontend run dev
```

Mở `https://localhost:5173/`: Vite phục vụ React/HMR và proxy HTTP/static/system tới Web7195, kiểm TLS. Override `MINILOGISTICS_BACKEND_URL`, `MINILOGISTICS_HTTPS_CERT`/`MINILOGISTICS_HTTPS_KEY` khi cần; key/certificate chỉ ở ignored storage. Root host mặc định `BrowserSpa:Enabled=true`, `ServeAtRoot=true`; build frontend trước khi mở UI trực tiếp từ Web. `/app/...` redirect local sang root giữ query. Các endpoint `/api`, `/auth`, `/culture`, `/shop/files`, `/health`, `/openapi` không bị SPA fallback chiếm. Sau cleanup không có Blazor/circuit; chín file Operations đã sửa của người dùng được giữ nguyên tại `archive/legacy-blazor`, ngoài source runtime.

```powershell
npm --prefix src/MiniLogistics.Frontend run typecheck
npm --prefix src/MiniLogistics.Frontend run lint
npm --prefix src/MiniLogistics.Frontend test
npm --prefix src/MiniLogistics.Frontend run build
pwsh -File scripts/Publish-WebWithFrontend.ps1 -OutputPath .agent/release
pwsh -File scripts/Test-PublishedWeb.ps1 -ArtifactPath .agent/release -RootMode
```

Publish script tạo artifact riêng với bundle tại `frontend`; normal .NET build/test không phụ thuộc Node. `BrowserSpa:DistPath` có thể override khi bundle nằm nơi khác. Cùng dist hỗ trợ React dưới `/app` nếu root flag false; không khôi phục Blazor trong artifact cuối. [Operations runbook](../Service/operations.md#react-release-và-rollback-phase11) sở hữu rollback artifact/config và storage guardrails. Script không migrate/seed hay deploy môi trường ngoài.

Playwright cần Chromium đã cài (`npx playwright install chromium` trong frontend), dev certificate và Web Release build. Shell suite tự chạy HTTPS backend/Vite trong `BrowserTests` không Infrastructure workers; data/Identity scenarios chạy bằng opt-in fixture riêng. Fixture dùng cookie/Identity/Application thật và EF InMemory tách biệt, không thay bằng chứng SQL transaction. Chạy `MINILOGISTICS_BROWSER_E2E=1`, `MINILOGISTICS_E2E_ROOT=1` rồi filter `BrowserRealBrowserTests`; xem [.agents/commands](../../.agents/commands.md) cho cách đặt/xóa env và optional pinned Node path. Published smoke dùng process từ artifact; shell smoke không chứng minh production dependency health hoặc external webhook delivery.

React auth ở `/login`, `/register-shop`; JSON cookie/CSRF APIs ở `/api/v1/app/auth/*`, Shop context `/api/v1/app/shop-context`, public reference/banners/tracking ở `/api/v1/public`. Legacy POST `/auth/*` giữ compatibility/antiforgery; không có Razor form renderer. Build Web sinh browser OpenAPI riêng, chạy `npm --prefix src/MiniLogistics.Frontend run api:generate` rồi kiểm drift; không áp browser converters lên Partner API.

Chạy lệnh từ repository root, dùng solution, không dùng `.csproj` lạc ở root.

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

React quản trị tại `/admin` dùng cookie/CSRF và API `/api/v1/app/admin`; active Admin được kiểm trong Application. Cấu hình route/fee vẫn tạo version và invalidation cache trên server; khi bắt đầu cấu hình vùng tuyến, phải chuẩn bị đủ danh sách tỉnh phục vụ vì bản đồ cấu hình thay thế toàn bộ fallback mặc định.

Đặt `BannerImages__RootPath` thành đường dẫn tuyệt đối ngoài thư mục release, trỏ đến storage bền vững có quyền đọc/ghi và backup. Mặc định dùng LocalApplicationData của account chạy host, không dùng release wwwroot. Trước khi bỏ release cũ, copy các ảnh runtime cũ từ `wwwroot/images/banners` vào storage giữ nguyên tên; `/images/banners/...` vẫn giữ URL. Không đưa storage vào bước xóa/copy frontend khi publish. Upload giới hạn5MiB, sáu extension cũ; SVG chỉ nhận subset an toàn, không script/style/event/external resource. Xóa metadata không xóa file ảnh.

Kiểm tra một URL ảnh đã upload trước publish, publish SPA theo lệnh ở trên, xác nhận byte ảnh không đổi sau publish và restart host. HTTP/use-case test: `BrowserAdminTests`, `BrowserBannerTests`; storage: `BannerImageStorageTests`. Xem [contract](../Frontend/react-migration-contracts.md#phase-9--administration-và-banner-io) và [evidence](../Frontend/react-migration-validation.md).

## Integration portal (Phase10)

Portal tại `/partner/integrations` dùng Identity cookie/CSRF, gọi `/api/v1/app/integrations`. Không dùng partner key làm credential của UI. Quyền vẫn là global/Shop/province trong IntegrationScopeService và ManageIntegrations của staff. Host quyết định Live/Sandbox; không có environment override từng client. Rotate đổi key và kích hoạt lại client; thu hồi là active=false. New key chỉ hiển thị một lần; mất response cần kiểm tra prefix/audit rồi mới quyết định rotate lại, không tự retry.

Webhook URL phải qua server URL/DNS policy; browser chỉ gọi upsert/test/retry. Signing secret write-only, tối thiểu32 byte UTF-8, tối đa200 ký tự ở browser; URL tối đa500 ký tự, IP whitelist tối đa2000 ký tự. Không có API reveal/rotate secret đã lưu hoặc deactivate endpoint riêng. Dashboard giữ10 delivery/audit gần nhất/client,100 outbox failures trước filter, usage aggregates hiện có; lỗi browser được redacted. Test queue/retry trong fixture không chứng minh external delivery. Transport signing/SSRF/leasing/retry/retention và Partner contracts không thay đổi. Xem [contracts](../Frontend/react-migration-contracts.md#phase-10--integration-management-portal) và [validation](../Frontend/react-migration-validation.md).

## Chẩn đoán thường gặp

| Hiện tượng | Kiểm tra |
| --- | --- |
| Ready unhealthy | SQL, Redis nếu bật Redis mode, Data Protection và production config theo [Runbook](../Service/operations.md) |
| Seed không chạy | `Seeding__Enabled`, đủ secret, flag sau `--` |
| Lỗi concurrency trên UI | Reload qua use case; không giữ entity/service EF qua operation hoặc retry input stale |
| OpenAPI generation lỗi | Xem lỗi composition/config trong output; không tắt generation làm cách build chuẩn |
| Địa chỉ không hợp lệ | Đối chiếu `wwwroot/data/vietnam-administrative-divisions.json` và route config active |

Load profile tại [test/load](../../test/load/README.md); scenario và evidence migration ở [validation log](../Frontend/react-migration-validation.md). Manual reports cũ đang bị xóa trong working tree, không còn là tài liệu chạy kiểm tra hiện hành. Quy tắc làm việc của agent nằm trong [AGENTS.md](../../AGENTS.md), không sao chép vào tài liệu developer.
