# Partner E-commerce Integration Task Board

Muc tieu: bien he thong Mini Logistics tu web quan ly don noi bo thanh dich vu logistics co API cho website ban hang ben thu 3. Website e-commerce co the goi API de tinh phi, tao van don, tra tracking code ve checkout/order system, va nhan cap nhat trang thai qua webhook.

## 1. Vision Va Scope

Feature can dat duoc luong:

```text
Customer mua hang tren website e-commerce
-> e-commerce lay dia chi, hang hoa, COD
-> e-commerce goi Logistics Quote API de tinh phi
-> customer xac nhan dat hang/thanh toan
-> e-commerce goi Logistics Create Shipment API
-> Logistics tra tracking code va fee snapshot
-> Operator/Admin/Shipper xu ly don tren he thong hien tai
-> Logistics gui webhook cap nhat status ve e-commerce
```

Gia tri nghiep vu:

- Giam thao tac nhap tay don hang tu website ban hang vao logistics.
- Cho phep moi shop/doi tac tich hop truc tiep vao quy trinh checkout.
- Tao nen nen tang B2B: logistics-as-a-service cho nhieu website ban hang.
- Mo duong cho COD settlement, invoice, webhook, API key management sau nay.

Khong nam trong scope dau tien:

- OAuth consent screen.
- Marketplace app cho ben thu 3.
- SLA/rate card rieng tung merchant nang cao.
- In label/waybill PDF.
- Billing/invoice day du.
- Real-time carrier routing/hub optimization.

## 2. Actors

| Actor | Vai tro |
| --- | --- |
| Customer | Nguoi mua hang tren website e-commerce. Khong truc tiep dung Mini Logistics. |
| E-commerce Website | He thong ben thu 3 goi API logistics de quote/create/tracking. |
| Shop | Merchant/chu shop trong Mini Logistics. Moi API client phai gan voi mot shop. |
| Logistics System | He thong Mini Logistics hien tai, xu ly quote, shipment, tracking, COD. |
| Operator/Admin | Dieu phoi don duoc tao tu API nhu don tao tu UI. |
| Shipper | Nhan va cap nhat don nhu luong hien tai. |

## 3. Nguyen Tac Thiet Ke

- Website ben thu 3 khong login bang cookie/session nhu user web.
- Ben thu 3 duoc xem la `ApiClient` duoc cap quyen thay mat mot `Shop`.
- API khong cho client truyen tuy y `CreatedByUserId`.
- Backend phai tinh lai phi va validate lai tat ca input, khong tin fee tu client.
- Tao don phai idempotent de tranh double shipment khi e-commerce retry.
- Trang thai shipment nen dong bo ve e-commerce bang webhook, khong bat buoc e-commerce poll lien tuc.
- Moi request API nen co audit trail: ai goi, shop nao, external order nao, request id nao.

## 4. Module De Xuat

Co the trien khai ban dau ngay trong `MiniLogistics.Web` bang Minimal API hoac Controller:

```text
src/MiniLogistics.Web
  PartnerApi/
    PartnerApiEndpoints.cs
    PartnerApiAuthentication.cs
    Dtos/
```

Sau khi feature lon hon, co the tach rieng:

```text
src/MiniLogistics.Api
  Public Partner API

src/MiniLogistics.Web
  Blazor UI cho shop/operator/admin/shipper
```

Chot P0: trien khai phase dau trong `MiniLogistics.Web` bang Minimal API endpoints.

Ly do:

- Repo hien tai da co Blazor Web App, DI, auth, EF DbContext va endpoint mapping trong `Program.cs`.
- Quote/create API can reuse truc tiep application services hien co.
- Tach `MiniLogistics.Api` ngay luc nay se tang wiring, deployment va test surface trong khi core API chua on dinh.
- Van tach folder/namespace `PartnerApi` ro rang de sau nay move sang project API rieng neu can.

## 5. Data Model Can Bo Sung

## 5.1. ApiClient

Muc dich: dai dien cho mot website/app ben thu 3 duoc phep goi API.

Fields de xuat:

```text
ApiClient
- Id: Guid
- ShopId: Guid
- Name: string
- ApiKeyPrefix: string
- ApiKeyHash: string
- SecretHash or EncryptedSecret: string
- IsActive: bool
- CreatedAtUtc: DateTimeOffset
- UpdatedAtUtc: DateTimeOffset?
- LastUsedAtUtc: DateTimeOffset?
```

Ghi chu:

- Khong luu raw API key.
- Khi tao key, chi hien raw secret mot lan cho shop/admin copy.
- `ApiKeyPrefix` giup debug/log ma khong lo key day du.

## 5.2. ExternalShipmentReference

Muc dich: mapping don hang ben e-commerce voi shipment trong Mini Logistics.

```text
ExternalShipmentReference
- Id: Guid
- ApiClientId: Guid
- ShopId: Guid
- ShipmentId: Guid
- ExternalOrderId: string
- IdempotencyKey: string
- RequestHash: string
- ResponseSnapshotJson: string
- CreatedAtUtc: DateTimeOffset
```

Unique constraints:

```text
(ApiClientId, IdempotencyKey)
(ApiClientId, ExternalOrderId)
```

Chot P0: phase 1 ap dung **1 external order = 1 shipment**.

Ly do:

- Phu hop luong checkout MVP: mot don e-commerce tao mot van don logistics.
- Giam rui ro tao trung van don va don gian hoa idempotency.
- Neu sau nay can split shipment, them `ExternalPackageNo` hoac `ShipmentSequence` va doi unique key sang `(ApiClientId, ExternalOrderId, ShipmentSequence)`.

## 5.3. WebhookEndpoint

Muc dich: URL ma logistics se goi de bao status/event cho e-commerce.

```text
WebhookEndpoint
- Id: Guid
- ApiClientId: Guid
- Url: string
- SecretHash or EncryptedSecret: string
- IsActive: bool
- CreatedAtUtc: DateTimeOffset
- UpdatedAtUtc: DateTimeOffset?
```

## 5.4. WebhookDelivery

Muc dich: theo doi lich su gui webhook va retry.

```text
WebhookDelivery
- Id: Guid
- WebhookEndpointId: Guid
- EventType: string
- AggregateId: Guid
- PayloadJson: string
- Status: Pending | Succeeded | Failed
- RetryCount: int
- NextAttemptAtUtc: DateTimeOffset?
- LastAttemptAtUtc: DateTimeOffset?
- LastResponseStatusCode: int?
- LastError: string?
- CreatedAtUtc: DateTimeOffset
```

## 6. API Contracts De Xuat

Base path:

```text
/api/v1/partner
```

Authentication:

```http
Authorization: Bearer {api_key}
```

Chot P0: phase 1 dung `Authorization: Bearer {api_key}` tren HTTPS, luu API key dang hash trong DB.

Ly do:

- Du don gian de e-commerce tich hop nhanh.
- Phu hop MVP va de test bang curl/Postman.
- Secret khong bi luu plain text neu hash dung cach.

Khong lam HMAC request signing cho inbound API trong phase 1. Phase sau co the bo sung neu can chong replay/man-in-the-middle nang cao:

```http
X-Api-Key: {api_key}
X-Signature: hmac-sha256(...)
X-Timestamp: 2026-06-29T10:30:00Z
```

## 6.1. Quote Shipping Fee

Endpoint:

```http
POST /api/v1/partner/shipping/quote
Authorization: Bearer {api_key}
```

Request:

```json
{
  "externalOrderId": "ECOM-10001",
  "pickupAddress": {
    "street": "123 Nguyen Trai",
    "ward": "Ben Thanh",
    "province": "Ho Chi Minh",
    "country": "Vietnam"
  },
  "deliveryAddress": {
    "street": "9 Le Loi",
    "ward": "Hoan Kiem",
    "province": "Ha Noi",
    "country": "Vietnam"
  },
  "parcel": {
    "weightKg": 1.2,
    "lengthCm": 20,
    "widthCm": 15,
    "heightCm": 10
  },
  "goodsValueAmount": 2000000,
  "codAmount": 150000,
  "currency": "VND"
}
```

Response:

```json
{
  "routeType": "InterRegion",
  "actualWeightKg": 1.2,
  "volumetricWeightKg": 0.6,
  "chargeableWeightKg": 1.2,
  "baseFeeAmount": 35000,
  "extraWeightFeeAmount": 8000,
  "insuranceFeeAmount": 10000,
  "returnFeeAmount": 0,
  "totalFeeAmount": 53000,
  "currency": "VND"
}
```

Business rules:

- Chi tinh phi, khong tao shipment.
- Pickup address co the lay default tu shop neu API khong truyen pickup.
- Delivery address bat buoc.
- Weight/dimensions/goods value/COD validate nhu UI tao don.
- Backend su dung `RouteClassificationService` va `ShippingFeeService` hien co.

## 6.2. Create Shipment

Endpoint:

```http
POST /api/v1/partner/shipments
Authorization: Bearer {api_key}
Idempotency-Key: ECOM-10001
```

Request:

```json
{
  "externalOrderId": "ECOM-10001",
  "sender": {
    "name": "Demo Mini Shop",
    "phone": "0900000001"
  },
  "receiver": {
    "name": "Nguyen Van A",
    "phone": "0911111111"
  },
  "pickupAddress": {
    "street": "123 Nguyen Trai",
    "ward": "Ben Thanh",
    "province": "Ho Chi Minh",
    "country": "Vietnam"
  },
  "deliveryAddress": {
    "street": "9 Le Loi",
    "ward": "Hoan Kiem",
    "province": "Ha Noi",
    "country": "Vietnam"
  },
  "parcel": {
    "weightKg": 1.2,
    "lengthCm": 20,
    "widthCm": 15,
    "heightCm": 10
  },
  "goodsValueAmount": 2000000,
  "codAmount": 150000,
  "currency": "VND",
  "note": "Hang de vo, giao gio hanh chinh"
}
```

Response:

```json
{
  "shipmentId": "00000000-0000-0000-0000-000000000000",
  "externalOrderId": "ECOM-10001",
  "trackingCode": "ML202606290001",
  "status": "PendingPickup",
  "routeType": "InterRegion",
  "shippingFeeAmount": 53000,
  "currency": "VND",
  "createdAtUtc": "2026-06-29T10:30:00Z"
}
```

Business rules:

- API auth xac dinh `ShopId`.
- Sender/pickup co the mac dinh tu shop profile neu request khong truyen.
- Tao shipment bang application service hien co, nhung can adapter command moi khong cho client truyen `CreatedByUserId`.
- Sau khi tao shipment, luu `ExternalShipmentReference`.
- Neu retry cung `Idempotency-Key`, tra lai response cu.

## 6.3. Get Shipment By Tracking Code

Endpoint:

```http
GET /api/v1/partner/shipments/{trackingCode}
Authorization: Bearer {api_key}
```

Response:

```json
{
  "trackingCode": "ML202606290001",
  "externalOrderId": "ECOM-10001",
  "status": "InTransit",
  "codStatus": "PendingCollection",
  "shippingFeeAmount": 53000,
  "currency": "VND",
  "timeline": [
    {
      "status": "PendingPickup",
      "note": "Shipment created.",
      "changedAtUtc": "2026-06-29T10:30:00Z"
    }
  ]
}
```

Business rules:

- API client chi xem duoc shipment thuoc shop cua minh.
- Khong tra du lieu noi bo nhu shipper phone/email neu khong can.

## 6.4. Cancel Shipment

Endpoint:

```http
POST /api/v1/partner/shipments/{trackingCode}/cancel
Authorization: Bearer {api_key}
```

Request:

```json
{
  "reason": "Customer cancelled order"
}
```

Business rules:

- Reuse cancel rule hien tai: chi cancel khi shipment chua qua cac trang thai cam.
- API client chi cancel shipment cua shop minh.

## 7. Idempotency

Ly do bat buoc:

- E-commerce co the retry khi timeout.
- Network failure co the xay ra sau khi logistics da tao shipment nhung response chua ve client.
- Khong co idempotency se tao trung van don.

Rule de xuat:

```text
Same ApiClientId + same Idempotency-Key:
- neu request hash giong: tra lai response snapshot cu
- neu request hash khac: tra 409 Conflict
```

Request hash nen tinh tu normalized JSON body, loai bo field khong anh huong nghiep vu neu can.

Status code:

| Case | Response |
| --- | --- |
| Tao moi thanh cong | 201 Created |
| Retry cung request | 200 OK voi response cu |
| Cung key nhung body khac | 409 Conflict |
| Thieu idempotency key khi create | 400 Bad Request |

## 8. Webhook

## 8.1. Event Can Gui

Chot P0: webhook khong nam trong P1. Chi trien khai webhook o P3 sau khi Partner API quote/create/tracking/cancel da on dinh.

P3 initial events:

- `shipment.created`
- `shipment.status_changed`

P3 extended events:

- `cod.collected`
- `cod.settled`
- `shipment.cancelled`
- `shipment.returned`

Payload mau:

```json
{
  "eventId": "00000000-0000-0000-0000-000000000000",
  "event": "shipment.status_changed",
  "trackingCode": "ML202606290001",
  "externalOrderId": "ECOM-10001",
  "status": "Delivered",
  "changedAtUtc": "2026-06-29T10:30:00Z"
}
```

Headers:

```http
X-MiniLogistics-Event: shipment.status_changed
X-MiniLogistics-Signature: sha256={hmac}
X-MiniLogistics-Timestamp: 2026-06-29T10:30:00Z
```

Signature:

```text
hmac = HMACSHA256(webhook_secret, timestamp + "." + raw_body)
```

## 8.2. Retry Policy

De xuat:

- Retry toi da 5 lan.
- Backoff: 1m, 5m, 15m, 1h, 6h.
- HTTP 2xx la success.
- HTTP 4xx/5xx hoac timeout la failed attempt.
- Luu response status/error vao `WebhookDelivery`.

## 9. Bao Mat

Can co toi thieu:

- API key hash, khong luu raw key.
- Key co the revoke/rotate.
- `ApiClient.IsActive` de khoa tich hop.
- Rate limit theo ApiClient.
- Audit log moi request quan trong.
- API chi thao tac tren shop duoc gan voi client.
- Webhook co HMAC signature.
- Khong log raw secret, Authorization header, phone/email day du neu khong can.

Rate limit goi y phase 1:

```text
Quote: 60 requests/min/client
Create shipment: 30 requests/min/client
Tracking: 120 requests/min/client
```

## 10. Application Service De Xuat

De tranh nhung API logic vao endpoint, them cac service:

```text
MiniLogistics.Application.PartnerApi
- IPartnerAuthenticationService
- IPartnerQuoteService
- IPartnerCreateShipmentService
- IPartnerShipmentQueryService
- IIdempotencyService
- IWebhookEventPublisher
```

DTO/command de xuat:

```text
PartnerQuoteCommand
PartnerCreateShipmentCommand
PartnerShipmentResponse
PartnerShippingQuoteResponse
```

`PartnerCreateShipmentService` nen reuse logic cua `CreateShipmentService` hoac tach core create shipment logic dung chung.

Can can nhac refactor:

- Hien `CreateShipmentCommand` bat buoc `CreatedByUserId`.
- Partner API khong co human user.
- Co the them `CreatedByUserId` la system/API user cua shop, hoac them field `CreatedByActorType`.

Lua chon pragmatic phase 1:

- Dung `Shop.OwnerUserId` lam `CreatedByUserId` khi goi core create shipment service.
- Luu dau vet API client trong `ExternalShipmentReference`.
- Khong refactor shipment actor model trong phase 1.

Ly do:

- `Shipment` va timeline hien yeu cau `changedByUserId` la user id dang ton tai.
- Dung owner user id giu timeline va mapper hien tai chay on dinh.
- `ExternalShipmentReference` van cho biet shipment duoc tao qua API client nao.

Lua chon sach hon phase 2:

```text
ShipmentCreatedBy
- ActorType: User | ApiClient
- ActorId: Guid
```

## 11. Database Migration

Migration P1 can co:

- `ApiClients`
- `ExternalShipmentReferences`

Migration P3 can co:

- `WebhookEndpoints`
- `WebhookDeliveries`

Indexes P1:

```text
IX_ApiClients_ApiKeyHash
IX_ApiClients_ShopId_IsActive
UX_ExternalShipmentReferences_ApiClientId_IdempotencyKey
IX_ExternalShipmentReferences_ShipmentId
```

Indexes P3:

```text
IX_WebhookDeliveries_Status_NextAttemptAtUtc
```

## 12. UI Quan Tri API Client

Phase 1 co the tao seed/manual API client bang DB/script.

Phase 2 nen co UI:

Route de xuat:

```text
/shop/integrations
/admin/integrations
```

Chuc nang:

- Tao API key cho shop.
- Xem prefix va trang thai key.
- Revoke key.
- Rotate key.
- Cau hinh webhook URL.
- Gui test webhook.
- Xem webhook delivery history.

## 13. Implementation Plan

## P0 - Design Chot Truoc Khi Code

- [x] API nam trong `MiniLogistics.Web` phase 1, dung Minimal API endpoint group `/api/v1/partner`.
- [x] Auth phase 1 dung Bearer API key tren HTTPS; API key luu hash, khong lam inbound HMAC signing luc dau.
- [x] Actor tao don phase 1 dung `Shop.OwnerUserId` lam `CreatedByUserId`; API client audit luu trong `ExternalShipmentReference`.
- [x] Unique rule phase 1: 1 external order = 1 shipment, enforce `(ApiClientId, ExternalOrderId)` va `(ApiClientId, IdempotencyKey)`.
- [x] Webhook khong lam trong P1; dua sang P3 sau khi quote/create/tracking/cancel on dinh.

## P1 - Partner API Core

- [x] Them domain/entity `ApiClient`.
- [x] Them entity `ExternalShipmentReference`.
- [x] Them EF configuration va migration.
- [x] Them repository/query cho `ApiClient`.
- [x] Them `PartnerApiAuthenticationService`.
- [x] Them `PartnerQuoteService`.
- [x] Them `PartnerCreateShipmentService`.
- [x] Them endpoint `POST /api/v1/partner/shipping/quote`.
- [x] Them endpoint `POST /api/v1/partner/shipments`.
- [x] Them idempotency handling cho create shipment.
- [x] Them seed/test API client cho demo.

P1 implementation notes:

- Endpoint da map trong `MiniLogistics.Web` tai `/api/v1/partner`.
- Create shipment tra `201 Created` khi tao moi va `200 OK` khi retry cung `Idempotency-Key` + cung request body.
- Demo API key duoc seed: `ml_demo_partner_key_123456`; DB chi luu `ApiKeyHash` va `ApiKeyPrefix`.
- Automated tests da cover application service va infrastructure seed/migration. HTTP endpoint contract tests chua them Web test project rieng.
- Manual HTTP smoke test ngay 2026-06-29 da pass: quote `200`, create `201`, retry `200`, missing auth `401`, idempotency conflict `409`.

## P2 - Tracking Va Cancel API

- [x] Them endpoint `GET /api/v1/partner/shipments/{trackingCode}`.
- [x] Them endpoint `POST /api/v1/partner/shipments/{trackingCode}/cancel`.
- [x] Enforce API client chi truy cap shipment cua shop minh.
- [x] Chuan hoa error response cho API client.

P2 implementation notes:

- Tracking endpoint tra `trackingCode`, `externalOrderId`, `status`, `codStatus`, `shippingFeeAmount`, `currency`, va timeline rut gon.
- Cancel endpoint reuse domain rule `Shipment.Cancel(...)`, dung `Shop.OwnerUserId` lam actor theo quyet dinh P0.
- Boundary enforce theo `ShopId` tu API client va `ExternalShipmentReference.ApiClientId`; shipment khong thuoc shop/client se tra `404`.
- Error response dung chung shape `{ error: { code, message, traceId } }`; cancel khong hop le tra `409`.
- Manual HTTP smoke test ngay 2026-06-29 da pass: GET tracking `200`, cancel `200`, cancel lai `409`, tracking khong ton tai `404`.

## P3 - Webhook

- [ ] Them entity `WebhookEndpoint`.
- [ ] Them entity `WebhookDelivery`.
- [ ] Them webhook payload model.
- [ ] Tao event khi shipment status changed.
- [ ] Tao background delivery worker.
- [ ] Them HMAC signature.
- [ ] Them retry/backoff.
- [ ] Them log delivery result.

## P4 - UI Quan Tri Integration

- [ ] Them trang shop/admin tao API key.
- [ ] Them revoke/rotate key.
- [ ] Them cau hinh webhook URL.
- [ ] Them nut test webhook.
- [ ] Them bang lich su webhook deliveries.

## P5 - Hardening

- [ ] Rate limit theo API client.
- [ ] Audit log request tao shipment.
- [ ] API docs Markdown/OpenAPI.
- [ ] Example curl/Postman collection.
- [ ] Contract tests cho API response.
- [ ] Security review: secret storage, logging, authorization boundaries.

## 14. Test Plan

## Unit/Application Tests

- [x] Quote API/service tinh dung fee theo route/weight/dimensions/goods value.
- [x] Create shipment bang API client active tao shipment thanh cong.
- [x] API client inactive bi tu choi.
- [x] API client A khong xem/cancel duoc shipment cua shop B.
- [x] Idempotency retry cung body tra response cu.
- [x] Idempotency cung key khac body tra 409.
- [x] Create shipment tao `ExternalShipmentReference`.
- [x] Cancel API reuse dung lifecycle cancel hien tai.

## Integration Tests

- [x] EF migration tao du bang/index.
- [x] Seed/demo API client dung duoc.
- [ ] HTTP quote endpoint tra 200 voi API key hop le.
- [ ] HTTP create endpoint tra 201 va tao shipment/COD/reference.
- [ ] HTTP create retry tra response cu, khong tao shipment thu 2.
- [ ] Unauthorized/missing API key tra 401.
- [ ] Forbidden inactive client tra 403.

## Webhook Tests

- [ ] Status changed tao `WebhookDelivery`.
- [ ] Delivery success khi endpoint tra 2xx.
- [ ] Delivery retry khi endpoint timeout/5xx.
- [ ] Signature dung format va verify duoc.

## 15. Error Response Chuan

De xuat response loi:

```json
{
  "error": {
    "code": "ValidationFailed",
    "message": "Delivery province is required.",
    "traceId": "00-..."
  }
}
```

Mapping:

| Case | HTTP |
| --- | ---: |
| Missing/invalid API key | 401 |
| API client inactive | 403 |
| Validation failed | 400 |
| Idempotency conflict | 409 |
| Shipment not found | 404 |
| Business rule conflict | 409 |
| Unexpected error | 500 |

## 16. Acceptance Criteria

Feature duoc xem la pass phase 1 khi:

- Website ben thu 3 co API key co the goi quote va create shipment.
- Create shipment tao ra shipment trong he thong hien tai voi fee breakdown, COD transaction va tracking code.
- Operator/Admin thay don do trong luong dieu phoi hien tai.
- Shipper co the xu ly don nhu don tao tu UI.
- E-commerce retry create voi cung idempotency key khong tao trung don.
- API client khong truy cap duoc don cua shop khac.
- Co test tu dong cover quote/create/idempotency/auth boundary.

Phase 2 pass khi:

- E-commerce co the tracking/cancel qua API.

Phase 3 pass khi:

- Logistics gui webhook status changed co signature.
- Webhook co retry va delivery history.

## 17. Rui Ro Va Quyet Dinh Da Chot

Rui ro:

- Neu khong lam idempotency, duplicate shipment rat de xay ra.
- Neu dung shop owner user id lam actor tao don, audit timeline co the khong phan biet user tao bang UI hay API.
- Neu API key luu plain text, nguy co lo key cao.
- Neu khong enforce shop boundary, partner co the doc/cancel shipment cua shop khac.
- Neu webhook khong signature, e-commerce khong verify duoc nguon event.

Quyet dinh da chot truoc khi implement:

- Actor model da chot phase 1: dung `Shop.OwnerUserId`, trace API client qua `ExternalShipmentReference`.
- Webhook da chot phase 1: chua lam trong P1, thuc hien o P3.
- External order da chot phase 1: 1 external order = 1 shipment.
- API auth da chot phase 1: Bearer API key, hash at rest, HTTPS required; inbound HMAC signing de sau.

## 18. De Xuat Thu Tu Lam Thuc Te

Thu tu toi uu cho repo hien tai:

1. Them `ApiClient` + `ExternalShipmentReference`.
2. Them API key auth.
3. Them quote endpoint.
4. Them create shipment endpoint + idempotency.
5. Them tests application/integration.
6. Them tracking/cancel endpoint.
7. Them webhook.
8. Them UI quan tri API key/webhook.

Ly do: quote/create la gia tri cot loi cho tich hop checkout. Tracking/cancel/webhook can thiet, nhung co the lam sau khi duong tao don on dinh.
