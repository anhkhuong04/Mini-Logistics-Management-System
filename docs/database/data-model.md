# Data Model and Persistence Guarantees

## Storage model

The application uses one SQL Server database through `MiniLogisticsDbContext`, which also hosts ASP.NET Core Identity tables. EF Core migrations under `src/MiniLogistics.Infrastructure/Persistence/Migrations` are the schema history and model snapshot.

The main data groups are:

| Group | Principal records | Important relationships |
| --- | --- | --- |
| Identity and shop | Identity users/roles, `Shop`, staff memberships, preferences, notifications | A shop has one owner; memberships grant per-shop staff access |
| Shipment operations | `Shipment`, assignments, status histories, delivery proofs, import batches/rows | Shipment owns assignment/history; proof and import records reference it |
| COD and pricing | `CodTransaction`, `FeeRule`, `RouteRegionConfig` | One COD transaction per shipment; fee/config records are selected at creation |
| Dispatch | `Hub`, `ShipperWorkingArea` | Working areas connect shippers to hub/province/ward/zone coverage |
| Partner integration | `ApiClient`, external references, webhook endpoints/deliveries, request/credential audits, integration scopes | Client belongs to a shop; references bind a client request to a shipment |
| Reliability and audit | `OutboxMessage`, `AdminAuditLog` | Outbox drives asynchronous work; admin audit captures privileged changes |

## Important database constraints

- `Shipment.TrackingCode` is unique.
- A filtered unique index permits only one active assignment (`UnassignedAtUtc IS NULL`) per shipment.
- A shipment has at most one COD transaction.
- Partner idempotency key and external order ID are each unique within an API client.
- API key hashes are unique; raw API keys are not stored.
- Import row number is unique within its batch.
- Shop staff/preference relationships use compound uniqueness to avoid duplicate effective membership/preferences.
- Most business relationships use restrictive deletes; shipment-owned history/assignments and COD use deliberate cascades.
- Outbox and webhook-delivery rows have due-queue indexes and SQL row-version concurrency tokens.

Consult the current entity configurations and generated migration before relying on column names, lengths, or every delete behavior.

## Snapshots versus live configuration

A shipment persists the values required to explain the original decision: route type, actual and chargeable weight, fee breakdown, total, and return-fee rate. Changing an active fee rule later must affect new calculations, not rewrite historical shipments.

External shipment references also persist request hash and response snapshot so an exact idempotent replay returns the original representation. Webhook deliveries persist the protected signing-secret snapshot/version used for queued work.

## Unit of work and transactions

Repositories registered in one scope share one DbContext. Changes tracked through shipment, COD, audit, external-reference, and outbox repositories are atomic when the application completes them with a single `SaveChangesAsync` call.

Some workflows explicitly open an `IApplicationDbTransaction` when they must coordinate multiple saves. This is not a global behavior. Before changing a use case, inspect every `SaveChangesAsync`, caller, and downstream action to establish its real transaction boundary.

Notable current behavior:

- Shipment creation saves the shipment and COD together, then attempts auto-assignment in a later save. Creation can succeed while assignment does not.
- Partner creation saves shipment, COD, client reference, and initial outbox data together. Auto-assignment and a possible response-snapshot refresh happen afterward.
- Status changes, notification/webhook outbox entries, and audit data are tracked before one save.
- Identity-backed registration or administration may cross Identity and domain repositories; do not assume one atomic commit without verifying the service.

## Concurrency model

Guaranteed today:

- Database uniqueness is the last line of defense for tracking codes, active assignment, and partner idempotency/external-order pairs.
- Outbox and webhook deliveries use row versions plus atomic SQL lease/claim queries, allowing expired work to be reclaimed.
- Partner API distributed rate limiting uses atomic Redis operations in production mode.

Not guaranteed globally:

- `Shipment`, `CodTransaction`, fee/configuration entities, and most admin records do not have row-version tokens.
- Several capacity/idempotency paths perform a read/check/write sequence and must handle database races explicitly.
- A Blazor Interactive Server scoped DbContext may live for the circuit lifetime; avoid parallel use and stale tracking assumptions.

Add concurrency behavior only with a defined conflict policy and tests. A token without user/application conflict handling is incomplete.

## Migration rules

- Generate migrations through EF tooling; do not hand-edit existing migration history or the model snapshot.
- Review the migration, designer, and snapshot together, including indexes, filters, defaults, backfill, and delete behavior.
- Test upgrades against a representative prior schema for destructive or data-transforming changes.
- Application startup migrates only when explicitly invoked with `--migrate`; production deployment owns when and how that command runs.
- Never run update/remove/destructive migration commands against an environment that was not explicitly placed in scope.

Commands are maintained in [Local development](../development/local-development.md).
