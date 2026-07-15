# Shipper Role

Tai lieu nay mo ta role `Shipper` hien co trong MiniLogistics. Shipper la nguoi xu ly pickup/delivery va COD collection cho cac shipment dang co active assignment.

## Pham vi role

`Shipper` chi lam viec tren shipment duoc assign active cho chinh user do. Shipper khong tu chon working area/capacity; Admin cau hinh cac thong tin nay. Shipper co the xem workspace, xem areas/capacity hien tai, update shipment status theo lifecycle va confirm COD collected khi giao thanh cong.

Role enum: `src/MiniLogistics.Domain/Users/UserRole.cs`.

## Entry points

| Entry point | File | Ghi chu |
| --- | --- | --- |
| `/shipper/shipments` | `src/MiniLogistics.Web/Components/Pages/ShipperShipments.razor` | Workspace chinh cua Shipper. |
| `/tracking` | `src/MiniLogistics.Web/Components/Pages/Tracking.razor` | Public tracking bang tracking code. |

Navigation hien tai chi hien `Don duoc giao` cho role `Shipper`.

## Tinh nang hien co

### Xem workspace shipment duoc assign

`GetAssignedShipmentsForShipperService` tra shipment co active assignment voi `shipperUserId`.

Rule:

- `shipperUserId` bat buoc.
- User phai ton tai, active, va co role `Shipper`.
- Repository chi lay shipment co active assignment cua shipper.
- Khong hien `Returned` hoac `Cancelled`.
- `Delivered` chi con hien neu COD status la `PendingCollection`.

Response gom:

- tracking code
- sender/receiver
- pickup/delivery address
- COD amount + COD status
- shipping fee
- shipment status
- created time
- tracking history

Files lien quan:

- `src/MiniLogistics.Application/Shipments/GetAssignedShipmentsForShipper`
- `src/MiniLogistics.Infrastructure/Persistence/Repositories/ShipmentRepository.cs`

### Xem working areas va capacity

Shipper workspace hien:

- active working areas/hubs
- `IsAvailableForAssignment`
- `MaxActiveShipments`
- active load hien tai

`GetShipperWorkingAreasService` cho shipper tu xem working areas cua chinh minh. Neu requested user khac shipper id, service yeu cau Admin.

Luu y: hien tai shipper khong co service de tu doi availability/capacity/area.

Files lien quan:

- `src/MiniLogistics.Application/Shippers/GetShipperWorkingAreas`
- `src/MiniLogistics.Application/Identity/IIdentityService.cs`
- `src/MiniLogistics.Infrastructure/Identity/ApplicationUser.cs`

### Update shipment status

Shipper update status qua `UpdateShipmentStatusService`.

Permission rule:

- User phai ton tai va active.
- User co role `Shipper`.
- Shipper chi update duoc shipment co active assignment cua chinh minh.
- Admin/Operator co the update support ma khong can assignment.

Domain lifecycle:

```text
Assigned -> PickingUp | Cancelled
PickingUp -> PickedUp | Cancelled
PickedUp -> InTransit | Returned
InTransit -> Delivering | Returned
Delivering -> Delivered | DeliveryFailed | Returned
DeliveryFailed -> Delivering | Returned
```

Shipper workspace thuc te bat dau o `Assigned`, vi shipment chi vao workspace sau khi co active assignment.

Important rules:

- `DeliveryFailed` bat buoc co note.
- Terminal `Delivered`, `Returned`, `Cancelled` khong update tiep.
- `Returned` deactivate active assignment.
- `Delivered` deactivate assignment ngay neu COD amount = 0.
- `Delivered` voi COD amount > 0 giu active assignment cho den khi COD collected.

Files lien quan:

- `src/MiniLogistics.Application/Shipments/UpdateShipmentStatus`
- `src/MiniLogistics.Domain/Shipments/Shipment.cs`

### Confirm COD collected

Shipper co the confirm COD collected khi:

- shipment da `Delivered`.
- COD transaction dang `PendingCollection`.
- shipment van co active assignment cua shipper.

Sau khi collected:

- `CodTransaction.Status` -> `Collected`.
- `CollectedAtUtc` va `CollectedByUserId` duoc set.
- `Shipment.DeactivateActiveAssignments()` duoc goi.
- shipment bien mat khoi workspace chinh vi khong con active assignment/COD pending.

Files lien quan:

- `src/MiniLogistics.Application/CashOnDelivery/MarkCodCollected`
- `src/MiniLogistics.Domain/CashOnDelivery/CodTransaction.cs`

### Nhan shipment tu auto/manual assignment

Shipper khong pull order. Assignment den tu:

- auto assignment khi Shop/Partner API tao shipment.
- retry auto assign tu Operations.
- manual assign tu Admin/Operator.

Auto assignment selector chi chon shipper khi:

- user active va role `Shipper`.
- `IsAvailableForAssignment = true`.
- co active `ShipperWorkingArea` match pickup hub/province/ward.
- active load hien tai nho hon `MaxActiveShipments`.

Active load statuses:

- `Assigned`
- `PickingUp`
- `PickedUp`
- `InTransit`
- `Delivering`
- `DeliveryFailed`

Files lien quan:

- `src/MiniLogistics.Application/Shipments/AssignmentSelection/ShipmentAssignmentSelector.cs`
- `src/MiniLogistics.Application/Shipments/ShipmentLoadStatuses.cs`

## Luong shipper chinh

### Pickup va delivery thanh cong co COD

```text
Shipment Assigned
-> Shipper update PickingUp
-> update PickedUp
-> update InTransit
-> update Delivering
-> update Delivered
-> vi COD PendingCollection, shipment van hien trong workspace
-> Shipper confirm COD collected
-> assignment deactivate
-> shipment khong con trong workspace
```

### Giao that bai va retry

```text
Shipment Delivering
-> Shipper update DeliveryFailed voi note bat buoc
-> shipment van active assignment
-> Shipper/Operator/Admin co the update DeliveryFailed -> Delivering de retry
-> hoac update DeliveryFailed -> Returned
```

### Don return

```text
PickedUp/InTransit/Delivering/DeliveryFailed
-> update Returned
-> domain apply return fee
-> active assignment deactivate
-> shipment bien mat khoi workspace
```

## Business rules can nho

- Shipper khong thay shipment khong assign cho minh.
- Shipper khong update status cua shipment assign cho shipper khac.
- Shipper khong assign/reassign shipment.
- Shipper khong settle COD.
- Shipper khong thay shipment terminal da xong viec: `Returned`, `Cancelled`, `Delivered + COD NotRequired`, `Delivered + COD Collected/Settled`.
- Delivery failed note la bat buoc o domain, khong chi UI.

## Production planning notes

- Can mobile-first/PWA flow cho shipper neu production.
- Chua co proof of pickup/delivery, image/signature/OTP/GPS.
- Chua co route sequencing, map navigation, batch pickup/delivery.
- Shipper availability hien do Admin set; production co the can shift check-in/check-out cua shipper.
- Capacity hien la count active shipment don gian, chua tinh weight/volume/zone/time window.
- COD collection chua co cash handover workflow giua shipper va hub/accounting ngoai `MarkCodSettled` Admin.
