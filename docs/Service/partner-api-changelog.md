# Partner API Changelog and Deprecation Policy

## Versioning Policy

- REST major versions are encoded in the path, currently `/api/v1/partner`.
- Webhook schema version is present in payload `schemaVersion` and header
  `X-MiniLogistics-Webhook-Version`.
- Additive fields and new enum values may be released within v1. Consumers must
  ignore unknown JSON properties and handle unknown enum values safely.
- Removing/renaming fields, narrowing ownership, changing authentication/signature,
  or changing existing field meaning requires a new major API/webhook version.
- A breaking version receives at least 90 days notice in sandbox and production,
  unless an actively exploited security issue requires emergency action.
- Deprecation notices are published here and through the partner support channel.

## 2026-08-02 - v1 Production Hardening

### Changed

- Tracking authorization is now shop-scoped. API clients can track shipments created
  by portal UI, CSV import, their own client, or another client in the same shop.
- `externalOrderId` is nullable and is returned only for the current API client's
  own external reference.
- Timeline items now expose `messageCode`, safe Vietnamese `message`, `locale`, and
  `changedAtUtc`. Raw internal `note` is removed from the public contract.
- Sandbox keys use `ml_test_`; live keys use `ml_live_` and are rejected across
  environments.
- Webhook payload/header schema version is `1.0`; secret version is sent in
  `X-MiniLogistics-Secret-Version`.

### Security and Reliability

- Webhook URLs are HTTPS-only and protected against private/reserved IP targets,
  redirects, mixed DNS answers, and DNS rebinding at connect time.
- Trusted reverse proxy configuration is explicit.
- Production rate limiting uses atomic Redis operations with timeout, circuit
  breaker, and endpoint-specific fail-open/fail-closed policy.
- Outbox/webhook workers use SQL leases, rowversion, reclaim, DLQ, audited manual
  retry, and retention for succeeded rows.
- Data Protection supports shared persistent keys protected by certificate.
- Liveness/readiness probes, production config guards, metrics, OpenAPI generation,
  contract drift checks, and an unversioned breaking-change CI gate were added.

### Compatibility Note

Removing `timeline.note` is a privacy/security correction. Consumers must use
`messageCode` for logic and `message` only for display. This change must be announced
to any pilot consumer that depended on the undocumented internal note field.
