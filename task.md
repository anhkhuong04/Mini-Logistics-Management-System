# Production Logistics Refactor

Muc tieu: refactor luong dieu phoi don hang tu manual-only sang mo hinh gan voi production logistics thuc te: shipper co khu vuc/tuyen lam viec, he thong uu tien tu dong phan cong don phu hop, admin/operator giu quyen manual override va xu ly fallback.

Quy tac thuc hien:

- Clean code, tach ro domain/application/infrastructure/web.
- Tai su dung `Shipment`, `ShipmentAssignment`, `AssignShipperToShipmentService`, `GetActiveShippersService`, route/address services neu phu hop.
- Khong copy logic assign/validate rai rac; gom vao service/selector co trach nhiem ro.
- Xoa hoac loai bo code/UI/documentation thua sau moi phase neu khong con duoc dung.
- Moi task khi lam xong can cap nhat trang thai tai file nay.

## Hien Trang Codebase

- Don tao tu shop UI va Partner API dang vao `PendingPickup`.
- Admin/Operator vao `/operations/assignments`, chon shipper active bang dropdown va bam `Phan cong`.
- `AssignShipperToShipmentService` chi ho tro manual assign, validate Admin/Operator va role Shipper.
- Shipper account hien chi co `FullName`, `Email`, `PhoneNumber`, `IsActive`, roles; chua co khu vuc/tuyen lam viec.
- Province/Ward hien dang nam trong pickup/delivery address va dung cho route/fee, chua dung de match shipper.
- Partner API/webhook da co va co the giu nguyen lam kenh tao don tu e-commerce/ben thu ba.

## Luong Dich Den

```text
Shop/Partner tao don
-> Shipment PendingPickup
-> Map pickup address ve Hub gia dinh theo province
-> Auto assignment tim shipper theo Hub/working area + active load
-> Neu tim thay: assign va status thanh Assigned
-> Neu khong tim thay: giu PendingPickup de Operations xu ly thu cong
-> Shipper xu ly pickup/delivery/COD
-> Admin/Operator co the manual assign/reassign khi can
```

## Trang Thai Tong Quan

| Priority | Phase | Trang thai | Ket qua mong doi |
| --- | --- | --- | --- |
| P0 | Design boundary va data model | Done | Province-first, SPX-like fake hubs, pickup-first assignment, manual override co warning. |
| P1 | Shipper working areas | Done | Da co Hub, ShipperWorkingArea, repo/service, migration, seed demo va UI admin gan hub. |
| P2 | Assignment selector/auto assign service | Done | Da co selector, load query, auto assign service, result status ro va webhook publish. |
| P3 | Tich hop vao luong tao don | Done | Shop UI va Partner API tao don xong se thu auto assign, response/snapshot phan anh status moi. |
| P4 | Operations UI hybrid | Done | UI hien auto result, fallback reason, manual override theo shipper phu hop. |
| P5 | Shipper workspace va capacity | Done | Shipper thay workspace/area/capacity; selector bo qua shipper tam ngung hoac vuot capacity. |
| P6 | Tests va cleanup | Pending | Coverage cho rule moi, xoa code thua, docs cap nhat. |

## P0 - Design Boundary Va Data Model

- [x] Xac dinh granularity khu vuc: bat dau bang `Province`, mo duong cho `Ward`/`ZoneCode`/`HubCode`.
- [x] Chot ngu nghia pickup vs delivery:
  - Pickup assignment uu tien theo `PickupAddress`.
  - Delivery assignment co the dung `DeliveryAddress` khi mo phase route/last-mile rieng.
- [x] Chot fallback khi khong co shipper phu hop: giu `PendingPickup`, luu reason/log de Operations thay.
- [x] Chot manual override: Admin/Operator duoc assign bat ky active shipper hay chi shipper trong area? De xuat: cho override, nhung UI can canh bao mismatch.
- [x] Xac dinh co can migration seed/demo working areas cho shipper hien co hay khong.

### P0 Decisions

| Topic | Decision |
| --- | --- |
| Area granularity | Dung `Province` lam boundary auto assignment dau tien. Model phai mo duong cho `Ward`, `ZoneCode`, va `HubCode`. |
| Hub assumption | Tao `Hub` gia dinh theo phong cach SPX, khong khang dinh la kho noi bo that cua SPX. Moi province co it nhat 1 hub; cac tinh/thanh lon co the mo rong nhieu hub/zone sau. |
| Initial seed hubs | Seed regional sorting hubs: `SPX-HY-SORT` tai Hung Yen va `SPX-BD-SORT` tai Binh Duong. Seed province hubs cho cac province active trong data dia gioi hien co. |
| Working area owner | Shipper khong tu chon area de duoc assign ngay. Admin/Operator gan `ShipperWorkingArea` cho shipper. |
| Pickup vs delivery | Auto assignment phase dau la pickup-first: match theo `Shipment.PickupAddress.Province` -> `Hub` -> shipper working area. Delivery-first/last-mile split se la phase sau. |
| Fallback | Neu khong co shipper phu hop, shipment giu `PendingPickup`. Tao log/attempt de Operations thay ly do, vi du `NoEligibleShipper`, `NoHubForProvince`, `CapacityFull`. |
| Manual override | Admin/Operator duoc assign bat ky active shipper. UI can canh bao neu shipper khong match hub/area; service van cho phep override khi co note. |
| Data model direction | Them `Hub`, `ShipperWorkingArea`, va `ShipmentAssignmentAttempt`/assignment log de luu ket qua auto assign. Khong dua working area truc tiep vao `ApplicationUser`. |
| Demo migration | Can seed demo hubs va working areas cho shipper hien co/demo de co the test auto assignment ngay sau migration. |

## P1 - Shipper Working Areas

- [x] Them domain/entity `Hub` va `ShipperWorkingArea` phu hop voi kien truc hien tai.
- [x] Them repository/query cho working areas:
  - Lay areas cua shipper.
  - Lay active shippers theo hub/province/ward.
  - Upsert/remove areas cho shipper.
- [x] Them EF configuration va migration.
- [x] Mo rong response `GetActiveShipperResponse` de tra ve working areas can thiet cho UI.
- [x] Them application services:
  - `GetShipperWorkingAreasService`
  - `SetShipperWorkingAreasService`
- [x] Cap nhat Admin Users UI de admin gan/chinh sua khu vuc lam viec cho shipper.
- [x] Khong cho shipper tu do tu chon area de duoc auto assign ngay; neu can form dang ky thi chi la thong tin mong muon, admin van duyet.

### P1 Result

- Them domain entities `Hub`, `ShipperWorkingArea` trong `MiniLogistics.Domain/Operations`.
- Them tables `Hubs`, `ShipperWorkingAreas` qua migration `AddHubAndShipperWorkingAreas`.
- Them repositories `IHubRepository`, `IShipperWorkingAreaRepository` va implementation EF.
- Them services `GetLogisticsHubsService`, `GetShipperWorkingAreasService`, `SetShipperWorkingAreasService`.
- `GetActiveShipperResponse` va `GetAdminUserResponse` da tra ve `WorkingAreas`.
- `DatabaseSeeder` seed SPX-like hubs va gan demo shipper vao `SPX-HCM-HUB`.
- `/admin/users` co panel gan hub lam viec cho shipper va hien working area trong bang user.

## P2 - Assignment Selector Va Auto Assign Service

- [x] Tao `IShipmentAssignmentSelector` de chon shipper tot nhat cho shipment.
- [x] Rule selector ban dau:
  - Shipper active va role `Shipper`.
  - Co working area match pickup hub hoac `PickupAddress.Province`.
  - Uu tien match ward/zone neu data co.
  - Sap xep theo active shipment count tang dan.
  - Tie-breaker on dinh theo `FullName`/`UserId`.
- [x] Tao query dem active load cua shipper dua tren `ShipmentAssignments` con active va status chua terminal.
- [x] Tao `IAutoAssignShipmentService`.
- [x] Service tra result ro:
  - Assigned(shipperId)
  - NoEligibleShipper(reason)
  - Skipped(status/reason)
- [x] Tai su dung domain method `Shipment.AssignShipper` de doi status va tao assignment, khong duplicate transition.
- [x] Publish webhook `ShipmentStatusChanged` khi auto assign thanh cong, tuong tu manual assign.

### P2 Result

- Them `IShipmentAssignmentSelector` va `ShipmentAssignmentSelector`.
- Selector match pickup province/hub, uu tien ward-specific working area, sap xep load tang dan, tie-break theo full name/user id.
- Them `IShipmentRepository.GetActiveAssignmentCountsByShipperIdsAsync` va `ShipmentLoadStatuses.ActiveAssignmentStatuses`.
- Them `IAutoAssignShipmentService` va `AutoAssignShipmentService`.
- Auto assign tra `Assigned`, `NoEligibleShipper`, hoac `Skipped`; khong throw/fail khi khong co shipper phu hop.
- Auto assign dung `Shipment.AssignShipper` va `SystemActorIds.AutoAssignment`.
- `ShipmentStatusHistoryMapper` hien thi actor system la `Auto assignment engine`.
- Tests cover selector load balancing, ward preference, no eligible shipper, auto assign status/webhook.

## P3 - Tich Hop Vao Luong Tao Don

- [x] Inject `IAutoAssignShipmentService` vao `CreateShipmentService`.
- [x] Sau khi luu shipment/COD, goi auto assign trong cung transaction/logical flow neu hop ly.
- [x] Inject `IAutoAssignShipmentService` vao `PartnerCreateShipmentService`.
- [x] Dam bao Partner API response status phan anh ket qua sau auto assign neu auto assign thanh cong.
- [x] Dam bao idempotency replay Partner API khong auto assign lai.
- [x] Neu auto assign fail vi khong co shipper, khong fail create shipment; shipment van duoc tao va cho manual assign.

### P3 Result

- `CreateShipmentService` goi `IAutoAssignShipmentService` sau khi luu shipment/COD.
- `PartnerCreateShipmentService` goi auto assign sau create, khong goi lai khi idempotency replay.
- Partner create response tra status sau auto assign; `ExternalShipmentReference.ResponseSnapshotJson` duoc update khi status doi sang `Assigned`.
- Neu auto assign tra `NoEligibleShipper`/`Skipped`, create shipment van thanh cong va status giu theo shipment hien tai.
- Infrastructure tests da cap nhat theo luong demo moi: demo shop HCM auto assign cho demo shipper HCM.

## P4 - Operations UI Hybrid

- [x] Cap nhat `/operations/assignments`:
  - Hien PendingPickup chua auto assign duoc.
  - Hien reason/fallback neu co.
  - Dropdown uu tien shipper match khu vuc len dau.
  - Canh bao khi manual assign shipper khong match khu vuc.
- [x] Tach component/logic UI neu page qua dai.
- [x] Hien thong tin working area/load cua shipper trong dropdown hoac tooltip ngan.
- [x] Giu manual assign cho Admin/Operator.
- [x] Can nhac them action `Retry auto assign` cho tung don hoac bulk.

### P4 Result

- `/operations/assignments` luon hien don `PendingPickup`, ke ca khi chua co shipper active.
- Pending table co cot fallback reason suy luan theo pickup province va active working areas.
- Dropdown shipper uu tien nhom match pickup area, sau do den manual override; option hien working area va active load.
- Khi chon shipper khong match pickup area, UI canh bao nhung van cho Admin/Operator manual assign voi note override.
- Them action `Retry auto` cho tung don, goi lai `IAutoAssignShipmentService` va hien reason/result tren row.
- Tach logic ranking/fallback sang `OperationsAssignmentUiModels` va them Web tests cho matching/sort dropdown.

## P5 - Shipper Workspace Va Capacity

- [x] Xac dinh active statuses tinh load: `Assigned`, `PickingUp`, `PickedUp`, `InTransit`, `Delivering`, `DeliveryFailed`.
- [x] Them cau hinh capacity don gian cho shipper neu can:
  - `MaxActiveShipments`
  - `IsAvailable`/ca lam viec
- [x] Cap nhat selector de bo qua shipper vuot capacity.
- [x] Cap nhat workspace shipper de hien khu vuc/tuyen hien tai neu data co.
- [x] Kiem tra COD flow sau auto assign khong bi anh huong.

### P5 Result

- Them cau hinh shipper `IsAvailableForAssignment` va `MaxActiveShipments` tren identity user, EF config, migration `AddShipperCapacitySettings`, va response identity/shipper.
- Them service Admin `SetShipperCapacityService` va UI `/admin/users` de Admin cap nhat trang thai nhan auto assign va max active shipments.
- Selector auto assign chi xet shipper active, con available, co working area match pickup hub/province, va chua vuot capacity active load.
- `/operations/assignments` hien trang thai available/capacity trong dropdown va uu tien shipper co the auto assign.
- Workspace shipper hien capacity/load active, trang thai available, va working areas/hubs hien tai.
- Tests cover active load statuses, skip shipper unavailable/at capacity, no eligible khi full capacity, COD collection sau auto assign, va admin update capacity.

## P6 - Tests, Docs, Cleanup

- [ ] Unit tests cho working area validation.
- [ ] Unit tests cho selector:
  - match province.
  - uu tien load thap.
  - khong co shipper phu hop.
  - inactive shipper bi loai.
- [ ] Application tests cho auto assign khi tao shipment tu shop UI service.
- [ ] Application tests cho Partner API create shipment + auto assign + idempotency replay.
- [ ] Tests cho manual override van hoat dong.
- [ ] Cap nhat docs Partner API: status co the la `Assigned` ngay sau create neu auto assign thanh cong.
- [ ] Cap nhat README/technical notes ve mo hinh hybrid assignment.
- [ ] Xoa service/response/UI state thua neu bi thay the.
- [ ] Chay test suite va fix warning/build error.

## Artefacts Lien Quan

- Domain shipment: `src/MiniLogistics.Domain/Shipments/Shipment.cs`
- Manual assign service: `src/MiniLogistics.Application/Shipments/AssignShipperToShipment/AssignShipperToShipmentService.cs`
- Shop create shipment: `src/MiniLogistics.Application/Shipments/CreateShipment/CreateShipmentService.cs`
- Partner create shipment: `src/MiniLogistics.Application/PartnerApi/PartnerCreateShipmentService.cs`
- Active shippers: `src/MiniLogistics.Application/Shippers/GetActiveShippers`
- Operations UI: `src/MiniLogistics.Web/Components/Pages/OperationsAssignments.razor`
- Identity user: `src/MiniLogistics.Infrastructure/Identity/ApplicationUser.cs`
- Partner docs da hoan thanh: `docs/partner-api.md`, `docs/third-party-shipment-integration-guide.md`

## Ghi Chu Da Hoan Thanh Truoc Do

- Partner API Core: Done.
- Tracking/Cancel Partner API: Done.
- Webhook delivery/retry: Done.
- UI Partner integrations: Done.
- Rate limit, audit, docs/OpenAPI/Postman, contract tests: Done.
