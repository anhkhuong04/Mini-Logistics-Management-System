# Partner API Security Review

Date: 2026-07-15

## Implemented Controls

- API clients authenticate with `Authorization: Bearer {api_key}`.
- Raw API keys are shown once on create/rotate and are not stored.
- API key lookup uses SHA-256 hash at rest plus non-sensitive prefix for display/debug.
- `ApiClient.IsActive` supports revoke/reactivate; rotate generates a new key and invalidates the old hash.
- Partner API endpoints enforce shop/client boundaries through `ApiClient.ShopId` and `ExternalShipmentReference.ApiClientId`.
- Create shipment is idempotent by `(ApiClientId, IdempotencyKey)` and detects conflicting bodies.
- Webhook delivery signs payloads with HMAC SHA-256.
- Shipment webhook events are written to `OutboxMessages` in the same transaction as the business change; an outbox worker later creates `WebhookDelivery` rows for network retry.
- Webhook signing secrets are protected at rest through `ISecretProtector`; the Infrastructure implementation uses ASP.NET Core Data Protection with a versioned prefix.
- Create shipment audit logs store metadata, status, latency and request hash only, not raw request payload or secrets.
- Credential/integration actions write `PartnerApiCredentialAudit` records for create, rotate, activate/deactivate, webhook upsert and webhook test queue.
- Rate limits are enforced per API client with endpoint-specific quotas; `PartnerApi:RateLimiting:Mode` can switch from `Memory` to `Distributed`.
- Error responses are normalized and include trace id for support.

## Known Tradeoffs

- Existing plaintext webhook secrets from older deployments are readable as a compatibility fallback until they are rotated/upserted; new/updated secrets are stored protected. Production should back Data Protection keys with a durable key ring or replace `ISecretProtector` with KMS/Key Vault.
- Inbound Partner API uses bearer API key only. For public internet production, add optional request HMAC signing with timestamp to reduce replay risk.
- Distributed rate limiting uses `IDistributedCache`; use a real shared cache provider for multi-instance deployments.
- Credential audit has nullable trace/client metadata fields, but the current UI flow does not yet pass IP hash or user agent into the application service.
- No dedicated API key scope model exists yet; current scope is implicit by shop and endpoint group.

## Logging Rules

- Do not log `Authorization`, raw API key, raw webhook signing secret, or full customer PII payload.
- Log API key prefix only when support/debug context needs it.
- Prefer `TraceId`, `ApiClientId`, `ShopId`, `ExternalOrderId`, `TrackingCode`, and request hash for support investigations.

## Authorization Boundaries Checked

- API client can quote only under its assigned shop.
- API client can create shipments only for its assigned shop.
- API client can read/cancel only shipments linked by `ExternalShipmentReference` to the same `ApiClientId`.
- Partner API authentication rejects active API clients when the owning shop is inactive.
- Shop/Admin UI integration page uses application service checks: Shop manages own shop only; Admin can manage all shops.
