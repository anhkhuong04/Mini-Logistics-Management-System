# Mini Logistics Management System

Hệ thống quản lý vận đơn .NET 10 modular monolith: Shop tạo/import đơn, điều phối và giao hàng, COD nội bộ, tracking công khai, quản trị và Partner API/webhook.

UI dùng Blazor Interactive Server; dữ liệu dùng EF Core/SQL Server và Identity; validation/test dùng FluentValidation/xUnit. Redis hỗ trợ quota Partner và cache cấu hình khi chạy production.

## Chuyển đổi frontend

Đã chọn **React + TypeScript + Ant Design** làm frontend đích; các trang nghiệp vụ hiện vẫn chạy Blazor. T01.01 đã tạo scaffold tại `src/MiniLogistics.Frontend`; lệnh frontend ở [Development](docs/Backend/development.md). [Kế hoạch refactor](refactorfe.md) và [trạng thái migration](docs/Frontend/react-migration-contracts.md) phân biệt phần đã triển khai với các phase còn lại.

## Bắt đầu

Đọc [Documentation guide](docs/README.md), sau đó [Development](docs/Backend/development.md) để cấu hình SQL, migration/seed và chạy local. Yêu cầu .NET 10 SDK; Infrastructure tests cần Windows LocalDB.

```powershell
dotnet restore Mini-logistics-manegemant-system.slnx
dotnet build Mini-logistics-manegemant-system.slnx -c Release
dotnet test Mini-logistics-manegemant-system.slnx -c Release
```

## Tài liệu developer

| Nhóm | Nội dung |
| --- | --- |
| [Backend](docs/Backend/README.md) | Kiến trúc, nghiệp vụ, dữ liệu, setup và kiểm tra |
| [Frontend](docs/Frontend/README.md) | UI Blazor hiện tại, hướng React/Ant Design, màn hình, state, form và localization |
| [Service](docs/Service/README.md) | Partner API, webhook, external dependency và vận hành |

`src/` hiện chứa bốn project .NET; `test/` chứa unit/integration/web/load tests; `postman/` chứa API collection; `scripts/` chứa compatibility helper; `deploy/` chứa production configuration example. Bằng chứng kiểm tra migration được ghi tại [validation log](docs/Frontend/react-migration-validation.md).

Agent bắt đầu tại [AGENTS.md](AGENTS.md). Tài liệu trong `.agents/` phục vụ quy trình agent. Dự án dùng cho học tập và portfolio.
