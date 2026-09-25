# Partner API Production-Readiness Checklist

This is a living evidence checklist, not a release announcement or a snapshot of a past test run. Code controls alone do not prove that a deployed environment is production-ready.

Current repository conclusion: **implementation supports production-like staging certification; public-production approval still requires environment evidence and owner sign-off.**

## Implemented controls to verify

| Area | Repository control | Required evidence in target environment |
| --- | --- | --- |
| Tenant isolation | Tracking is scoped to client shop; writes, external IDs, and idempotency remain client-owned | Cross-shop/cross-client integration results retained with release SHA |
| API credentials | Hashed, environment-prefixed keys; scopes, status, optional IP restrictions, credential audits | Issuance/rotation/revocation drill using the real secret store and ingress |
| Ingress identity | Explicit trusted proxy/network/hop configuration | Real proxy-chain test, including forged forwarded-header rejection |
| Rate limiting | Atomic Redis limiter is mandatory in Production; write actions fail closed on store outage | Multi-replica load test and Redis timeout/outage behavior |
| Webhook SSRF | HTTPS/port/public-address validation, connect-time destination pinning, no redirect, bounded response | Default-deny egress plus controlled DNS/rebinding/redirect test |
| Webhook integrity | HMAC signature, timestamp, event ID, secret version/snapshot | Receiver verifies signature and handles duplicate/out-of-order events |
| Queue reliability | SQL lease/claim, row version, retry/backoff, dead-letter and audited manual retry | Two-worker soak, kill-during-lease, expired-lease reclaim, alert verification |
| Data Protection | Production guard requires shared persistent key path and certificate protection | Cross-replica decrypt, rolling restart, backup and isolated restore |
| Data retention | Batched retention excludes unresolved failed/dead-letter work | Scheduled execution, deletion sampling, growth/capacity review |
| Observability | Correlation IDs, health probes, Partner API and worker meters | Exporter, dashboards, SLOs, alert routes, and on-call ownership |
| Public contract | Versioned endpoints, generated OpenAPI, compatibility/docs tests | Artifact generated from release SHA and partner acceptance run |
| Privacy | Public-safe timeline mapper excludes internal notes, actor, GPS, contact/address data | Privacy review of representative responses and logs |

## Certification sequence

1. Provision production-like staging with managed SQL Server, Redis, persistent encrypted Data Protection keys, real ingress, and at least two application/worker instances.
2. Apply migrations using the deployment process, verify `/health/live` and `/health/ready`, and retain the migration/rollback evidence.
3. Exercise API-key issuance, scope denial, revocation, IP restrictions, cross-shop isolation, idempotent replay, conflict behavior, and payload limits.
4. Run the end-to-end partner path: quote, create, track, signed webhook receipt, cancel where valid, and reconciliation after retries.
5. Execute load/soak at agreed SLO targets. Observe SQL plans/capacity, request-audit growth, Redis quotas, queue age, and worker throughput.
6. Inject Redis, SQL, DNS, receiver-timeout, and worker-termination failures. Confirm readiness policy, fail-open/fail-closed behavior, lease reclaim, dead letters, and alerts.
7. Validate backup/restore for SQL and the Data Protection key ring, plus credential and webhook-secret rotation across replicas.
8. Run dependency, container, SAST, and secret scans. Resolve Critical/High findings or record an owner-approved risk acceptance.
9. Have a pilot partner complete the integration checklist in an isolated sandbox using `ml_test_` credentials.
10. Attach evidence to the release SHA and obtain Product, Backend, Security, Platform/SRE, and QA approval before canary rollout.

## Minimum rollout and rollback plan

- Start with a small set of pilot shops, then increase cohorts only while API error/latency, Redis health, queue age, dead letters, webhook success, and database capacity remain within approved thresholds.
- Define numeric thresholds and evaluation windows in the deployment system/runbook before release; this repository does not currently encode business SLO values.
- Roll back application traffic when compatibility permits. Do not blindly roll back a migration; use the reviewed recovery plan for the specific schema change.
- Pause partner writes or webhook delivery independently when their dependency is unsafe, preserving queued records for reconciliation.

## Evidence record template

Do not add transient test counts to this document. Store or link release evidence from the delivery system using:

| Field | Value |
| --- | --- |
| Release SHA/image digest | |
| Environment/date | |
| Schema migration | |
| Load/soak profile and result | |
| Chaos/recovery result | |
| Security/privacy scan result | |
| Backup/restore result | |
| Pilot partner acceptance | |
| Approvers and accepted risks | |

## Related documents

- [Partner API production contract](architecture/adr-001-partner-api-production-contract.md)
- [Transactional outbox and webhooks](architecture/adr-003-transactional-outbox-and-webhooks.md)
- [Partner API threat model](security/partner-api-threat-model.md)
- [Partner API production runbook](operations/partner-api-production-runbook.md)
- [Partner API reference](partner-api.md)
- [Generated OpenAPI](partner-api.openapi.json)
