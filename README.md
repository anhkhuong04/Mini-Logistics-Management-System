# Mini Logistics Management System

Hệ thống quản lý vận đơn .NET 10 modular monolith: Shop tạo/import đơn, điều phối và giao hàng, COD nội bộ, tracking công khai, quản trị và Partner API/webhook.

UI dùng Blazor Interactive Server; dữ liệu dùng EF Core/SQL Server và Identity; validation/test dùng FluentValidation/xUnit. Redis hỗ trợ quota Partner và cache cấu hình khi chạy production.

## Chuyển đổi frontend

Phase 8 bổ sung hồ sơ/tạo thêm Shop, nhân viên và báo cáo COD/thông báo/nhật ký dưới `/app`. API kiểm owner/Shop permission, dữ liệu được che theo quyền; tạo Shop cập nhật context từ server, đổi quyền staff được server áp dụng cho cả tab cũ. Báo cáo và export giữ giới hạn/quyền hiện có, không cấp đối soát cho Shop.

Phase 7 bổ sung Shipper mobile tại `/app/shipper/shipments`: các nhóm đơn được phân công, bộ lọc/phân trang, báo cáo COD ngày UTC, trạng thái nhận đơn, cập nhật hành trình và xác nhận thu COD. API `/shipper/me` chỉ dùng actor đăng nhập và phân công active; thao tác chưa rõ kết quả phải refresh trước khi gửi lại. Luồng xuyên Shop → Operator → Shipper → Admin có kiểm thử trình duyệt và đối chiếu history/audit/outbox trong fixture riêng.

Phase 6 bổ sung Operations tại `/app/operations/assignments` và alias `/app/operations/assignment`: phân công/đổi/hủy, bulk retry từng kết quả, cập nhật trạng thái, thu COD và đối soát Admin. API kiểm quyền trong Application; giữ capacity SQL, audit/outbox và URL Blazor cũ.

Đã chọn **React + TypeScript + Ant Design** làm frontend đích. Phase 1–8 có hosting opt-in `/app`, theme/layout, JSON Identity/CSRF, đăng nhập/đăng ký, Shop context theo quyền, dashboard, shipment read/mutation, CSV import có preview/edit/batch polling và tải export/label qua cookie. Quote và phí tính trên server. Icon dùng `@ant-design/icons`. Các chức năng chưa chuyển tiếp tục mở Blazor; URL cũ vẫn hoạt động trong coexist. Lệnh HTTPS dev, test và publish ở [Development](docs/Backend/development.md); xem [kế hoạch refactor](refactorfe.md) và [trạng thái migration](docs/Frontend/react-migration-contracts.md).

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

`src/` chứa bốn project .NET và frontend React; `test/` chứa unit/integration/web/load tests; `postman/` chứa API collection; `scripts/` chứa compatibility helper; `deploy/` chứa production configuration example. Bằng chứng kiểm tra migration được ghi tại [validation log](docs/Frontend/react-migration-validation.md).

Agent bắt đầu tại [AGENTS.md](AGENTS.md). Tài liệu trong `.agents/` phục vụ quy trình agent. Dự án dùng cho học tập và portfolio.
