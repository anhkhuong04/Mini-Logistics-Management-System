# Operator Role

Tai lieu nay mo ta role `Operator` hien co trong MiniLogistics. Muc tieu la giup developer hieu luong dieu phoi/van hanh hien tai de phat trien tiep theo huong production logistics.

## Pham vi role

`Operator` la role noi bo phu trach operations hang ngay. Operator khong quan ly user, khong cau hinh shipper working area/capacity, khong quan ly partner integrations. Operator chia se mot so quyen van hanh voi Admin tren shipment lifecycle.

Role enum: `src/MiniLogistics.Domain/Users/UserRole.cs`.

## Entry points

| Entry point | File | Ghi chu |
| --- | --- | --- |
| `/operations/assignments` | `src/MiniLogistics.Web/Components/Pages/OperationsAssignments.razor` | Trang chinh cua Operator. |
| `/tracking` | `src/MiniLogistics.Web/Components/Pages/Tracking.razor` | Public tracking, dung de tra cuu nhanh. |

Navigation hien tai chi hien nhom `Dieu phoi` cho `Admin,Operator`.

## Tinh nang hien co

### Xem pending pickup fallback queue

`GetPendingPickupShipmentsService` tra cac shipment dang `PendingPickup`.

Trang operations dung response nay de hien:

- tracking code
- receiver
- pickup province
- delivery province
- COD amount
- shipping fee
- created time

UI tinh fallback insight dua tren pickup province va active shippers/working areas:

- co shipper match pickup area hay khong.
- option shipper match area duoc sap xep truoc.
- shipper manual override hien sau va co warning.

Files lien quan:

- `src/MiniLogistics.Application/Shipments/GetPendingPickupShipments`
- `src/MiniLogistics.Web/Components/Pages/OperationsAssignmentUiModels.cs`
- `test/MiniLogistics.Web.Tests/OperationsAssignmentUiModelsTests.cs`

### Retry auto assignment

Operator co action `Retry auto` tren tung don pending.

Luong:

```text
Operator bam Retry auto
-> OperationsAssignments.razor goi IAutoAssignShipmentService.AutoAssignAsync
-> AutoAssignShipmentService chi xu ly shipment PendingPickup
-> ShipmentAssignmentSelector chon shipper theo pickup hub/province/ward + load/capacity
-> neu thanh cong, Shipment.AssignShipper doi status -> Assigned
-> publish webhook shipment.status_changed neu shipment co external reference
-> neu khong co shipper, shipment van PendingPickup va UI hien reason
```

Files lien quan:

- `src/MiniLogistics.Application/Shipments/AutoAssignShipment`
- `src/MiniLogistics.Application/Shipments/AssignmentSelection`

### Manual assign va manual override

Operator duoc manual assign shipper cho shipment `PendingPickup`.

Rule service:

- assigning user phai ton tai, active, va co role `Admin` hoac `Operator`.
- target shipper phai ton tai, active, va co role `Shipper`.
- domain chi cho assign shipment `PendingPickup`.
- moi shipment chi co mot active assignment.
- service khong validate working area; day la chu dich manual override.

Files lien quan:

- `src/MiniLogistics.Application/Shipments/AssignShipperToShipment/AssignShipperToShipmentService.cs`
- `src/MiniLogistics.Domain/Shipments/Shipment.cs`

### Theo doi active operations board

`GetOperationsShipmentsService` tra cac shipment operations can theo doi:

- `Assigned`
- `PickingUp`
- `PickedUp`
- `InTransit`
- `Delivering`
- `DeliveryFailed`
- `Delivered` neu COD con `PendingCollection`

Delivered shipment se bi an khoi operations board neu COD khong pending collection.

Response co:

- pickup/delivery address
- receiver info
- COD status
- active shipper summary
- tracking history

Files lien quan:

- `src/MiniLogistics.Application/Shipments/GetOperationsShipments`
- `src/MiniLogistics.Application/CashOnDelivery/ICodTransactionRepository.cs`

### Ho tro update shipment status

Operator co the update status thay shipper khi can support.

`UpdateShipmentStatusService` cho `Admin`, `Operator`, hoac assigned `Shipper` update. Operator khong can la assigned shipper.

Domain lifecycle:

```text
PendingPickup -> Assigned | Cancelled
Assigned -> PickingUp | Cancelled
PickingUp -> PickedUp | Cancelled
PickedUp -> InTransit | Returned
InTransit -> Delivering | Returned
Delivering -> Delivered | DeliveryFailed | Returned
DeliveryFailed -> Delivering | Returned
```

Rules:

- Terminal `Delivered`, `Returned`, `Cancelled` khong update tiep.
- `DeliveryFailed` bat buoc co note.
- `Returned` ap return fee va deactivate active assignments.
- `Delivered` deactivate assignment chi khi COD amount = 0.

Files lien quan:

- `src/MiniLogistics.Application/Shipments/UpdateShipmentStatus`
- `src/MiniLogistics.Domain/Shipments/Shipment.cs`

### COD support

Operator co the confirm COD collected khi shipment da `Delivered` va COD transaction dang `PendingCollection`.

Rule:

- `MarkCodCollectedService` cho `Admin`, `Operator`, hoac assigned `Shipper`.
- COD chi collect duoc khi shipment status = `Delivered`.
- COD status phai la `PendingCollection`.
- Sau khi collected, active assignment bi deactivate.

Operator khong duoc settle COD; `MarkCodSettledService` chi cho Admin.

Files lien quan:

- `src/MiniLogistics.Application/CashOnDelivery/MarkCodCollected`
- `src/MiniLogistics.Application/CashOnDelivery/MarkCodSettled`
- `src/MiniLogistics.Domain/CashOnDelivery/CodTransaction.cs`

## Luong operations chinh

### Don moi da auto assign thanh cong

```text
Shop/Partner tao shipment
-> auto assignment thanh cong
-> shipment status Assigned
-> shipment xuat hien tren active operations board
-> Operator theo doi va support status/COD neu can
```

### Don moi fallback manual

```text
Shop/Partner tao shipment
-> auto assignment khong tim duoc shipper
-> shipment giu PendingPickup
-> Operator thay trong pending queue
-> retry auto assign hoac manual assign active shipper
-> shipment status Assigned
```

### Delivery failed support

```text
Shipper/Operator update Delivering -> DeliveryFailed
-> note bat buoc
-> shipment van co active assignment
-> Operator co the update DeliveryFailed -> Delivering de retry
-> hoac DeliveryFailed -> Returned
```

## Business rules can nho

- Operator la role van hanh, khong co quyen admin-only.
- Operator co quyen manual override assignment ngoai working area.
- Operator co quyen update status moi shipment operations, khong bi gioi han active assignment.
- Operator co quyen confirm COD collected, nhung khong settle COD.
- Operator khong quan ly API clients/webhooks.

## Production planning notes

- Can tach operations permissions neu production co nhieu cap: dispatcher, supervisor, COD officer.
- Hien `Retry auto` la per-shipment; bulk retry chua co.
- Manual override note hien co, nhung chua co approval/audit dashboard rieng cho override mismatch.
- Operations board dang gom pickup + delivery + COD support trong mot page; production co the can tach queue theo station/team.
- Chua co assignment reassign sau khi da Assigned; domain `AssignShipper` chi cho `PendingPickup`.
- Chua co SLA/escalation, route batching, shift scheduling, proof of delivery.
