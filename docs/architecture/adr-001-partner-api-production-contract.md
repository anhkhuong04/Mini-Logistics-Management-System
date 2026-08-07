# ADR-001: Partner API Production Contract

- Status: Accepted in code; Security/Product/Platform sign-off pending
- Date: 2026-08-02
- Owners: Backend, Security, Platform, Product

## Context

Partner API v1 originally required an `ExternalShipmentReference` owned by the same
API client for tracking. That prevented a shop from tracking shipments created from
the portal, CSV import, or another API client. The original worker, rate-limit, URL,
proxy, and key-ring defaults were also unsafe for horizontal scale.

## Decision

### Authorization and public contract

- `GET /api/v1/partner/shipments/{trackingCode}` authorizes by the authenticated
  client's `ShopId` and `TrackShipment` scope.
- Tracking lookup remains `trackingCode + ShopId`; cross-shop results are always
  `404` and do not disclose whether a shipment exists.
- `externalOrderId` is returned only when the reference belongs to the current API
  client. It is `null` for UI/CSV shipments and shipments created by another client
  in the same shop.
- Create, cancel, and idempotency ownership remain bound to the originating
  `ApiClientId`. This ADR does not broaden write access.
- Partner and public tracking timelines return deterministic `messageCode`, safe
  `message`, and `locale=vi-VN`. Internal `ShipmentStatusHistory.Note`, GPS, actor,
  token, phone, email, and address data are never used as a fallback.
- Public API compatibility is managed under `/api/v1`; additive changes are allowed.
  Breaking response/auth semantics require a new major path and deprecation period.

### Production topology baseline

- One TLS ingress/load balancer, at least two application replicas, and at least two
  active worker instances.
- SQL Server is the source of truth. SQL `UPDLOCK`, `READPAST`, `ROWLOCK`, rowversion,
  and expiring leases coordinate outbox/webhook workers.
- Redis is required for production Partner API rate limiting. Memory mode is local
  development only.
- ASP.NET Data Protection keys use a persistent shared volume and certificate/KMS
  wrapping. Every replica uses the same environment-specific `ApplicationName`.
- Webhook egress is HTTPS-only, DNS/IP validated at registration and connect time,
  redirect-disabled, response-bounded, and restricted again by network egress rules.
- Only explicitly configured reverse proxies/networks may supply forwarded headers.

### Initial SLO and capacity contract

These are release targets, not evidence that staging has already met them:

| Signal | Initial target | Measurement window |
| --- | --- | --- |
| Tracking availability | >= 99.9%, excluding authenticated client errors | Calendar month |
| Tracking latency | p95 <= 300 ms, p99 <= 750 ms at 100 RPS | 5-minute windows |
| Partner API server errors | < 0.1% 5xx | 5-minute windows |
| Outbox queue age | p95 <= 30 seconds, max <= 2 minutes | 5-minute windows |
| Webhook handoff | 99% of events enter delivery queue within 60 seconds | 5-minute windows |
| Webhook delivery | Receiver-dependent; alert if success < 95% for 15 minutes | 15-minute windows |

### Retention

| Data | Default | Owner |
| --- | ---: | --- |
| Succeeded outbox/webhook rows | 30 days | Platform/SRE |
| Partner request audits | 180 days | Security/Product |
| Credential audits | 365 days | Security |
| Failed/dead-lettered rows | Retain until reviewed and recovered | SRE/Backend |
| Data Protection key ring/backups | Per key lifetime and restore policy | Platform/Security |

The retention worker deletes only succeeded queue records and expired audits in
batches. It never automatically deletes unresolved DLQ rows.

## Environment Contract

| Environment | Key prefix | SQL | Rate limit | DP key ring | Webhook egress | Owner/source |
| --- | --- | --- | --- | --- | --- | --- |
| Local | `ml_test_` | LocalDB | Memory | Ephemeral or local path | Public HTTPS tests | Developer config |
| Test/CI | `ml_test_` | Isolated LocalDB | Memory/fake atomic store | Temp shared path | Fake DNS/handler | CI secrets |
| Sandbox | `ml_test_` | Isolated managed SQL | Redis | Shared + KEK | Restricted public HTTPS | Platform secret manager |
| Staging | `ml_test_` | Production-like SQL | Redis | Shared + KEK | Production-like firewall | Platform secret manager |
| Production | `ml_live_` | Managed/HA SQL | Redis required | Shared + KEK required | Default deny + approved HTTPS | Platform secret manager |

Production startup fails for sandbox mode, memory rate limiting, local SQL, wildcard
hosts, missing trusted proxy config, missing shared key path/certificate, enabled
seeding, or disabled retention.

## Consequences

- Same-shop API clients can read a common tracking view but cannot see each other's
  external order IDs or cancel each other's shipments.
- Webhooks remain tied to the external reference/client that created the shipment.
- Delivery remains at-least-once. Partner receivers must deduplicate by `eventId` and
  reconcile out-of-order events.
- Infrastructure certification, firewall verification, restore drills, and SLO load
  evidence remain release gates and cannot be proven by repository tests alone.

## Related Documents

- [Threat model](../security/partner-api-threat-model.md)
- [Production runbook](../operations/partner-api-production-runbook.md)
- [Partner API contract](../partner-api.md)
- [OpenAPI document](../partner-api.openapi.json)
