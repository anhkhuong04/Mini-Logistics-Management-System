# Project Context

## Product and runtime

Mini Logistics is a logistics operations modular monolith. It supports Shop shipment management, operational assignment and delivery, COD, public tracking, administration, partner REST integrations, outbox processing, and signed webhooks.

- Runtime: .NET 10, ASP.NET Core Blazor Web App with Interactive Server components.
- Data: SQL Server/LocalDB, EF Core Code First, ASP.NET Core Identity.
- Validation/testing: FluentValidation and xUnit.
- Distributed production concerns: Redis partner rate limiting, persisted Data Protection keys, hosted outbox/webhook/import/retention workers.
- Logging uses `Microsoft.Extensions.Logging`; Serilog is not installed.

## Projects and dependency direction

```text
src/MiniLogistics.Domain          no project references
src/MiniLogistics.Application     -> Domain
src/MiniLogistics.Infrastructure  -> Application, Domain
src/MiniLogistics.Web             -> Application, Infrastructure

test/MiniLogistics.Domain.Tests
test/MiniLogistics.Application.Tests
test/MiniLogistics.Infrastructure.Tests
test/MiniLogistics.Web.Tests
```

Web is the composition root, so its Infrastructure reference is intentional. Direct Infrastructure use should remain limited to host composition and web adapters such as Identity endpoints; business use cases go through Application contracts.

There is no `MiniLogistics.Shared` project. The root `Mini-logistics-manegemant-system.csproj` is not part of the solution and is not a supported build target.

## Layer responsibilities

- **Domain:** aggregates, entities, value objects, state transitions, fee calculation, invariant errors. No EF, HTTP, Identity, UI, or other project dependency.
- **Application:** feature-oriented use cases, DTOs, FluentValidation, authorization/data-isolation checks, repository/external-service ports, transaction orchestration, and expected `Result` errors.
- **Infrastructure:** EF Core context/configurations/repositories/migrations, Identity, caches, secret protection, outbox and webhook dispatch, retention/import workers, and external transport.
- **Web:** Razor components, minimal endpoints, request/response mapping, host configuration, browser/partner rate limiting, localization, and static assets. No direct database queries from components.

Application is organized by feature/use case rather than generic command/query folders. Follow the nearest feature's structure before inventing a new pattern.

## Repository map

- `src/MiniLogistics.Domain/Shipments`: `Shipment` aggregate, assignment/history/proof, status and route types.
- `src/MiniLogistics.Domain/CashOnDelivery`, `Fees`, `Operations`, `Shops`, `PartnerApi`, `Outbox`: supporting domain models.
- `src/MiniLogistics.Application/Shipments`, `CashOnDelivery`, `Shops`, `Shippers`, `PartnerApi`, `Admin*`: use cases and ports.
- `src/MiniLogistics.Infrastructure/Persistence`: DbContext, configurations, repositories, migrations, caches, audit, seeding and import worker.
- `src/MiniLogistics.Infrastructure/PartnerApi` and `Outbox`: secure webhook transport, retention, dispatch and workers.
- `src/MiniLogistics.Web/Components`: role-oriented Blazor UI.
- `src/MiniLogistics.Web/Endpoints`: browser auth, partner API, localization and Shop file downloads.
- `test`: unit, application, SQL LocalDB integration, web contract, load and manual tests.
- `docs` and `postman`: partner-facing contracts and examples; keep them aligned with API changes.

## Roles and authorization model

Identity roles are `Admin`, `Operator`, `Shop`, `Shipper`, and `IntegrationAdmin`.

- Admin has all operational permissions and COD settlement.
- Operator has operational shipment/assignment/proof/COD-collection permissions, but not COD settlement or admin audit viewing.
- Shop access is tenant-scoped. A Shop account may also have a `ShopStaffMembership` with role/flag permissions such as shipment management, full PII, export, COD, audit, integration, notification, and profile management.
- Shipper access is restricted to active assignments and the permitted transition/action.
- Integration management is additionally scoped by global, Shop, or province records. Partner API clients are Shop-bound and permissioned by `PartnerApiScope` flags.
- Public tracking must return only the safe summary unless the phone-last-four verification succeeds.

Do not infer authorization solely from navigation or role attributes. Preserve checks inside application services and repositories/query scope.

## Core business invariants

### Shipment lifecycle

```text
Draft -> PendingPickup | Cancelled
PendingPickup -> Assigned | Cancelled
Assigned -> PickingUp | Cancelled
PickingUp -> PickedUp | Cancelled
PickedUp -> InTransit | Returned
InTransit -> Delivering | Returned
Delivering -> Delivered | DeliveryFailed | Returned
DeliveryFailed -> Delivering | Returned
```

`Delivered`, `Returned`, and `Cancelled` are terminal. `DeliveryFailed` requires a note. Every creation/transition adds history; do not update `Status` or collection fields directly.

### Assignment and COD

- Assignment starts only from `PendingPickup`; only one active assignment per shipment is permitted.
- Reassigning or cancelling an assignment is valid only while `Assigned`.
- Auto-assignment uses pickup province → hub/working area, active/available shipper, and capacity. Operations may manually override with an area mismatch warning.
- A delivered shipment with COD pending keeps its assignment active until collection. Delivered without COD, COD collected, returned, or cancelled deactivates the assignment.
- COD is `NotRequired` for zero amount, otherwise `PendingCollection`; collection requires a delivered shipment. Settlement requires `Collected` and is an Admin permission in the current operation matrix.

### Routing and fees

- Route types are exactly `IntraProvince`, `IntraRegion`, and `InterRegion`.
- Chargeable weight is `max(actual, L × W × H / 5000)`.
- Total fee is base + extra-weight steps + insurance + return fee. Active, versioned fee rules select by route and optional weight range.
- Return fee is applied on transition to `Returned`. Persisted shipment address and fee breakdown are snapshots; do not recalculate historical shipments implicitly.

### Integrations and side effects

- Partner routes live under `/api/v1/partner`; API keys are Bearer tokens, Shop-bound, environment-prefixed, hashed at rest, and scope checked.
- Shipment creation uses `Idempotency-Key`; the same key and payload replays, a different payload conflicts.
- Business changes and outbox messages should commit in the same EF unit of work. Workers lease/retry messages; do not replace this with direct webhook calls inside a business transaction.
- Webhook destinations are HTTPS/public-host constrained, DNS-revalidated at connect time, signed, size/time bounded, and protected against redirects/proxies/SSRF. Preserve all layers.

For exact rules, use the aggregate and tests as source of truth: `Shipment.cs`, `CodTransaction.cs`, `FeeRule.cs`, `OperationPermissions.cs`, `ShopPermission.cs`, `PartnerApiScope.cs`, and their test files.
