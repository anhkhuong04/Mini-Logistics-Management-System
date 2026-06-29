# Partner E-commerce Integration

Muc tieu: cho website ban hang ben thu 3 goi MiniLogistics de tinh phi, tao shipment, tracking/cancel, va nhan webhook status thay vi nhap don thu cong.

Flow chinh:

```text
E-commerce checkout
-> POST /api/v1/partner/shipping/quote
-> POST /api/v1/partner/shipments
-> MiniLogistics van hanh don
-> webhook status ve e-commerce
```

## Trang Thai

| Phase | Trang thai | Ghi chu |
| --- | --- | --- |
| P0 - Design decisions | Done | Partner API nam trong `MiniLogistics.Web`, Bearer API key, 1 external order = 1 shipment. |
| P1 - Partner API Core | Done | ApiClient, ExternalShipmentReference, quote/create, idempotency, seed demo key. |
| P2 - Tracking va Cancel API | Done | Tracking/cancel endpoint, boundary theo API client/shop, error response chuan. |
| P3 - Webhook | Done | WebhookEndpoint, WebhookDelivery, HMAC signature, worker retry/backoff, delivery log. |
| P4 - UI Quan Tri Integration | Done | `/partner/integrations`, create/rotate/revoke API key, webhook config/test/history. |
| P5 - Hardening | Done | Rate limit, create shipment audit, docs/OpenAPI/Postman, contract tests, security review. |

## P5 Checklist

- [x] Rate limit theo API client.
- [x] Audit log request tao shipment.
- [x] API docs Markdown/OpenAPI.
- [x] Example curl/Postman collection.
- [x] Contract tests cho API response.
- [x] Security review: secret storage, logging, authorization boundaries.

## Artefacts

- Partner API docs: `docs/partner-api.md`
- OpenAPI spec: `docs/partner-api.openapi.json`
- Security review: `docs/partner-api-security-review.md`
- Postman collection: `postman/partner-api.postman_collection.json`
- UI quan tri: `/partner/integrations`
- Demo API key seed: `ml_demo_partner_key_123456`

## Technical Notes

- API key chi hien raw value mot lan khi create/rotate; DB luu hash + prefix.
- Rate limit hien la in-memory fixed window theo API client:
  - Quote: 60/min
  - Create shipment: 30/min
  - Tracking: 120/min
  - Cancel: 30/min
- `PartnerApiRequestAudits` luu metadata va request hash cho create shipment, khong luu raw payload/secret.
- Webhook signing secret hien dang luu recoverable/plaintext de worker tinh HMAC; production nen ma hoa bang Key Vault/KMS/DPAPI.
- Multi-instance deployment can doi rate limiter sang Redis/shared store.

## Verification

- Application tests cover quote/create/idempotency/auth boundary/webhook management.
- Infrastructure tests cover webhook delivery success/retry.
- Web contract tests cover Partner API error response, audit write, and rate limit response.
