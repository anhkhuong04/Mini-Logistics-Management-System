# MiniLogistics MVP Task Board

Muc tieu: theo doi cong viec hang ngay cho den khi MVP chay duoc end-to-end: Shop tao don -> Operator/Admin assign -> Shipper giao hang -> COD collected -> Public tracking.

## Quy Uoc Trang Thai

- `[ ]` Chua lam
- `[~]` Dang lam
- `[x]` Hoan thanh
- `[!]` Dang bi chan / can quyet dinh

## Bao Cao Tien Do MVP Hien Tai

Cap nhat ngay 2026-06-24 sau khi doi chieu `overview.md` voi code/task hien tai.

Ket luan ngan: MVP core trong `overview.md` da hoan thanh end-to-end va da vuot muc demo ban dau. Cac hang muc hardening P3 da duoc trien khai xong; board hien khong con task P3 dang mo.

| Hang muc MVP theo overview | Tien do hien tai | Ghi chu |
| --- | --- | --- |
| Shop tao don | Done | Shop login/register, tao shipment, tu tinh fee/insurance/chargeable weight/route, xem danh sach/chi tiet, cancel khi hop le. |
| Operator/Admin dieu phoi | Done | `/operations/assignments` da cover assign shipper active, theo doi don dang van hanh, update status support, COD support. |
| Shipper giao hang | Done | `/shipper/shipments` da cover active assignments, lifecycle update, required note khi `DeliveryFailed`, COD collected. |
| Public tracking | Done | `/tracking` tra cuu tracking code, hien timeline va actor an toan cho public. |
| COD core | Done | Tao COD transaction, collect sau delivered, close active assignment sau collected, Admin settle COD. |
| Business rules | Done | Assignment permission, status lifecycle, terminal rules, COD rules, cancel/returned/collected workspace filtering da co test. |
| Admin hardening | Done | `/admin/users` cho Admin list users, tao Shipper/Operator, activate/deactivate; inactive user khong login/assign duoc. |
| DB setup | Done | Option B: `--migrate`, `--seed`, `--migrate --seed`; normal web startup khong migrate/seed ngam. |
| Regression tests | Done | Application tests + SQL Server LocalDB integration tests pass `38/38`. |
| Demo checklist/docs | Done | `DEMO-CHECKLIST.md` co lenh setup DB moi, run app, flow demo, va lenh integration tests. |

Chenh lech voi `overview.md`:

- `overview.md` dang bi cu o muc verification: van ghi `Passed: 14`; hien tai full solution test la `38/38`.
- Cac "Task tiep theo" trong `overview.md` da duoc xu ly: manual validation, UI polish, tests bo sung, audit timeline, COD settlement, Admin user management, setup DB hardening, integration tests.
- Tai thoi diem nay, viec con lai khong thuoc MVP core/P3 da chot: commit/push cac thay doi hien tai, cap nhat `overview.md` neu muon tai lieu overview dong bo voi task board, va cac viec production hoa neu phat sinh sau demo.

## Notes Da Hoan Thanh

- P0 blockers done:
  - Seed demo shop da dung province `Ho Chi Minh` va update lai shop demo cu khi seed.
  - `DEMO-CHECKLIST.md` da co buoc `dotnet ef database update` truoc seed.
  - DB moi tu dau da verify migrate + seed + login 4 role + shop/fee rules san sang.

- P1 E2E validation done:
  - Browser E2E pass flow Shop create -> Operator assign -> Shipper delivery lifecycle -> COD collected -> Public tracking.
  - Tracking code da verify: `ML202606240355484425`.
  - Fee/route da verify: `InterRegion`, chargeable weight `1.200 kg`, insurance `10,000 VND`, total fee `61,000 VND`.
  - Da sua EF client-generated Guid key bug bang `ValueGeneratedNever()` cho cac entity lien quan; khong can migration moi.

- P1 UX demo flow done:
  - Login redirect theo role: Shop -> `/dashboard`, Admin/Operator -> `/operations/assignments`, Shipper -> `/shipper/shipments`.
  - Landing tracking form submit GET toi `/tracking?code=...`; trang tracking preload query `code`.
  - Landing/login/register/nav critical image paths da co asset that, khong con reference 404.

- P2 test bo sung done:
  - Operations workspace filter da cover: Delivered + COD pending hien; Delivered + COD collected, Returned, Cancelled bi an.
  - Shipper workspace da cover sau `MarkCodCollectedService`: active assignment dong va shipment bien mat khoi `GetAssignedShipmentsForShipper`.
  - Cancel shipment da cover cho don `Assigned` va `PickingUp`: active assignment bi dong.
  - Route classification da cover aliases `Ho Chi Minh`, `Thanh pho Ho Chi Minh`, `Ha Noi`, `Thanh pho Ha Noi`.
  - Create shipment service da cover active shop tao don, COD transaction, route, chargeable weight, fee breakdown.

- P2 UI polish done:
  - Operations table responsive da verify desktop/mobile; mobile doi table thanh card rows, khong tran ngang body.
  - Shipper shipment cards responsive da verify desktop/mobile; action controls khong vuot card.
  - `RegisterShop`, `NotFound`, `Error`, not-authorized va cac auth/business error critical path da Viet hoa ro hon.
  - Date/time display gom ve helper chung `UiDisplay.FormatLocalDateTime` voi format `dd/MM/yyyy HH:mm`.

- P3.1 audit timeline done:
  - Timeline response co actor id/name/email; public tracking khong lo email, internal pages hien day du hon.
  - Query mapping actor da cover user inactive va user khong tim thay.

- P3.2 Admin COD settlement done:
  - Admin xem COD da thu chua doi soat trong `/operations/assignments` va chot `Doi soat COD`.
  - Service enforce chi Admin active settle duoc, chi COD `Collected` moi duoc settle.

- P3.3 Admin user/shipper management done:
  - Scope demo chot: Admin list user, tao Shipper/Operator, activate/deactivate Shipper/Operator.
  - `/admin/users` chi Admin thay tren nav; form tao user noi bo nhanh.
  - Deactivated shipper bien mat khoi active shipper list, khong duoc assign va login bi chan.

- P3.4 setup DB hardening done:
  - Chot Option B: command ro rang `--migrate`, `--seed`, hoac `--migrate --seed`.
  - Normal web startup khong migrate/seed ngam; command setup chay xong thi exit.
  - `DEMO-CHECKLIST.md` da dung `dotnet run --project src/MiniLogistics.Web -- --migrate --seed`, khong phu thuoc EF CLI.

- P3.5 integration tests done:
  - Da them project `test/MiniLogistics.Infrastructure.Tests` dung SQL Server LocalDB va service/repository EF that.
  - Cover migrate + seed idempotent, create shipment persist fee/COD/status history, delivery/COD collect/settle, cancel/returned workspace filtering.
  - `DEMO-CHECKLIST.md` da co lenh chay rieng integration tests.

## P3 - Hardening Sau MVP

Muc tieu P3: nang demo tu "chay duoc E2E" len "co tinh quan tri, audit, setup va regression tin cay hon". Khong bat buoc cho MVP core, nhung nen lam neu can demo Admin day du hoac chuan bi review code.

Khong con task P3 dang mo trong board hien tai.

## Verification Gan Nhat

- [x] `dotnet build .\Mini-logistics-manegemant-system.slnx`: pass, 0 warning, 0 error.
- [x] `dotnet test .\Mini-logistics-manegemant-system.slnx`: pass 38/38.
- [x] `dotnet test .\test\MiniLogistics.Infrastructure.Tests\MiniLogistics.Infrastructure.Tests.csproj`: pass 4/4, tao/xoa LocalDB rieng.
- [x] P3.4 setup command pass tren DB tam `MiniLogisticsP34Setup_20260624204738`:
  - `dotnet run --project src/MiniLogistics.Web --no-build -- --migrate --seed`
  - DB moi duoc tao, migrations apply thanh cong, seed demo roles/users/shop thanh cong, process exit khong start web.
  - `dotnet run --project src/MiniLogistics.Web --no-build -- --seed` pass lai tren schema da co, seed idempotent.
- [x] Playwright P3.3 admin user management check pass tren DB tam `MiniLogisticsP33UserMgmt_20260624203549`:
  - Admin login -> `/admin/users` -> tao Shipper moi -> deactivate -> user inactive khong login duoc.
  - User verify: `p33.driver.1782308472699@example.test`.
  - Screenshot: `%TEMP%\minilogistics-p33-admin-users-1782308472699.png`.
- [x] Cac verify truoc do da pass: P3.2 COD settlement, P3.1 audit timeline, P2 UI polish/E2E.

## Ghi Chu Hien Tai

- RUI RO CHINH DA XU LY: demo seed province mismatch, EF child entity insert/update bug, login role redirect, landing tracking form tinh, landing asset 404, UI demo responsive/error message/date-time polish, audit timeline actor, Admin COD settlement, Admin user/shipper management, setup DB command khong phu thuoc EF CLI, regression EF/SQL Server LocalDB.
