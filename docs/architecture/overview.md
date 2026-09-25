# Architecture Overview

## System shape

Mini Logistics is a .NET 10 modular monolith deployed as one ASP.NET Core process. The process hosts:

- a Blazor Web App using Interactive Server rendering;
- minimal API endpoints under `/api/v1/partner`;
- ASP.NET Core Identity and authorization policies;
- EF Core with SQL Server;
- background workers for imports, outbox dispatch, webhook delivery, and partner-data retention.

SQL Server is the system of record. Redis is optional in development and mandatory for Partner API rate limiting in production mode. Webhook receivers are the principal outbound external dependency.

## Project dependency direction

```text
MiniLogistics.Web (composition root, UI, HTTP)
        |                         |
        v                         v
MiniLogistics.Application <--- MiniLogistics.Infrastructure
        |
        v
MiniLogistics.Domain
```

The actual project references enforce these rules:

- Domain has no project dependency and owns entities, value objects, errors, and invariants.
- Application depends only on Domain and owns use-case orchestration, authorization, validation, and ports/interfaces.
- Infrastructure depends on Application and Domain and implements EF Core, Identity, cryptography, Redis, external HTTP, and workers.
- Web depends on Application and Infrastructure, composes the runtime, and translates UI/HTTP input and output.

Blazor components and endpoints call application services. They must not query `MiniLogisticsDbContext` directly. See [ADR-002](adr-002-modular-monolith.md) for the decision and trade-offs.

## Main runtime flows

### Create shipment

```text
UI / Partner endpoint
  -> validator and actor/shop authorization
  -> address normalization + route classification
  -> fee calculation
  -> Shipment + COD (+ external reference/outbox for Partner API)
  -> one EF Core SaveChanges unit of work
  -> auto-assignment attempt and second SaveChanges when assignment succeeds
```

Creation remains successful when no shipper is eligible; Operations handles the `PendingPickup` shipment. Auto-assignment is therefore a follow-up step, not a prerequisite for persistence. Partner idempotency guards are persisted before returning the response.

### Change shipment status

```text
UI / application caller
  -> validate command
  -> load tracked Shipment aggregate
  -> authorize operation or active assigned shipper
  -> domain transition + history
  -> enqueue safe notification/webhook outbox records
  -> audit record
  -> one EF Core SaveChanges unit of work
```

The domain controls transition validity; the application controls who may request it.

### Deliver webhooks

```text
business transaction
  -> OutboxMessages
  -> OutboxWorker claims a leased batch
  -> creates WebhookDeliveries
  -> WebhookDeliveryWorker claims a leased batch
  -> revalidates destination and sends signed HTTPS request
  -> success, retry with backoff, or dead letter
```

SQL row versions, leases, and claim queries coordinate multiple worker instances. This provides durable at-least-once processing, not exactly-once delivery.

## Cross-cutting concerns

### Identity and authorization

ASP.NET Core Identity stores users and coarse roles. Application services add operational permissions, shop membership/permissions, active-user checks, assigned-shipper checks, and integration-management scopes. Partner API authentication uses hashed bearer API keys with environment prefixes and explicit scopes.

### Validation and errors

FluentValidation handles command shape and input constraints. Domain methods guard invariants. Application services return `Result`/typed errors; Web maps them to UI feedback or stable Partner API error responses.

### Persistence and transactions

Repositories share the scoped `MiniLogisticsDbContext`; a single `SaveChangesAsync` persists all tracked changes atomically. Explicit database transactions exist for workflows that span multiple `SaveChanges` operations, but not every service uses them. See [Data model](../database/data-model.md) for current guarantees and gaps.

### Observability and operations

Partner requests emit a server correlation ID and metrics. `/health/live` reports process liveness; `/health/ready` checks required runtime dependencies. Background workers expose queue/result telemetry. The [Partner API runbook](../operations/partner-api-production-runbook.md) defines deployment and recovery checks.

### Security boundaries

- Production startup rejects local SQL, sandbox partner mode, memory Partner API limiting, wildcard hosts, disabled retention, missing trusted-proxy settings, and non-persistent/unprotected Data Protection configuration.
- Webhook destinations are restricted to HTTPS/allowed ports, resolved to public addresses, checked again at connect time, bounded by time/size limits, and sent without redirects.
- Partner and public projections mask internal/PII fields.
- Secrets belong in environment/provider configuration, never committed configuration.

Partner-specific risks are maintained in the [threat model](../security/partner-api-threat-model.md).

## Repository map

```text
src/                         production projects
  MiniLogistics.Domain/
  MiniLogistics.Application/
  MiniLogistics.Infrastructure/
  MiniLogistics.Web/
test/                        Domain, Application, Infrastructure, and Web tests
docs/                        product and engineering knowledge
postman/                     Partner API collection/environment
scripts/                     operational, compatibility, load, and validation scripts
.agents/ and AGENTS.md        coding-agent operational guidance
```

The similarly named root `Mini-logistics-manegemant-system.csproj` is not the application entry point. Build the `.slnx` or a specific project.
