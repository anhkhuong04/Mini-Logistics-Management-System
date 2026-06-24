# MiniLogistics MVP Task Board

Muc tieu: theo doi cong viec hang ngay cho den khi MVP chay duoc end-to-end: Shop tao don -> Operator/Admin assign -> Shipper giao hang -> COD collected -> Public tracking.

## Quy Uoc Trang Thai

- `[ ]` Chua lam
- `[~]` Dang lam
- `[x]` Hoan thanh
- `[!]` Dang bi chan / can quyet dinh

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

## P3 - Hardening Sau MVP

- [ ] Bo sung Admin user/shipper management neu can demo quan tri day du hon.
- [ ] Hien thi nguoi cap nhat status trong tracking/audit timeline.
- [ ] Bo sung COD settlement flow cho Admin.
- [ ] Can nhac auto-migrate chi trong Development hoac them command rieng cho setup DB.
- [ ] Can nhac integration tests voi EF Core/SQL Server hoac testcontainer/localdb.

## Verification Gan Nhat

- [x] `dotnet build .\Mini-logistics-manegemant-system.slnx`: pass, 0 warning, 0 error.
- [x] `dotnet test .\Mini-logistics-manegemant-system.slnx`: pass 23/23.
- [x] Playwright UI polish check pass tren DB tam `MiniLogisticsP2UiVerify_20260624153801`:
  - `RegisterShop`, `NotFound`, `Error` hien text tieng Viet dung.
  - Login sai mat khau, tracking khong tim thay, register-shop duplicate error hien message ro rang.
  - Shop tao don -> Admin assign -> Shipper workspace co card; tracking code `ML202606240842192812`.
  - Operations desktop/mobile va Shipper desktop/mobile khong tran ngang; date/time co format `dd/MM/yyyy HH:mm`.
  - Screenshot luu tai `%TEMP%\minilogistics-p2ui-screenshots`.

## Ghi Chu Hien Tai

- RUI RO CHINH DA XU LY: demo seed province mismatch, EF child entity insert/update bug, login role redirect, landing tracking form tinh, landing asset 404, UI demo responsive/error message/date-time polish.
