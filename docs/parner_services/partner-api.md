# MiniLogistics Partner API v1

Machine-readable contract: [`partner-api.openapi.json`](partner-api.openapi.json).
This API is backend-to-backend only. Never put an API key in browser JavaScript,
HTML, a mobile bundle, logs, or a source repository.

## Environments and Authentication

| Environment | Example base URL | Key prefix |
| --- | --- | --- |
| Sandbox/staging | Supplied by MiniLogistics | `ml_test_` |
| Production | `https://api.minilogistics.example/api/v1/partner` | `ml_live_` |

Keys are environment-bound. A sandbox key is rejected in production and a live key
is rejected in sandbox.

```http
Authorization: Bearer <environment-specific-api-key>
Accept: application/json
```

Optional exact-IP allowlists are evaluated after the configured trusted proxy chain.
The API client and its shop must both be active and the key must not be expired.

## Scopes and Limits

| Method/path | Scope | Default limit/client |
| --- | --- | ---: |
| `POST /shipping/quote` | `Quote` | 60/minute |
| `POST /shipments` | `CreateShipment` | 30/minute |
| `GET /shipments/{trackingCode}` | `TrackShipment` | 120/minute |
| `POST /shipments/{trackingCode}/cancel` | `CancelShipment` | 30/minute |

Quota is shared across replicas. `429` includes `Retry-After` in seconds. During a
rate-store outage, create/cancel return `503 PartnerApi.RateLimitUnavailable`; quote
and tracking follow the availability-first fail-open policy. Limits can differ by
environment.

## Ownership Contract

- Tracking read authorization is by `ShopId`. A client can track any non-draft
  shipment in its shop, including shipments created by UI, CSV import, itself, or
  another API client.
- Cross-shop lookup returns uniform `404` without disclosing existence.
- `externalOrderId` is returned only when the shipment reference belongs to the
  current API client. It is `null` for UI/CSV and another same-shop client's shipment.
- Create, cancel, and idempotency remain bound to the originating `ApiClientId`.
  A different same-shop client cannot cancel the shipment and receives `404`.

## Track Shipment

```bash
curl --request GET \
  --url "https://api.minilogistics.example/api/v1/partner/shipments/ML202608020001" \
  --header "Authorization: Bearer $MINILOGISTICS_API_KEY" \
  --header "Accept: application/json"
```

Response `200`:

```json
{
  "trackingCode": "ML202608020001",
  "externalOrderId": null,
  "status": "InTransit",
  "codStatus": "PendingCollection",
  "shippingFeeAmount": 53000,
  "currency": "VND",
  "timeline": [
    {
      "status": "PendingPickup",
      "messageCode": "SHIPMENT_PENDING_PICKUP",
      "message": "Vận đơn đang chờ lấy hàng.",
      "locale": "vi-VN",
      "changedAtUtc": "2026-08-02T03:10:00+00:00"
    },
    {
      "status": "InTransit",
      "messageCode": "SHIPMENT_IN_TRANSIT",
      "message": "Vận đơn đang được vận chuyển.",
      "locale": "vi-VN",
      "changedAtUtc": "2026-08-02T08:30:00+00:00"
    }
  ]
}
```

Use `messageCode` for program logic. `message` is safe display text for the declared
locale. The API never exposes internal free-form notes, actor IDs, GPS, phone, email,
address, or tokens in timeline items. All timestamps are UTC ISO-8601.

Shipment status values: `Draft`, `PendingPickup`, `Assigned`, `PickingUp`,
`PickedUp`, `InTransit`, `Delivering`, `Delivered`, `DeliveryFailed`, `Returned`,
`Cancelled`. Public Partner tracking does not return `Draft` shipments.

COD values: `NotRequired`, `PendingCollection`, `Collected`, `Settled`.

## Create Shipment

Every create request requires an idempotency key unique within the API client:

```bash
curl --request POST \
  --url "https://api.minilogistics.example/api/v1/partner/shipments" \
  --header "Authorization: Bearer $MINILOGISTICS_API_KEY" \
  --header "Content-Type: application/json" \
  --header "Idempotency-Key: order-10001-create-v1" \
  --data '{
    "externalOrderId": "ORDER-10001",
    "receiver": { "name": "Nguyen Van A", "phone": "0911111111" },
    "deliveryAddress": {
      "street": "9 Le Loi",
      "ward": "Ben Nghe",
      "province": "Ho Chi Minh",
      "country": "Vietnam"
    },
    "parcel": { "weightKg": 1.2, "lengthCm": 20, "widthCm": 15, "heightCm": 10 },
    "goodsValueAmount": 2000000,
    "codAmount": 150000,
    "currency": "VND",
    "note": "Deliver during office hours"
  }'
```

The first successful request returns `201`; replaying the same key and payload
returns `200` with the original response. Reusing the key with a different payload
returns `409 PartnerApi.IdempotencyConflict`. `externalOrderId` is unique per API
client. Omitted sender/pickup fields use the shop profile.

The full quote/create/cancel schemas and enum constraints are in OpenAPI. The Postman
collection is at `postman/partner-api.postman_collection.json`.

## Error Contract

```json
{
  "error": {
    "code": "PartnerApi.MissingScope",
    "message": "API client does not have the required scope.",
    "traceId": "0HNN...:00000001"
  }
}
```

| HTTP | Client behavior |
| --- | --- |
| `400` | Fix validation; do not retry unchanged input. |
| `401` | Stop; key is missing/invalid/wrong environment. |
| `403` | Check scope, IP, expiry, client status, and shop status. |
| `404` | Treat as unavailable to this client; do not probe. |
| `409` | Resolve idempotency or shipment-state conflict. |
| `429` | Wait `Retry-After`, add jitter, then retry. |
| `503` | Rate-limit dependency unavailable for protected write; retry with backoff. |
| `500` | Retry bounded exponential backoff; retain `traceId` for support. |

Every Partner response includes `X-Correlation-ID`. Never log authorization values,
PII, or whole response bodies in a customer-visible log.

## Webhooks

Configure an active HTTPS URL and a random secret of at least 32 UTF-8 bytes in
`/partner/integrations`. Private, loopback, link-local, metadata, reserved, redirect,
or DNS-rebound destinations are blocked. Production network policy applies an
additional egress boundary.

Headers:

```http
X-MiniLogistics-Event: shipment.status_changed
X-MiniLogistics-Timestamp: 2026-08-02T08:30:00.0000000+00:00
X-MiniLogistics-Signature: sha256=<hex-hmac-sha256>
X-MiniLogistics-Webhook-Version: 1.0
X-MiniLogistics-Secret-Version: 2
Content-Type: application/json
```

Payload:

```json
{
  "eventId": "f3265a09-9655-45c0-8549-17a24d92c18e",
  "event": "shipment.status_changed",
  "trackingCode": "ML202608020001",
  "externalOrderId": "ORDER-10001",
  "status": "InTransit",
  "changedAtUtc": "2026-08-02T08:30:00+00:00",
  "schemaVersion": "1.0"
}
```

Event types are `webhook.test`, `shipment.created`, and
`shipment.status_changed`. The signature is computed over exact raw bytes:

Shipment webhooks belong to the API client/external reference that created the
shipment. Shop-wide tracking access does not subscribe the client to UI/CSV or
another client's events; poll the tracking endpoint for those shipments.

```text
sha256=hex(HMAC-SHA256(secret, timestamp + "." + raw_body))
```

Receivers must:

1. Read the secret selected by `X-MiniLogistics-Secret-Version`.
2. Reject timestamps outside a five-minute window and compare HMAC in constant time.
3. Insert `eventId` under a unique constraint before applying the event.
4. Apply only a `changedAtUtc` newer than the stored state; never trust arrival order.
5. Queue heavy work and return `2xx` quickly.
6. Reconcile uncertain state through tracking.

Delivery is at-least-once. Failed deliveries use bounded exponential backoff and move
to DLQ after retry exhaustion; authorized portal users can perform audited retry.
Secret rotation increments the version and queued deliveries retain their original
protected secret snapshot.

## Lifecycle and Support

- Key rotation immediately invalidates the old key; test rotation in sandbox first.
- Deactivate a suspected-compromised client and contact the configured support path
  with `traceId`, API client ID, event ID, and timestamp, never the key/secret.
- Versioning and deprecation: [`partner-api-changelog.md`](partner-api-changelog.md).
- Backend examples: [`examples/partner-api-node.mjs`](examples/partner-api-node.mjs)
  and [`examples/PartnerApiExample.cs`](examples/PartnerApiExample.cs).
- Shop implementation guide:
  [`third-party-shipment-integration-guide.md`](third-party-shipment-integration-guide.md).
