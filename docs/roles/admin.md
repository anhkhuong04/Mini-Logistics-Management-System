# Admin Role

Tai lieu nay mo ta role `Admin` hien co trong MiniLogistics de developer nam duoc luong xu ly, tinh nang da co va cac diem can can nhac khi production hoa.

## Pham vi role

`Admin` la role noi bo co quyen cao nhat trong he thong hien tai. Admin khong tao shipment thay shop trong web UI, nhung co the quan tri nguoi dung noi bo, cau hinh shipper cho auto assignment, dieu phoi don giong Operator, xu ly COD fallback va quan ly partner integrations cua tat ca shop.

Role enum: `src/MiniLogistics.Domain/Users/UserRole.cs`.

## Entry points

| Entry point | File | Ghi chu |
| --- | --- | --- |
| `/admin/users` | `src/MiniLogistics.Web/Components/Pages/AdminUsers.razor` | Chi `Admin`. Quan ly Operator/Shipper, working areas, capacity. |
| `/operations/assignments` | `src/MiniLogistics.Web/Components/Pages/OperationsAssignments.razor` | `Admin,Operator`. Dieu phoi, retry auto assign, manual override, update status, COD support. |
| `/partner/integrations` | `src/MiniLogistics.Web/Components/Pages/PartnerIntegrations.razor` | `Shop,Admin`. Admin thay va quan ly integrations cua moi shop. |
| `/tracking` | `src/MiniLogistics.Web/Components/Pages/Tracking.razor` | Public tracking, Admin co the dung de tra cuu nhu nguoi dung thuong. |

## Tinh nang hien co

### Quan ly nguoi dung noi bo

Admin tao duoc tai khoan noi bo cho `Operator` hoac `Shipper` qua `CreateInternalUserService`.

Rule hien tai:

- Chi active Admin duoc tao user noi bo.
- Validator chi chap nhan role `Operator` hoac `Shipper`; khong tao Admin moi tu UI/service nay.
- User moi duoc tao active theo implementation identity hien tai.

Files lien quan:

- `src/MiniLogistics.Application/AdminUsers/CreateInternalUser`
- `src/MiniLogistics.Application/AdminUsers/AdminUserAuthorization.cs`
- `src/MiniLogistics.Infrastructure/Identity/IdentityService.cs`

### Xem danh sach user va working area cua shipper

`GetAdminUsersService` lay tat ca identity users + roles, sau do enrich them working areas cho user co role `Shipper`.

Du lieu hien thi gom:

- `FullName`, `Email`, `PhoneNumber`
- `IsActive`
- `IsAvailableForAssignment`
- `MaxActiveShipments`
- roles
- active working areas + hub metadata

Files lien quan:

- `src/MiniLogistics.Application/AdminUsers/GetAdminUsers`
- `src/MiniLogistics.Application/Shippers/ShipperWorkingAreaResponseMapper.cs`
- `src/MiniLogistics.Domain/Operations/Hub.cs`
- `src/MiniLogistics.Domain/Operations/ShipperWorkingArea.cs`

### Active/deactive Operator va Shipper

Admin bat/tat active status bang `SetUserActiveStatusService`.

Rule hien tai:

- Chi active Admin duoc thao tac.
- Target phai ton tai.
- Chi target role `Shipper` hoac `Operator` duoc active/deactive qua service nay.
- Deactive shipper lam shipper bien mat khoi danh sach active shippers va khong the manual assign.

Files lien quan:

- `src/MiniLogistics.Application/AdminUsers/SetUserActiveStatus`

### Gan working areas cho shipper

Admin gan hub/ward/zone cho shipper bang `SetShipperWorkingAreasService`.

Rule hien tai:

- Chi active Admin duoc set areas.
- Target phai co role `Shipper`.
- Command nhan danh sach area item gom `HubId`, optional `Ward`, optional `ZoneCode`.
- Gioi han 30 working areas/shipper.
- Duplicate bi reject sau normalize whitespace/null.
- Hub phai ton tai va active.
- Service deactivate current active areas khong con trong request, reactivate area cu neu request trung, hoac tao area moi.
- Shipper khong tu set area de auto assign ngay.

Files lien quan:

- `src/MiniLogistics.Application/Shippers/SetShipperWorkingAreas`
- `src/MiniLogistics.Application/Shippers/GetShipperWorkingAreas`
- `src/MiniLogistics.Application/Shippers/IShipperWorkingAreaRepository.cs`

### Cau hinh capacity shipper

Admin cau hinh:

- `IsAvailableForAssignment`
- `MaxActiveShipments`

Auto assignment selector chi xet shipper active, available va chua vuot `MaxActiveShipments`.

Files lien quan:

- `src/MiniLogistics.Application/AdminUsers/SetShipperCapacity`
- `src/MiniLogistics.Application/Shipments/AssignmentSelection/ShipmentAssignmentSelector.cs`
- `src/MiniLogistics.Application/Shipments/ShipmentLoadStatuses.cs`

### Dieu phoi va van hanh don hang

Admin dung cung operations workspace voi Operator.

Admin co the:

- xem don `PendingPickup` chua auto assign duoc.
- retry auto assignment tung don.
- manual assign active shipper, ke ca khi khong match pickup area; UI canh bao mismatch.
- theo doi don dang van hanh trong cac status `Assigned`, `PickingUp`, `PickedUp`, `InTransit`, `Delivering`, `DeliveryFailed`, va `Delivered` neu COD con pending.
- update shipment status theo lifecycle rule.
- confirm COD collected fallback.
- settle COD collected.

Files lien quan:

- `src/MiniLogistics.Application/Shipments/GetPendingPickupShipments`
- `src/MiniLogistics.Application/Shipments/GetOperationsShipments`
- `src/MiniLogistics.Application/Shipments/AssignShipperToShipment`
- `src/MiniLogistics.Application/Shipments/UpdateShipmentStatus`
- `src/MiniLogistics.Application/CashOnDelivery/MarkCodCollected`
- `src/MiniLogistics.Application/CashOnDelivery/MarkCodSettled`

### Partner integrations cua moi shop

Admin truy cap `/partner/integrations` va co the quan ly API clients/webhook endpoints cho tat ca shops.

`PartnerIntegrationManagementService.GetAccessibleShopsAsync` cho Admin active lay `IShopRepository.GetAllAsync`.

Admin co the:

- xem shops va API clients.
- tao API client.
- rotate key.
- activate/deactivate API client.
- upsert webhook endpoint.
- tao webhook test delivery.

Files lien quan:

- `src/MiniLogistics.Application/PartnerApi/PartnerIntegrationManagementService.cs`
- `src/MiniLogistics.Domain/PartnerApi`

## Luong xu ly quan trong

### Gan shipper auto assignment-ready

```text
Admin login
-> /admin/users
-> tao hoac chon Shipper
-> set active = true
-> set IsAvailableForAssignment + MaxActiveShipments
-> gan Hub/Ward working areas
-> selector co the chon shipper khi shop/partner tao shipment moi
```

### Manual override shipment

```text
Admin login
-> /operations/assignments
-> xem PendingPickup + fallback reason
-> chon shipper active
-> neu khong match area, UI canh bao manual override
-> AssignShipperToShipmentService validate Admin/Operator + target Shipper active
-> Shipment.AssignShipper doi status PendingPickup -> Assigned
-> publish webhook shipment.status_changed neu shipment co external reference
```

### COD fallback va settlement

```text
Shipment Delivered + COD PendingCollection
-> Admin/Operator co the MarkCodCollected
-> CodTransaction.MarkCollected validate shipment Delivered va COD PendingCollection
-> Shipment.DeactivateActiveAssignments
-> Admin settle COD bang MarkCodSettled
```

## Business rules can nho

- Admin active la dieu kien bat buoc cho admin-only services.
- Admin khong duoc tao Admin khac tu `CreateInternalUserService`.
- Manual assign chi duoc khi shipment dang `PendingPickup`.
- Status terminal `Delivered`, `Returned`, `Cancelled` khong update tiep duoc.
- `DeliveryFailed` bat buoc co note.
- `Returned` tu dong ap return fee va deactivate assignment.
- `Delivered` chi deactivate assignment ngay neu COD amount = 0; neu COD pending thi giu assignment de shipper/Admin/Operator confirm.

## Production planning notes

- Can tach permission chi tiet hon neu Admin khong nen quan ly tat ca shop integrations trong production.
- Can audit log hien thi cho thao tac admin: create user, set active, set capacity, set working areas, rotate/revoke API key.
- `SetUserActiveStatusService` hien chi cho Shipper/Operator; neu can lock Shop/Admin thi can service/policy rieng.
- Chua co UI quan ly Hub; hub dang seed/demo va repository da co.
- Chua co approval flow cho shipper working area; Admin set truc tiep.
- COD settlement hien co service va operations UI hook, nhung chua thanh module accounting day du.
- In-memory partner API rate limiter phu hop demo, production can distributed rate limiter.
