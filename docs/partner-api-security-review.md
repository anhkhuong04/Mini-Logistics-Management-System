# Partner API Security Review

Date: 2026-06-29

## Implemented Controls

- API clients authenticate with `Authorization: Bearer {api_key}`.
- Raw API keys are shown once on create/rotate and are not stored.
- API key lookup uses SHA-256 hash at rest plus non-sensitive prefix for display/debug.
- `ApiClient.IsActive` supports revoke/reactivate; rotate generates a new key and invalidates the old hash.
- Partner API endpoints enforce shop/client boundaries through `ApiClient.ShopId` and `ExternalShipmentReference.ApiClientId`.
- Create shipment is idempotent by `(ApiClientId, IdempotencyKey)` and detects conflicting bodies.
- Webhook delivery signs payloads with HMAC SHA-256.
- Create shipment audit logs store metadata and request hash only, not raw request payload or secrets.
- Rate limits are enforced per API client with endpoint-specific quotas.
- Error responses are normalized and include trace id for support.

## Known Tradeoffs

- Webhook signing secret is currently stored as plaintext because the worker must recover it to compute HMAC. Production should encrypt this value with DPAPI, Key Vault, KMS, or an application-level encryption key.
- Inbound Partner API uses bearer API key only. For public internet production, add optional request HMAC signing with timestamp to reduce replay risk.
- Rate limiter is in-memory. In multi-instance deployment, move counters to Redis or another shared store.
- Audit log does not yet capture response latency or client IP hash. Add these when observability requirements are clearer.
- No dedicated API key scope model exists yet; current scope is implicit by shop and endpoint group.

## Logging Rules

- Do not log `Authorization`, raw API key, raw webhook signing secret, or full customer PII payload.
- Log API key prefix only when support/debug context needs it.
- Prefer `TraceId`, `ApiClientId`, `ShopId`, `ExternalOrderId`, `TrackingCode`, and request hash for support investigations.

## Authorization Boundaries Checked

- API client can quote only under its assigned shop.
- API client can create shipments only for its assigned shop.
- API client can read/cancel only shipments linked by `ExternalShipmentReference` to the same `ApiClientId`.
- Shop/Admin UI integration page uses application service checks: Shop manages own shop only; Admin can manage all shops.
