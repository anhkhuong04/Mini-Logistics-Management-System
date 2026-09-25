# ADR-003: Transactional Outbox and At-Least-Once Webhooks

- Status: Accepted
- Date: 2026-09-24
- Owners: Backend, Platform, Security

## Context

Shipment creation and status changes must produce partner webhooks and shop notifications. Calling external receivers inside the business request would make database success depend on an unreliable network and could lose an event if the database commits after a failed send or the process stops between commit and send.

## Decision

Persist an `OutboxMessage` in the same EF Core unit of work as the related business change. A background worker claims due outbox rows with a database lease and converts them into durable notification or webhook-delivery records. A second worker claims webhook deliveries and sends signed HTTPS requests.

Processing semantics are at-least-once:

- rows have stable identifiers, status, retry count, next-attempt time, lease owner/expiry, and SQL row version;
- multiple workers coordinate through atomic SQL claim logic and expired leases can be reclaimed;
- webhook-delivery creation is deduplicated by the outbox/event identifier;
- attempts use bounded exponential backoff and stop in a failed/dead-letter state after the configured maximum;
- operations may inspect and explicitly retry failures through authorized, audited workflows;
- receivers must deduplicate by event ID and tolerate retries or out-of-order delivery.

Webhook security is applied at delivery time: the destination is revalidated, redirects are disabled, request/response bounds apply, and the payload is signed with a protected secret snapshot/version.

## Consequences

Positive:

- A committed business change retains durable evidence of work still to be dispatched.
- External latency and outages do not hold the business transaction open.
- Leases allow horizontal worker execution and crash recovery.
- Secret snapshots allow already-queued events to remain verifiable across secret rotation.

Trade-offs:

- Delivery is not immediate and cannot be exactly once.
- Queue growth, age, retries, dead letters, retention, and database contention require monitoring.
- Consumers need idempotent handlers and reconciliation.
- The current workers run in the web process, so build/design-time host startup and deployment lifecycle can affect them.

## Operational requirements

- Alert on queue age, failed/dead-letter counts, retry rate, and webhook error/latency.
- Do not automatically delete unresolved failures. Retention applies only to eligible succeeded/audit records.
- Test lease reclaim and multi-worker behavior against SQL Server, not only in-memory fakes.
- Preserve event ID, signing contract, destination policy, and outbox atomicity when changing integration flows.

See the [production runbook](../operations/partner-api-production-runbook.md) and [threat model](../security/partner-api-threat-model.md).
