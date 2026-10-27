# Mini Logistics Management System

Hệ thống quản lý vận đơn .NET 10 modular monolith: Shop tạo/import đơn, điều phối và giao hàng, COD nội bộ, tracking công khai, quản trị và Partner API/webhook.

Frontend dùng **React + TypeScript + Ant Design** tại `src/MiniLogistics.Frontend`; ASP.NET Core host HTTP/static assets và workers, EF Core/SQL Server và Identity giữ nghiệp vụ/dữ liệu. Icon dùng thư viện `@ant-design/icons`. Redis hỗ trợ quota Partner và cache cấu hình production.

## Frontend và trạng thái refactor

Phases 0–11 đã hoàn thành các lát cắt nền tảng/auth/Shop context, dashboard, vận đơn và quote, CSV import/export/label, Operations, Shipper mobile, Shop settings/reporting, Administration/banner và Integration portal. Phase11 đã hoàn tất phần public landing/FAQ/contact, tracking summary, lazy routes, QA và root cutover; trạng thái kiểm chứng cuối cùng nằm trong [kế hoạch lưu trữ](docs/Frontend/history/refactorfe.md) và [validation log](docs/Frontend/react-migration-validation.md).

React sở hữu các route UI tại root, gồm `/login`, `/dashboard`, `/shipments`, `/operations/assignments`, `/shipper/shipments`, `/admin`, `/partner/integrations`; các deep link `/app/...` được chuyển sang root và giữ query. `/api`, `/auth`, `/culture`, `/shop/files`, `/health`, `/openapi` giữ chủ sở hữu HTTP riêng. Default host config là `BrowserSpa:Enabled=true`, `ServeAtRoot=true`. Bản cleanup không chạy circuit/bundle Blazor. Chín file Operations có thay đổi của người dùng được bảo toàn nguyên trạng tại `archive/legacy-blazor`, ngoài source runtime. Cấu trúc và quy tắc dependency hiện tại ở [source organization](docs/Backend/source-organization.md).

Public tracking chỉ trả summary và timeline an toàn, tên được mask, không trả số điện thoại/địa chỉ đầy đủ; xác minh phone-last-four chưa phải flow UI. Contact dùng các liên kết thật, không có form gửi giả. Đây là SPA, chưa có SSR hay cam kết SEO server-rendered.

## Bắt đầu

Đọc [Documentation guide](docs/README.md), [Development](docs/Backend/development.md) và [Frontend](docs/Frontend/README.md). Dùng .NET 10 SDK, Node **24.22.1**, npm **11.19.1**; Infrastructure tests cần Windows LocalDB. Không dùng file `.csproj` lạc ở root.

```powershell
npm --prefix src/MiniLogistics.Frontend ci
npm --prefix src/MiniLogistics.Frontend run dev:cert
dotnet dev-certs https --trust
dotnet run --project src/MiniLogistics.Web --launch-profile https
# Terminal khác:
npm --prefix src/MiniLogistics.Frontend run dev
```

Vite tại `https://localhost:5173/` proxy HTTP đến Web7195 với TLS được kiểm chứng. Build frontend trước khi phục vụ UI trực tiếp từ Web. Migration và seed là thao tác explicit, không tự chạy khi khởi động host.

```powershell
dotnet restore Mini-logistics-manegemant-system.slnx
dotnet build Mini-logistics-manegemant-system.slnx -c Release
dotnet test Mini-logistics-manegemant-system.slnx -c Release
pwsh -File scripts/Publish-WebWithFrontend.ps1 -OutputPath .agent/release
pwsh -File scripts/Test-PublishedWeb.ps1 -ArtifactPath .agent/release -RootMode
```

Publish script tạo artifact riêng, không triển khai production. Giữ `BannerImages__RootPath` tuyệt đối ngoài release, Data Protection key ring/certificate và SQL/Redis config ổn định giữa các release. Rollback sau cleanup dùng **artifact trước cleanup**, không chỉ tắt root flag; không rollback database cho thay đổi renderer. Xem [release/rollback runbook](docs/Service/operations.md#react-release-và-rollback-phase11).

## Tài liệu developer

| Nhóm | Nội dung |
| --- | --- |
| [Backend](docs/Backend/README.md) | Kiến trúc, nghiệp vụ, dữ liệu, setup và kiểm tra |
| [Frontend](docs/Frontend/README.md) | React/Ant Design, route, state, form, localization và QA |
| [Service](docs/Service/README.md) | Partner API, webhook, external dependency và vận hành |

`src/` chứa bốn project .NET và frontend React; `test/` chứa unit/integration/web/load tests; `postman/` chứa API collection; `scripts/` chứa compatibility/publish/smoke helpers; `deploy/` chứa production configuration example. [AGENTS.md](AGENTS.md) là entry point của agent. Dự án dùng cho học tập và portfolio.
