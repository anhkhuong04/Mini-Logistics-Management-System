# Project Context

## Product and runtime

Mini Logistics is a logistics operations modular monolith. It supports Shop shipment management, operational assignment and delivery, COD, public tracking, administration, partner REST integrations, outbox processing, and signed webhooks.

- Runtime: .NET 10, ASP.NET Core Blazor Web App with Interactive Server components.
- Data: SQL Server/LocalDB, EF Core Code First, ASP.NET Core Identity.
- Validation/testing: FluentValidation and xUnit.
- Distributed production concerns: Redis partner rate limiting and configuration-cache generations, shared persisted Data Protection keys, hosted outbox/webhook/import/retention workers.
- Logging uses `Microsoft.Extensions.Logging`; Serilog is not installed.

## Frontend migration direction

Phase 7 self APIs now exist under `/api/v1/app/shipper/me` for workspace/profile/working areas/UTC COD summary/availability/status/collection. Profile reads only the actor, capacity comes from aggregate counts, all IDs in browser command bodies are ignored in favor of principal/route. ShipperSelfService forces the internal RequireActiveShipperAssignment policy on shared status/COD commands so mixed Operations roles cannot bypass /me scope; existing callers retain their default policy. Shipper NotRequired search includes explicit zero-COD transactions plus legacy absent rows, with SQL paging regression coverage. React `/app/shipper/shipments` provides mobile cards, stages/URL filters/paging, COD summary, availability and confirmed status/collection commands. Per-shipment pending and unknown/409 guards require successful authoritative refresh; an older refresh cannot unlock a newer failure. Preserve legacy Shipper route and historical-assignment/UTC report semantics. Capacity retains existing ShipmentLoadStatuses (delivered COD-pending may retain assignment without consuming delivery load). Domain UpdateStatus(Cancelled) now ends active assignments, matching Cancel(); delivered positive COD ends assignment only after collection.

Phase 6 adds the React Operations workspace at `/app/operations/assignments` and alias `/app/operations/assignment`, with actor-authorized read/assignment/status/COD adapters under `/api/v1/app/operations`. Login/return URL and navigation now resolve these to React. List filters/page stay in the URL and SQL; capacity comes from aggregate counts. Assignment dialogs preserve area/availability warnings and server capacity enforcement. Bulk displays each outcome; unknown/409 mutations block replay until a successful authoritative refresh. COD settlement read/write retains current Admin/CodSettle boundary; history actor/GPS and collector identity require AuditView. Keep legacy Operations files untouched during coexistence. Phase 6 validation is tracked in the migration log; Shop settings/admin/integrations remain later phases.

The user has selected React + TypeScript + Ant Design to replace Blazor. Phase 1–7 provides scaffold/toolchain/CI, coexist hosting, theme/layouts, JSON Identity/CSRF, React auth/session/Shop context, dashboard, shipment list/detail and create/draft/edit/submit/cancel. Browser APIs under `/api/v1/app/shops/{shopId}` call authorized Application services; list uses server paging. Quote normalizes administrative divisions in Application and calls existing route/fee services. React form lives at `/app/shipments/create?shopId=...&shipmentId=...`; unconverted features still navigate to Blazor. Icons use the pinned `@ant-design/icons` package. See [migration contract/status](../docs/Frontend/react-migration-contracts.md) for readiness and [the plan](../refactorfe.md) for task details.

Shipment commands use the existing ManageShipments/active Shop policies. Editing UI additionally requires ViewFullPii and ViewCod because the current detail DTO otherwise contains masked values; never prefill and save masked snapshots. Browser create/draft use the same actor limiter/store as Blazor, which retains its limiter. Unknown mutation outcomes require checking authoritative state before retry; 409 requires a successful reload. Quote preview is advisory, never a command fee. Browser input guards mirror existing SQL decimal capacity without changing Domain rounding or Partner contracts.

Phase 5 adds Shop import preview/confirm/batch APIs under `/api/v1/app/shops/{shopId}/shipment-imports`, plus a React panel on the shipment list. CSV is UTF-8, at most2,000,000 file bytes/500 rows; only import overrides foundation64KiB to a3,000,000-byte request limit. Confirm revalidates typed draft rows in Application, existing worker processes batches. Preview adds optional InvalidNumericFields from parsing; malformed numeric cells must be corrected explicitly instead of confirming parser fallback0. Poll every2s until terminal or read failure, abort/ignore stale user/Shop/batch responses; collapse retains pending/unknown confirmation guards. Do not replay uncertain confirmations. Cookie downloads keep `/shop/files` routes and legacy errors, verify MIME and revoke object URLs; ExportData/PII/COD permissions remain enforced in Application. Text CSV cells neutralize spreadsheet formulas while preserving escaping/headers.

`src/MiniLogistics.Frontend` provides opt-in `/app` hosting, strict HTTPS Vite proxy and conditional publish. Browser auth endpoints live under `/api/v1/app/auth`, context under `/api/v1/app/shop-context`, public provinces under `/api/v1/public/reference-data/provinces`. Browser-only converters/errors/OpenAPI do not apply to Partner endpoints. Session/cache must clear on logout, 401 or changed user; Shop queries key by user/Shop and abort stale requests. Application remains the owner of authorization/orchestration and Domain of invariants; Web is the composition root. Do not delete Blazor components or application operation scopes during coexistence. Runtime workers remain enabled normally; OpenAPI generation and isolated `BrowserTests` omit Infrastructure hosted services.

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

Application service interfaces registered by `AddApplication` resolve the implementation and scoped dependencies in an async child scope per call. Nested application calls in the same operation reuse that scope; the scope is disposed when the returned `Task` completes. Do not cache EF-backed results/services between calls, and keep passing cancellation tokens through the full operation.

## Repository map

- `src/MiniLogistics.Domain/Shipments`: `Shipment` aggregate, assignment/history/proof, status and route types.
- `src/MiniLogistics.Domain/CashOnDelivery`, `Fees`, `Operations`, `Shops`, `PartnerApi`, `Outbox`: supporting domain models.
- `src/MiniLogistics.Application/Shipments`, `CashOnDelivery`, `Shops`, `Shippers`, `PartnerApi`, `Admin*`: use cases and ports.
- `src/MiniLogistics.Infrastructure/Persistence`: DbContext, configurations, repositories, migrations, caches, audit, seeding and import worker.
- `src/MiniLogistics.Infrastructure/PartnerApi` and `Outbox`: secure webhook transport, retention, dispatch and workers.
- `src/MiniLogistics.Web/Components`: role-oriented Blazor UI.
- `src/MiniLogistics.Web/Endpoints`: browser auth, partner API, localization and Shop file downloads.
- `test`: unit, application, SQL LocalDB integration, web contract and load tests. Manual reports are deleted in the current working tree; do not restore them as part of migration preparation. New migration QA evidence belongs in the [validation log](../docs/Frontend/react-migration-validation.md).
- `docs/Backend`, `docs/Frontend`, and `docs/Service`: developer documentation grouped by responsibility; `docs/README.md` is the human entry point. Partner contracts, generated OpenAPI, and examples live in `docs/Service`; keep them aligned with `postman` when APIs change.

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
