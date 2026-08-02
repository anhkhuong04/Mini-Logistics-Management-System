# Bao Cao Hoan Thanh TASK-SHOP-7 Va TASK-SHOP-8

Ngay bao cao: 2026-08-02

## Ket Qua

| Task | Trang thai | Ket qua chinh |
| --- | --- | --- |
| TASK-SHOP-7 | Hoan thanh | Shop owner co the tao va quan ly staff/sub-account theo tung shop, role preset hoac permission tuy chinh. |
| TASK-SHOP-8 | Hoan thanh | Create shipment, import preview va import confirm goi truc tiep tu Blazor deu co rate limit theo user/action. |

## TASK-SHOP-7

- Bo sung `ShopStaffMembership`, `ShopStaffRole` va permission flags cho shipment, PII, export, COD, audit, integration, notification va shop profile.
- Shop owner mac dinh co toan quyen; staff chi truy cap shop dang co membership active va dung permission.
- UI `/shop/staff` cho owner tao sub-account, chon role preset, tuy chinh permission va khoa/mo membership.
- Cac use case Shop kiem tra permission tai Application layer; UI an action khong duoc phep.
- Ten, dien thoai, dia chi va COD duoc mask/an khi staff khong co quyen; filter/sort nhay cam bi tu choi tai backend.
- Audit ghi nhan tao staff va thay doi access.

## TASK-SHOP-8

- Quota rieng cho `CreateShipment`, `ImportPreview` va `ImportConfirm` trong `IShopUiActionRateLimiter`.
- `CreateShipment.razor` rate limit truoc create shipment hoac create draft moi.
- `Shipments.razor` rate limit truoc import preview va import confirm.
- Khi vuot quota, UI tra thong bao co thoi gian retry; quota tach theo user va action kind.

## Database

- Migration: `20260802031535_AddShopStaffPermissions`.
- Migration da apply thanh cong tren LocalDB.
- EF xac nhan khong co model change chua tao migration.

## Quality Gate

- Build solution: dat, 0 warning, 0 error.
- Test solution: 192/192 dat.
- Domain: 22/22.
- Application: 142/142.
- Infrastructure: 11/11.
- Web: 17/17.
- `git diff --check`: dat.
