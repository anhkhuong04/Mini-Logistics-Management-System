# Shop Role

Tai lieu nay mo ta role `Shop` hien co trong MiniLogistics. Shop la nguoi tao don, quan ly shipment cua shop minh va cau hinh Partner API cho kenh e-commerce/ben thu ba.

## Pham vi role

`Shop` la external business user. Moi Shop account co mot `Shop` domain record gom owner user, ten shop, phone va dia chi lay hang mac dinh. Shop chi thay shipment thuoc shop cua minh.

Role enum: `src/MiniLogistics.Domain/Users/UserRole.cs`.

## Entry points

| Entry point | File | Ghi chu |
| --- | --- | --- |
| `/register-shop` | `src/MiniLogistics.Web/Components/Pages/RegisterShop.razor` | Dang ky Shop account va Shop domain record. |
| `/dashboard` | `src/MiniLogistics.Web/Components/Pages/Dashboard.razor` | Tong quan shipment cua shop. |
| `/shipments/create` | `src/MiniLogistics.Web/Components/Pages/CreateShipment.razor` | Tao shipment tu web UI. |
| `/shipments` | `src/MiniLogistics.Web/Components/Pages/Shipments.razor` | Danh sach shipment cua shop. |
| `/shipments/{id}` | `src/MiniLogistics.Web/Components/Pages/ShipmentDetail.razor` | Chi tiet shipment, tracking history, cancel khi hop le. |
| `/partner/integrations` | `src/MiniLogistics.Web/Components/Pages/PartnerIntegrations.razor` | Quan ly API clients/webhooks cua shop. |
| `/api/v1/partner/*` | `src/MiniLogistics.Web/Endpoints/PartnerApiEndpoints.cs` | Kenh backend e-commerce goi bang API key. |
| `/tracking` | `src/MiniLogistics.Web/Components/Pages/Tracking.razor` | Public tracking bang tracking code. |

## Tinh nang hien co

### Dang ky shop

`RegisterShopService` tao identity user, add role `Shop`, sau do tao `Shop` domain record.

Rule:

- Validate input bang FluentValidation.
- Tao identity user truoc.
- Add role `Shop`.
- Moi owner user chi co mot shop.
- Shop record luu phone va dia chi lay hang mac dinh.

Files lien quan:

- `src/MiniLogistics.Application/Shops/RegisterShop`
- `src/MiniLogistics.Domain/Shops/Shop.cs`
- `src/MiniLogistics.Infrastructure/Identity/IdentityService.cs`

### Xem dashboard va danh sach shipment

`GetShipmentsForCurrentShopService` lay shop theo `ownerUserId`, validate shop active, roi query shipments theo `ShopId`.

Dashboard/list item hien:

- tracking code
- receiver
- route type
- actual weight, chargeable weight
- COD amount
- shipping fee
- status
- created time

Files lien quan:

- `src/MiniLogistics.Application/Shipments/GetShipmentsForCurrentShop`
- `src/MiniLogistics.Web/Components/Pages/Dashboard.razor`
- `src/MiniLogistics.Web/Components/Pages/Shipments.razor`

### Tao shipment tu web UI

`CreateShipmentService` la luong tao don cho Shop UI.

Luong backend:

```text
Shop submit create shipment
-> validate command
-> lay Shop theo CreatedByUserId va check active
-> tao Weight/ParcelDimensions/Money value objects
-> RouteClassificationService classify pickup province -> delivery province
-> ShippingFeeService tinh fee theo route, weight, dimensions, declared goods value
-> generate unique TrackingCode
-> Shipment.Create status PendingPickup va status history "Shipment created."
-> CodTransaction.Create, COD = NotRequired neu amount = 0, nguoc lai PendingCollection
-> save shipment + COD
-> IAutoAssignShipmentService.AutoAssignAsync
-> response tra status cuoi cung: PendingPickup hoac Assigned
```

Auto assignment co the doi status sang `Assigned` ngay neu tim duoc shipper hop working area/capacity. Neu khong tim duoc, create shipment van thanh cong va status giu `PendingPickup`.

Files lien quan:

- `src/MiniLogistics.Application/Shipments/CreateShipment`
- `src/MiniLogistics.Application/Routing/RouteClassificationService.cs`
- `src/MiniLogistics.Application/Fees/ShippingFeeService.cs`
- `src/MiniLogistics.Application/Shipments/AutoAssignShipment`

### Xem chi tiet shipment

`GetShipmentDetailForCurrentShopService` enforce ownership:

- owner user id bat buoc.
- shipment id bat buoc.
- shop phai ton tai va active.
- shipment phai thuoc shop hien tai.

Response gom:

- sender/receiver
- pickup/delivery full address
- parcel dimensions, volumetric weight, chargeable weight
- goods value, COD amount
- fee breakdown
- route type
- note, status, created time
- tracking history voi actor display name/email neu tim duoc

Files lien quan:

- `src/MiniLogistics.Application/Shipments/GetShipmentDetailForCurrentShop`
- `src/MiniLogistics.Application/Shipments/ShipmentStatusHistoryMapper.cs`

### Cancel shipment

Shop co the cancel shipment cua shop minh qua `CancelShipmentForCurrentShopService`.

Rule:

- Shop phai ton tai va active.
- Shipment phai thuoc shop.
- Domain cho cancel khi status chua qua cac trang thai khong cho huy.
- Khong cancel duoc khi status la `PickedUp`, `InTransit`, `Delivering`, `Delivered`, `DeliveryFailed`, `Returned`, `Cancelled`.
- Cancel deactivates active assignments.
- Publish webhook `shipment.status_changed` neu shipment co external reference.

Files lien quan:

- `src/MiniLogistics.Application/Shipments/CancelShipmentForCurrentShop`
- `src/MiniLogistics.Domain/Shipments/Shipment.cs`

### Partner integrations UI

Shop quan ly API clients va webhook endpoint cua shop minh tai `/partner/integrations`.

Shop co the:

- tao API client va nhan API key mot lan.
- rotate key.
- activate/deactivate client.
- upsert webhook endpoint.
- tao webhook test delivery.
- xem recent webhook deliveries.

`PartnerIntegrationManagementService` chi tra shop cua current user neu current user la Shop active va shop active.

Files lien quan:

- `src/MiniLogistics.Application/PartnerApi/PartnerIntegrationManagementService.cs`
- `src/MiniLogistics.Web/Components/Pages/PartnerIntegrations.razor`

### Partner API

Partner API la kenh e-commerce/third-party tao shipment thay Shop bang API key.

Endpoints:

- `POST /api/v1/partner/shipping/quote`
- `POST /api/v1/partner/shipments`
- `GET /api/v1/partner/shipments/{trackingCode}`
- `POST /api/v1/partner/shipments/{trackingCode}/cancel`

Cross-cutting:

- Bearer API key auth bang `PartnerApiAuthenticationService`.
- API client inactive bi reject.
- Auth thanh cong se `MarkUsed`.
- In-memory rate limit theo API client: quote 60/min, create 30/min, tracking 120/min, cancel 30/min.
- Create shipment ghi audit row `PartnerApiRequestAudit`.
- Webhook deliveries duoc tao khi shipment created/status changed neu API client co active webhook endpoint.

Create Partner API:

- Bat buoc `Idempotency-Key`.
- Bat buoc external order id theo validator.
- Cung `Idempotency-Key` + cung request hash tra stored response snapshot.
- Cung `Idempotency-Key` + body khac tra conflict.
- Khac idempotency key nhung cung `externalOrderId` tra conflict.
- Response status co the `Assigned` ngay neu auto assignment thanh cong.

Files lien quan:

- `src/MiniLogistics.Web/Endpoints/PartnerApiEndpoints.cs`
- `src/MiniLogistics.Application/PartnerApi`
- `docs/partner-api.md`
- `docs/third-party-shipment-integration-guide.md`

## Business rules can nho

- Shop chi thao tac tren shipment thuoc shop cua minh.
- Shop khong duoc assign shipper.
- Shop khong duoc update shipment status sau khi tao.
- Shop cancel duoc truoc khi shipment di qua pickup/in-transit stages.
- Fee tinh theo route type + chargeable weight + insurance fee.
- Route classification support danh sach province trong `RouteClassificationService`; province ngoai danh sach se fail create/quote.
- COD transaction luon duoc tao; amount 0 -> `NotRequired`, amount > 0 -> `PendingCollection`.

## Production planning notes

- Can bo sung shop profile management: sua phone, pickup address, active status workflow.
- Chua co multi-shop per user.
- Chua co draft shipment/edit shipment truoc pickup.
- Chua co order import/bulk create trong UI.
- Partner API auth/rate limit can production hardening: key rotation audit, distributed rate limit, secret storage policy.
- Create shipment luu shipment va COD truoc, sau do auto assign; can xem lai transaction boundary neu production yeu cau atomic outbox/webhook.
- Public tracking khong co access control ngoai tracking code; can quyet dinh tracking privacy policy.
