# Phát triển local

Các lệnh .NET dưới đây dành cho runtime backend/Blazor hiện tại. Scaffold React/TypeScript/Ant Design nằm ở `src/MiniLogistics.Frontend`, dùng Node 24.22.1/npm 11.19.1 theo [toolchain](../Frontend/react-migration-toolchain.md). Xem [migration status](../Frontend/react-migration-contracts.md) để biết phạm vi hosting/API đã có.

Frontend: chạy `npm --prefix src/MiniLogistics.Frontend ci`, sau đó `run dev`; validation dùng `run typecheck`, `run lint`, `test`, `run build`. `run test:e2e` dùng Playwright Chromium đã cài. Lệnh `api:generate` chỉ dùng được sau khi browser OpenAPI thật được sinh ở T02.01.

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

## Chẩn đoán thường gặp

| Hiện tượng | Kiểm tra |
| --- | --- |
| Ready unhealthy | SQL, Redis nếu bật Redis mode, Data Protection và production config theo [Runbook](../Service/operations.md) |
| Seed không chạy | `Seeding__Enabled`, đủ secret, flag sau `--` |
| Lỗi concurrency trên UI | Reload qua use case; không giữ entity/service EF qua operation hoặc retry input stale |
| OpenAPI generation lỗi | Xem lỗi composition/config trong output; không tắt generation làm cách build chuẩn |
| Địa chỉ không hợp lệ | Đối chiếu `wwwroot/data/vietnam-administrative-divisions.json` và route config active |

Load profile tại [test/load](../../test/load/README.md); scenario và evidence migration ở [validation log](../Frontend/react-migration-validation.md). Manual reports cũ đang bị xóa trong working tree, không còn là tài liệu chạy kiểm tra hiện hành. Quy tắc làm việc của agent nằm trong [AGENTS.md](../../AGENTS.md), không sao chép vào tài liệu developer.
