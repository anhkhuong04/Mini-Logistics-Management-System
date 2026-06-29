# MiniLogistics Partner API

Base URL:

```text
http://localhost:5221/api/v1/partner
```

Authentication:

```http
Authorization: Bearer {api_key}
```

All error responses use:

```json
{
  "error": {
    "code": "Application.ValidationFailed",
    "message": "Validation message",
    "traceId": "0H..."
  }
}
```

## Rate Limits

Limits are enforced per API client with a 1 minute fixed window.

| Endpoint | Limit |
| --- | ---: |
| `POST /shipping/quote` | 60/min |
| `POST /shipments` | 30/min |
| `GET /shipments/{trackingCode}` | 120/min |
| `POST /shipments/{trackingCode}/cancel` | 30/min |

When exceeded, the API returns `429` with `Retry-After`.

## Quote

```http
POST /api/v1/partner/shipping/quote
Authorization: Bearer {api_key}
```

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

## Create Shipment

```http
POST /api/v1/partner/shipments
Authorization: Bearer {api_key}
Idempotency-Key: ECOM-10001
```

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
  "note": "Deliver during office hours"
}
```

Responses:

- `201 Created`: shipment created.
- `200 OK`: same idempotency key and same request body, old response replayed.
- `409 Conflict`: same idempotency key with different body, or shipment lifecycle rule conflict.

Create shipment writes an audit row to `PartnerApiRequestAudits` for authenticated requests. The audit stores metadata and request hash only, not raw payload or secrets.

## Tracking

```http
GET /api/v1/partner/shipments/{trackingCode}
Authorization: Bearer {api_key}
```

The API client can only read shipments linked to its own shop/client.

## Cancel

```http
POST /api/v1/partner/shipments/{trackingCode}/cancel
Authorization: Bearer {api_key}
```

```json
{
  "reason": "Customer cancelled order"
}
```

## Webhooks

Webhook deliveries are configured in `/partner/integrations`.

Headers:

```http
X-MiniLogistics-Event: shipment.status_changed
X-MiniLogistics-Signature: sha256={hmac}
X-MiniLogistics-Timestamp: 2026-06-29T10:30:00Z
```

Signature input:

```text
timestamp + "." + raw_payload_json
```

Supported events:

- `shipment.created`
- `shipment.status_changed`
- `webhook.test`
