# Admin Role

Tai lieu nay mo ta role `Admin` hien co trong MiniLogistics de developer nam duoc luong xu ly, tinh nang da co va cac diem can can nhac khi production hoa.

## Pham vi role

`Admin` la role noi bo co quyen cao nhat trong he thong hien tai. Admin khong tao shipment thay shop trong web UI, nhung co the quan tri nguoi dung noi bo, cau hinh shipper cho auto assignment, quan ly shop/hub, dieu phoi don giong Operator, xu ly COD settlement/reporting, xem audit log, theo doi control tower va quan ly partner integrations theo scope.

Role enum: `src/MiniLogistics.Domain/Users/UserRole.cs`.

## Entry points

| Entry point | File | Ghi chu |
| --- | --- | --- |
| `/admin`, `/admin/dashboard` | `src/MiniLogistics.Web/Components/Pages/AdminDashboard.razor` | Chi `Admin`. Control tower van hanh trong ngay, metric server-side va drill-down. |
| `/admin/users` | `src/MiniLogistics.Web/Components/Pages/AdminUsers.razor` | Chi `Admin`. Quan ly Operator/Shipper, working areas, capacity. |
| `/admin/shops` | `src/MiniLogistics.Web/Components/Pages/AdminShops.razor` | Chi `Admin`. Xem shop va active/deactive shop. |
| `/admin/hubs` | `src/MiniLogistics.Web/Components/Pages/AdminHubs.razor` | Chi `Admin`. Quan ly hub production config. |
| `/admin/cod` | `src/MiniLogistics.Web/Components/Pages/AdminCod.razor` | Chi `Admin`. COD settlement/reporting, filter va CSV export. |
| `/admin/audit-logs` | `src/MiniLogistics.Web/Components/Pages/AdminAuditLogs.razor` | Chi `Admin`. Tra cuu Admin Audit Log. |
| `/admin/system-config` | `src/MiniLogistics.Web/Components/Pages/AdminSystemConfig.razor` | Chi `Admin`. Quan ly route region config va fee rule versions. |
| `/operations/assignments` | `src/MiniLogistics.Web/Components/Pages/OperationsAssignments.razor` | `Admin,Operator`. Dieu phoi, retry auto assign, manual override, update status, COD support. |
| `/partner/integrations` | `src/MiniLogistics.Web/Components/Pages/PartnerIntegrations.razor` | `Shop,Admin,IntegrationAdmin`. Quan ly API client/webhook theo shop/scope va xem webhook monitoring. |
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

`GetAdminUsersService` query danh sach identity users + roles theo filter server-side, pagination, sau do enrich them working areas cho user co role `Shipper`.

Du lieu hien thi gom:

- `FullName`, `Email`, `PhoneNumber`
- `IsActive`
- `IsAvailableForAssignment`
- `MaxActiveShipments`
- roles
- active working areas + hub metadata
- server-side search/status/role filter va CSV export tu UI

Files lien quan:

- `src/MiniLogistics.Application/AdminUsers/GetAdminUsers`
- `src/MiniLogistics.Application/Shippers/ShipperWorkingAreaResponseMapper.cs`
- `src/MiniLogistics.Domain/Operations/Hub.cs`
- `src/MiniLogistics.Domain/Operations/ShipperWorkingArea.cs`

### Active/deactive Operator va Shipper

Admin bat/tat active status bang `SetUserActiveStatusService`, bat buoc nhap reason khi deactivate tu UI.

Rule hien tai:

- Chi active Admin duoc thao tac.
- Target phai ton tai.
- Chi target role `Shipper` hoac `Operator` duoc active/deactive qua service nay.
- Deactive shipper lam shipper bien mat khoi danh sach active shippers va khong the manual assign.
- Deactivate reason duoc ghi vao Admin Audit Log.

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
- Thay doi working areas duoc ghi Admin Audit Log.

Files lien quan:

- `src/MiniLogistics.Application/Shippers/SetShipperWorkingAreas`
- `src/MiniLogistics.Application/Shippers/GetShipperWorkingAreas`
- `src/MiniLogistics.Application/Shippers/IShipperWorkingAreaRepository.cs`

### Cau hinh capacity shipper

Admin cau hinh:

- `IsAvailableForAssignment`
- `MaxActiveShipments`

Auto assignment selector chi xet shipper active, available va chua vuot `MaxActiveShipments`.

Thay doi capacity duoc ghi Admin Audit Log.

Files lien quan:

- `src/MiniLogistics.Application/AdminUsers/SetShipperCapacity`
- `src/MiniLogistics.Application/Shipments/AssignmentSelection/ShipmentAssignmentSelector.cs`
- `src/MiniLogistics.Application/Shipments/ShipmentLoadStatuses.cs`

### Quan ly shop

Admin xem va active/deactive shop qua `/admin/shops`.

Rule hien tai:

- Chi active Admin duoc active/deactive shop.
- Admin khong sua profile/thong tin kinh doanh thay Shop trong UI hien tai.
- Thay doi active status ghi Admin Audit Log.

Files lien quan:

- `src/MiniLogistics.Application/Shops/SetShopActiveStatus`
- `src/MiniLogistics.Web/Components/Pages/AdminShops.razor`

### Quan ly hub

Admin quan ly hub qua `/admin/hubs`.

Admin co the:

- search/filter hub theo keyword, province, active status, sorting hub.
- tao va sua hub: code, name, province, address, sorting hub flag, active status.
- activate/deactivate hub voi reason.

Rule hien tai:

- Chi active Admin duoc tao/sua/activate/deactivate hub.
- Hub code unique va duoc normalize.
- Deactivate hub khong xoa hay sua working areas lich su.
- UI canh bao khi hub dang duoc gan cho working areas active.
- Set working area chi chon active hub.
- Moi action create/update/deactivate ghi Admin Audit Log.

Files lien quan:

- `src/MiniLogistics.Application/AdminHubs`
- `src/MiniLogistics.Domain/Operations/Hub.cs`
- `src/MiniLogistics.Infrastructure/Persistence/Repositories/HubRepository.cs`
- `src/MiniLogistics.Web/Components/Pages/AdminHubs.razor`

### Admin dashboard / control tower

Admin xem tong quan van hanh qua `/admin` hoac `/admin/dashboard`.

Metrics hien tai:

- shipments tao trong date range, khong tinh Draft.
- pending pickup chua assign.
- active/available shippers va shipper qua capacity.
- delivery failed count/rate.
- COD pending collection, collected waiting settlement, settled.
- shop active/inactive.
- webhook delivery failed/retry pending neu co du lieu.

Metric duoc tinh server-side, UI chi hien aggregate va drill-down sang operations, users, shops, partner integrations va COD.

Files lien quan:

- `src/MiniLogistics.Application/AdminDashboard`
- `src/MiniLogistics.Infrastructure/Persistence/Repositories/AdminDashboardMetricsRepository.cs`
- `src/MiniLogistics.Web/Components/Pages/AdminDashboard.razor`

### Dieu phoi va van hanh don hang

Admin dung cung operations workspace voi Operator.

Admin co the:

- xem don `PendingPickup` chua auto assign duoc voi filter/search server-side va pagination.
- retry auto assignment tung don hoac bulk retry batch gioi han.
- manual assign active shipper, ke ca khi khong match pickup area; UI canh bao mismatch.
- reassign/cancel assignment khi shipment con o `Assigned`.
- theo doi don dang van hanh trong cac status `Assigned`, `PickingUp`, `PickedUp`, `InTransit`, `Delivering`, `DeliveryFailed`, va `Delivered` neu COD con pending.
- update shipment status theo lifecycle rule.
- confirm COD collected fallback.
- settle COD collected.
- xem SLA indicators cho pending pickup qua nguong, delivery failed lap lai va COD pending collection qua nguong.

Files lien quan:

- `src/MiniLogistics.Application/Shipments/GetPendingPickupShipments`
- `src/MiniLogistics.Application/Shipments/GetOperationsShipments`
- `src/MiniLogistics.Application/Shipments/AssignShipperToShipment`
- `src/MiniLogistics.Application/Shipments/ReassignShipment`
- `src/MiniLogistics.Application/Shipments/CancelShipmentAssignment`
- `src/MiniLogistics.Application/Shipments/BulkRetryAutoAssignment`
- `src/MiniLogistics.Application/Shipments/UpdateShipmentStatus`
- `src/MiniLogistics.Application/CashOnDelivery/MarkCodCollected`
- `src/MiniLogistics.Application/CashOnDelivery/MarkCodSettled`

### COD settlement va reporting

Admin xu ly COD rieng qua `/admin/cod`.

Module hien tai co:

- Pending collection: delivered nhung COD chua collected.
- Collected awaiting settlement: da thu tien, chua settle.
- Settled history.
- Filter theo shipper, province, date range, amount range, status.
- Settle tung transaction voi note/reason.
- Aggregate report theo shipper/hub/ngay va CSV export.

Rule hien tai:

- Delivered + COD PendingCollection giu active assignment den khi MarkCodCollected thanh cong.
- Chi active Admin duoc settle.
- Settlement action ghi Admin Audit Log.

Files lien quan:

- `src/MiniLogistics.Application/AdminCod`
- `src/MiniLogistics.Application/CashOnDelivery`
- `src/MiniLogistics.Infrastructure/Persistence/Repositories/AdminCodReportRepository.cs`
- `src/MiniLogistics.Web/Components/Pages/AdminCod.razor`

### Partner integrations cua moi shop

Admin hoac IntegrationAdmin truy cap `/partner/integrations` va quan ly API clients/webhook endpoints theo shop duoc phep.

`PartnerIntegrationManagementService.GetAccessibleShopsAsync` giu backward-compatible cho Admin demo: neu chua cau hinh granular scope active thi active Admin van xem duoc tat ca shop. Khi co `IntegrationManagementScope`, Admin/IntegrationAdmin chi xem va thao tac shop match global/shop/province scope.

Admin/IntegrationAdmin co the:

- xem shops va API clients.
- tao API client.
- rotate key.
- activate/deactivate API client.
- upsert webhook endpoint.
- tao webhook test delivery.
- xem webhook monitoring: success rate, average latency, failed deliveries, retry queue.

Files lien quan:

- `src/MiniLogistics.Application/PartnerApi/PartnerIntegrationManagementService.cs`
- `src/MiniLogistics.Application/PartnerApi/IIntegrationManagementScopeRepository.cs`
- `src/MiniLogistics.Domain/PartnerApi`
- `src/MiniLogistics.Infrastructure/PartnerApi/WebhookDeliveryDispatcher.cs`

### Admin Audit Log

Admin xem audit log qua `/admin/audit-logs`.

Audit log ghi:

- actor user id/role, action, target type/id.
- old/new value JSON da sanitize.
- reason, ip address, user agent, created at UTC.

Audit duoc gan vao cac action nhay cam: user/shop/hub active status, create internal user, shipper capacity/working areas, assignment manual/retry/reassign/cancel, status update, COD collected/settled, partner API client/webhook, route config va fee rule version.

Rule bao mat:

- Khong log raw API key, Authorization header, webhook secret hoac PII qua muc can thiet.
- Audit write nam trong cung transaction voi state change khi action thay doi DB.

Files lien quan:

- `src/MiniLogistics.Domain/AdminAuditing`
- `src/MiniLogistics.Application/AdminAuditing`
- `src/MiniLogistics.Infrastructure/Persistence/AdminAuditService.cs`
- `src/MiniLogistics.Web/Components/Pages/AdminAuditLogs.razor`

### System configuration

Admin quan ly cau hinh he thong qua `/admin/system-config`.

Module hien tai:

- route region config theo province voi versioning.
- shipping fee rule version theo route type.
- fee parameters: base fee, extra weight fee, min/max weight, insurance threshold/max/rate, return fee rate.

Rule hien tai:

- Tao config version moi deactivate active version cu thay vi sua truc tiep.
- Thay doi config ghi Admin Audit Log.
- Shipment cu giu fee da tinh trong `ShippingFeeBreakdown`, bao gom `ReturnFeeRate`.
- `RouteClassificationService` lay config qua `IRouteRegionConfigSource` va co fallback default cho demo.

Files lien quan:

- `src/MiniLogistics.Application/AdminSystemConfiguration`
- `src/MiniLogistics.Application/Routing/IRouteRegionConfigSource.cs`
- `src/MiniLogistics.Domain/Operations/RouteRegionConfig.cs`
- `src/MiniLogistics.Domain/Fees/FeeRule.cs`
- `src/MiniLogistics.Web/Components/Pages/AdminSystemConfig.razor`

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
-> ghi Admin Audit Log
```

### Reassign / cancel assignment

```text
Admin/Operator login
-> /operations/assignments
-> chon shipment dang Assigned
-> Reassign sang shipper moi hoac Cancel assignment voi reason bat buoc
-> service validate status, active shipper va actor
-> Shipment dam bao chi mot active assignment
-> publish webhook status/assignment change neu shipment co external reference
-> ghi Admin Audit Log
```

### COD fallback va settlement

```text
Shipment Delivered + COD PendingCollection
-> Admin/Operator co the MarkCodCollected
-> CodTransaction.MarkCollected validate shipment Delivered va COD PendingCollection
-> Shipment.DeactivateActiveAssignments
-> Admin settle COD bang MarkCodSettled
-> settlement ghi Admin Audit Log
```

## Business rules can nho

- Admin active la dieu kien bat buoc cho admin-only services.
- Admin khong duoc tao Admin khac tu `CreateInternalUserService`.
- Manual assign chi duoc khi shipment dang `PendingPickup`.
- Reassign/cancel assignment chi duoc khi shipment dang `Assigned`.
- Bulk retry chi ap dung cho shipment `PendingPickup`, co batch limit va report tung don.
- Status terminal `Delivered`, `Returned`, `Cancelled` khong update tiep duoc.
- `DeliveryFailed` bat buoc co note.
- `Returned` tu dong ap return fee va deactivate assignment.
- `Delivered` chi deactivate assignment ngay neu COD amount = 0; neu COD pending thi giu assignment de shipper/Admin/Operator confirm.
- Admin/IntegrationAdmin quan ly partner integration theo scope neu granular scope da duoc cau hinh.
- Fee/routing config la versioned; khong sua fee da tinh cua shipment cu.

## Production planning notes

- Granular integration permission da co foundation theo global/shop/province scope; neu can shop group/region phuc tap hon thi mo rong `IntegrationManagementScope`.
- `SetUserActiveStatusService` hien chi cho Shipper/Operator; neu can lock Shop/Admin thi can service/policy rieng.
- Chua co approval flow cho shipper working area; Admin set truc tiep.
- In-memory partner API rate limiter phu hop demo, production can distributed rate limiter.
- Working area template trong P3 system config chua implement vi la nhu cau tuy chon; nen chot workflow van hanh truoc khi them.
