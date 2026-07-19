# Huong Dan Tich Hop Shipment Cho Website Ben Thu 3

Tai lieu nay danh cho website ban hang/e-commerce muon ket noi voi MiniLogistics de tinh phi giao hang, tao van don, theo doi trang thai va nhan webhook cap nhat shipment.

## 1. Tong Quan Mo Hinh

MiniLogistics dong vai tro la dich vu logistics cho website ban hang ben thu 3.

```text
Khach hang mua san pham tren website ban hang
-> website ban hang thu thap thong tin giao hang
-> website goi MiniLogistics Quote API de tinh phi ship
-> website hien phi ship cho khach hang
-> khach xac nhan dat hang
-> website goi MiniLogistics Create Shipment API
-> MiniLogistics tra tracking code va status ban dau
-> shop/operator/shipper xu ly don tren MiniLogistics
-> MiniLogistics gui webhook cap nhat trang thai ve website ban hang
```

Website ben thu 3 khong can dang nhap bang tai khoan web MiniLogistics. Moi website/app se duoc cap mot `API key` dai dien cho mot shop trong MiniLogistics.

## 2. Dieu Kien Dang Ky Su Dung

Truoc khi tich hop, doi tac can co:

- Mot tai khoan shop tren MiniLogistics.
- Thong tin shop mac dinh: ten shop, so dien thoai, dia chi lay hang.
- Mot API client duoc tao trong trang quan tri integration.
- Webhook URL neu muon nhan cap nhat trang thai tu dong.

### Cach Tao API Key

1. Dang nhap MiniLogistics bang tai khoan `Shop` hoac `Admin`.
2. Mo trang:

```text
/partner/integrations
```

3. Chon shop can tich hop.
4. Nhap ten ung dung, vi du:

```text
My Shopify Store
```

5. Bam `Tao API key`.
6. Copy API key vua tao va luu vao backend cua website ban hang.

Luu y bao mat:

- API key chi hien thi day du mot lan khi tao hoac rotate.
- Khong dua API key vao frontend/browser/mobile app public.
- Chi backend cua website ban hang duoc goi MiniLogistics Partner API.
- Neu nghi ngo lo key, dung chuc nang `Rotate` hoac `Revoke`.
- Webhook signing secret duoc bao ve trong database; MiniLogistics chi giai ma khi can ky HMAC webhook.

## 3. Base URL Va Authentication

Moi request Partner API dung base path:

```text
http://localhost:5221/api/v1/partner
```

Moi request can header:

```http
Authorization: Bearer {api_key}
```

Vi du:

```http
Authorization: Bearer <partner-api-key>
```

Moi API client chi co quyen thao tac voi shipment thuoc shop cua minh.

## 4. Luong Tich Hop Khuyen Nghi

### Buoc 1: Khach Nhap Thong Tin Giao Hang

Website ban hang thu thap:

- Ten nguoi nhan.
- So dien thoai nguoi nhan.
- Dia chi giao hang.
- Thong tin kien hang: can nang, dai/rong/cao.
- Gia tri hang hoa.
- So tien COD neu co.

### Buoc 2: Goi API Tinh Phi Ship

Khi khach nhap xong dia chi hoac truoc khi hien tong tien checkout, website goi:

```http
POST /api/v1/partner/shipping/quote
```

API nay chi tinh phi, chua tao van don.

### Buoc 3: Hien Thi Phi Ship Cho Khach

Website dung `totalFeeAmount` trong response de hien phi ship.

Tong tien goi y:

```text
order_total = product_total + shipping_fee
cod_amount = so tien can shipper thu ho neu don COD
```

### Buoc 4: Khach Xac Nhan Dat Hang

Sau khi order duoc tao tren website ban hang, website goi:

```http
POST /api/v1/partner/shipments
```

API nay tao van don that trong MiniLogistics va tra ve `trackingCode`.

### Buoc 5: Luu Mapping Don Hang

Website ban hang nen luu:

| Field | Muc dich |
| --- | --- |
| `externalOrderId` | Ma don hang tren website ban hang. |
| `trackingCode` | Ma van don MiniLogistics tra ve. |
| `shipmentId` | ID shipment trong MiniLogistics. |
| `shippingFeeAmount` | Phi ship da tinh/chot luc tao shipment. |
| `status` | Trang thai shipment gan nhat. |

Luu y: `status` sau khi tao shipment co the la `Assigned` ngay neu MiniLogistics tim duoc shipper phu hop voi khu vuc lay hang. Neu khong co shipper phu hop, status giu `PendingPickup` de Operations xu ly thu cong.

### Buoc 6: Theo Doi Trang Thai

Co hai cach:

- Polling: goi `GET /shipments/{trackingCode}` khi can.
- Webhook: MiniLogistics tu dong POST event ve webhook URL cua website.

Khuyen nghi dung webhook cho production.

MiniLogistics ghi webhook event vao outbox cung transaction voi shipment, sau do worker gui HTTP bat dong bo. Vi vay webhook co the tre vai giay, nhung event khong bi mat neu request tao/cap nhat shipment da commit.

## 5. Input Tinh Phi Ship

Endpoint:

```http
POST /api/v1/partner/shipping/quote
Authorization: Bearer {api_key}
Content-Type: application/json
```

Request body:

```json
{
  "externalOrderId": "ECOM-10001",
  "pickupAddress": null,
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

### Giai Thich Field

| Field | Bat buoc | Mo ta |
| --- | --- | --- |
| `externalOrderId` | Khong | Ma don hang ben website. Nen truyen de debug/log. |
| `pickupAddress` | Khong | Dia chi lay hang. Neu `null`, MiniLogistics dung dia chi shop mac dinh. |
| `deliveryAddress` | Co | Dia chi giao hang cua khach. |
| `parcel.weightKg` | Co | Can nang thuc te, don vi kg. |
| `parcel.lengthCm` | Co | Chieu dai kien hang, don vi cm. |
| `parcel.widthCm` | Co | Chieu rong kien hang, don vi cm. |
| `parcel.heightCm` | Co | Chieu cao kien hang, don vi cm. |
| `goodsValueAmount` | Co | Gia tri hang hoa de tinh phi bao hiem neu co. |
| `codAmount` | Co | So tien thu ho. Neu khong COD, gui `0`. |
| `currency` | Khong | Mac dinh `VND`. |

### Response Tinh Phi

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

Website nen hien `totalFeeAmount` cho khach hang. Cac field con lai dung de hien breakdown neu can.

### Curl Mau Tinh Phi

```bash
curl -X POST "http://localhost:5221/api/v1/partner/shipping/quote" \
  -H "Authorization: Bearer <partner-api-key>" \
  -H "Content-Type: application/json" \
  -d '{
    "externalOrderId": "ECOM-10001",
    "pickupAddress": null,
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
  }'
```

## 6. Tao Shipment

Endpoint:

```http
POST /api/v1/partner/shipments
Authorization: Bearer {api_key}
Idempotency-Key: {unique_key}
Content-Type: application/json
```

`Idempotency-Key` bat buoc cho create shipment. Nen dung ma order hoac payment transaction ID.

Request body:

```json
{
  "externalOrderId": "ECOM-10001",
  "sender": null,
  "receiver": {
    "name": "Nguyen Van A",
    "phone": "0911111111"
  },
  "pickupAddress": null,
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
  "note": "Giao gio hanh chinh"
}
```

### Field Tao Shipment

| Field | Bat buoc | Mo ta |
| --- | --- | --- |
| `externalOrderId` | Co | Ma don hang cua website ban hang. Moi API client chi duoc tao 1 shipment cho 1 `externalOrderId`. |
| `sender` | Khong | Nguoi gui. Neu `null`, dung thong tin shop. |
| `receiver.name` | Co | Ten nguoi nhan. |
| `receiver.phone` | Co | So dien thoai nguoi nhan. |
| `pickupAddress` | Khong | Dia chi lay hang. Neu `null`, dung dia chi shop. |
| `deliveryAddress` | Co | Dia chi giao hang. |
| `parcel` | Co | Can nang va kich thuoc kien hang. |
| `goodsValueAmount` | Co | Gia tri hang hoa. |
| `codAmount` | Co | So tien thu ho. Gui `0` neu khong COD. |
| `currency` | Khong | Mac dinh `VND`. |
| `note` | Khong | Ghi chu giao hang. |

### Response Tao Shipment

```json
{
  "shipmentId": "00000000-0000-0000-0000-000000000000",
  "externalOrderId": "ECOM-10001",
  "trackingCode": "ML202606290001",
  "status": "PendingPickup",
  "routeType": "InterRegion",
  "shippingFeeAmount": 53000,
  "currency": "VND",
  "createdAtUtc": "2026-06-29T10:30:00Z",
  "isIdempotentReplay": false
}
```

Website can luu `trackingCode` de hien cho khach va truy van trang thai.

`status` trong response tao shipment khong luon la `PendingPickup`. He thong se thu auto assign theo hub/khu vuc lay hang; neu thanh cong, response tra `Assigned` va retry cung `Idempotency-Key` se replay dung snapshot `Assigned`. Neu auto assign khong tim duoc shipper hop le, shipment van duoc tao thanh cong voi `PendingPickup`.

### Idempotency Khi Tao Shipment

Neu website retry vi timeout/network:

| Tinh huong | Ket qua |
| --- | --- |
| Cung `Idempotency-Key`, cung body | `200 OK`, tra lai shipment da tao, khong tao don moi. |
| Cung `Idempotency-Key`, body khac | `409 Conflict`. |
| Khac `Idempotency-Key`, cung `externalOrderId` | `409 Conflict` vi phase hien tai la 1 external order = 1 shipment. |

### Curl Mau Tao Shipment

```bash
curl -X POST "http://localhost:5221/api/v1/partner/shipments" \
  -H "Authorization: Bearer <partner-api-key>" \
  -H "Idempotency-Key: ECOM-10001" \
  -H "Content-Type: application/json" \
  -d '{
    "externalOrderId": "ECOM-10001",
    "sender": null,
    "receiver": {
      "name": "Nguyen Van A",
      "phone": "0911111111"
    },
    "pickupAddress": null,
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
    "note": "Giao gio hanh chinh"
  }'
```

## 7. Tracking Shipment

Endpoint:

```http
GET /api/v1/partner/shipments/{trackingCode}
Authorization: Bearer {api_key}
```

Response mau:

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

Chi API client tao shipment moi doc duoc shipment do.

## 8. Huy Shipment

Endpoint:

```http
POST /api/v1/partner/shipments/{trackingCode}/cancel
Authorization: Bearer {api_key}
Content-Type: application/json
```

Request:

```json
{
  "reason": "Customer cancelled order"
}
```

Huy shipment chi thanh cong khi shipment chua di qua cac trang thai khong cho phep huy. Neu khong the huy, API tra `409 Conflict`.

## 9. Webhook Cap Nhat Trang Thai

Webhook giup website ban hang nhan cap nhat tu dong thay vi polling lien tuc.

### Cau Hinh Webhook

1. Dang nhap MiniLogistics.
2. Mo `/partner/integrations`.
3. Chon API client.
4. Nhap webhook URL cua website ban hang, vi du:

```text
https://store.example.com/webhooks/minilogistics
```

5. Nhap signing secret.
6. Bam `Luu webhook`.
7. Bam `Test webhook` de kiem tra endpoint nhan event.

### Header Webhook

```http
X-MiniLogistics-Event: shipment.status_changed
X-MiniLogistics-Signature: sha256={hmac}
X-MiniLogistics-Timestamp: 2026-06-29T10:30:00Z
```

Signature duoc tinh theo:

```text
HMACSHA256(signing_secret, timestamp + "." + raw_body)
```

Website nen verify signature truoc khi xu ly event.

### Payload Mau

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

Supported events:

- `shipment.created`
- `shipment.status_changed`
- `webhook.test`

### Yeu Cau Endpoint Webhook

- Nhan `POST` JSON.
- Tra HTTP `2xx` neu xu ly thanh cong.
- Neu tra `4xx`, `5xx` hoac timeout, MiniLogistics se retry theo backoff.
- Xu ly duplicate event theo `eventId` de dam bao idempotency.

## 10. Error Response

Tat ca loi Partner API co dang:

```json
{
  "error": {
    "code": "Application.ValidationFailed",
    "message": "Delivery province is required.",
    "traceId": "0HN..."
  }
}
```

Mapping thuong gap:

| HTTP | Code | Y nghia |
| ---: | --- | --- |
| 400 | `Application.ValidationFailed` | Input khong hop le/thieu field. |
| 401 | `PartnerApi.MissingApiKey` | Thieu API key. |
| 401 | `PartnerApi.InvalidApiKey` | API key sai. |
| 403 | `PartnerApi.ApiClientInactive` | API client da bi revoke/inactive. |
| 403 | `PartnerApi.ShopInactive` | Shop dang inactive nen Partner API bi chan. |
| 404 | `Application.NotFound` | Khong tim thay shipment trong pham vi API client. |
| 409 | `PartnerApi.IdempotencyConflict` | Idempotency key bi dung lai voi body khac. |
| 409 | `Application.Conflict` | Conflict nghiep vu, vi du external order da ton tai. |
| 429 | `PartnerApi.RateLimitExceeded` | Vuot rate limit. |

## 11. Rate Limit

Rate limit hien tai theo moi API client trong cua so 1 phut:

| API | Limit |
| --- | ---: |
| Quote | 60 requests/min |
| Create shipment | 30 requests/min |
| Tracking | 120 requests/min |
| Cancel | 30 requests/min |

Khi vuot limit, API tra:

```http
429 Too Many Requests
Retry-After: 30
```

Website nen retry sau so giay trong header `Retry-After`.

## 12. Checklist Cho Ben Tich Hop

- [ ] Tao tai khoan shop tren MiniLogistics.
- [ ] Tao API client va luu API key vao backend.
- [ ] Goi Quote API trong checkout de hien phi ship.
- [ ] Goi Create Shipment API sau khi order duoc xac nhan.
- [ ] Dung `Idempotency-Key` on dinh cho moi order.
- [ ] Luu `trackingCode` va `shipmentId`.
- [ ] Cai webhook endpoint va verify HMAC signature.
- [ ] Xu ly duplicate webhook theo `eventId`.
- [ ] Xu ly cac loi `400/401/403/409/429`.
- [ ] Khong log API key, Authorization header, webhook signing secret.

## 13. Tai Nguyen Kem Theo

- API reference ngan: `docs/partner-api.md`
- OpenAPI spec: `docs/partner-api.openapi.json`
- Postman collection: `postman/partner-api.postman_collection.json`
- Security review: `docs/partner-api-security-review.md`
