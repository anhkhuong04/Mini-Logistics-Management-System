# Known Issues and Change Boundaries

Verified against the repository on 2026-09-25. These are context for scoping and testing, not permission to fix them during unrelated work. Re-check the code before acting because this file may age.

Migration-specific notes below were rechecked against source on 2026-10-25. That check does not imply the older runtime/concurrency suites were rerun.

## Current issues

Phase 9 configuration note: `RouteRegionConfigRepository` uses the built-in province map only when no active configuration rows exist; any configured map replaces that fallback in full. This is existing behavior, not a merge per province. Prepare complete province coverage before switching from the fallback. React explains the first-configuration boundary; the browser fixture seeds the full default map so its Admin configuration scenario does not invalidate other shipment scenarios.

1. **Public tracking and Shop UI limiters remain process-local/non-atomic.** Route, fee and hub configuration caches now validate local snapshots against shared Redis generations in Production, with SQL fallback when Redis is unavailable. Public tracking and Shop UI limiters still use non-atomic distributed-cache get/set backed by memory and are not multi-instance safe.
2. **Verified public tracking is not wired through the UI.** The Application service supports phone-last-four verification, but React and its public HTTP adapter deliberately call it with null and expose only a safe summary. No verification form is rendered.
3. **Documentation drifts.** README progress/test counts and some historic reports may lag current code. Use source/tests/migrations and this context hierarchy rather than copying old counts or schema descriptions.
4. **Banner boundary addressed in Phase 9.** Admin list/mutations/upload now require active Admin in Application; Retained compatibility callers use the same services; file I/O is in Infrastructure. Deployment must configure stable absolute `BannerImages:RootPath` outside release, preserve old filenames/URLs and back up files. Existing six formats remain; SVG is restricted to a safe XML subset (no scripts/events/external resources/style), raster validation checks signatures rather than full decoding. Legacy files must be copied before retiring their release. Metadata deletion retains image files; automatic orphan cleanup is separate scope.
5. **Internal reads are not browser entry points.** Phase 6 adds an actor-authorized Operations read facade before HTTP exposure; raw GetOperationsShipments/GetPendingPickupShipments/GetCodSettlementCandidates/GetActiveShippers remain internal/legacy signatures. Settlement workspace requires CodSettle, preserving current Admin-only UI. OperationsActionService authorizes manual auto-assign before calling the internal system-capable service and passes requestedByUserId for audit. Phase9 uses actor-authorized GetAdminHubs for browser lookup; raw GetLogisticsHubs remains internal; Phase 7 ShipperSelfService now provides a self-only profile using actor-specific Identity summary and aggregate load.

## Migration preparation status

- React/TypeScript/Ant Design is the runtime frontend after Phase11 root cutover. appsettings enables root hosting; `/app/...` redirects to root. No Blazor runtime is registered after cleanup. Nine pre-existing user-modified Operations files remain excluded source. Read [migration contract/status](../docs/Frontend/react-migration-contracts.md) and validation evidence for checkpoint status.
- Manual-report files under `test/manual` are deleted in the working tree. Their old links have been removed from current guides; do not restore those user deletions during this work. Migration evidence belongs in [the validation log](../docs/Frontend/react-migration-validation.md).
- Keep the summary-only public tracking behavior and current server-timezone semantics until an explicit behavior change is scoped. Moving browser rate-limit checks from components to ingress does not itself solve multi-instance limiter atomicity.
- Direct Shop create navigation without `shopId` can briefly render before ShopContextProvider normalizes the URL, then remount during its second context read. Phase 7 browser QA observed receiver input clearing in that window. Cross-role tests preserve selected `shopId`, matching normal navigation; a general Shop form/context lifecycle correction remains outside Phase 7.
- Phase 0 baseline: 320 tests passed, none failed, one Redis integration skipped because its environment variable was absent; see validation log for the exact run, not a permanent test-count claim. Phase 1 installed/verified pinned frontend dependencies using portable Node24.22.1/npm11.19.1; global Node was not changed. BrowserTests omits Infrastructure workers for isolated shell/routing smoke, not data/auth workflow validation.

## Resolved in this branch

Phase10 provides scoped browser integration management. Existing semantics remain: once any active management scope exists, Admin/IntegrationAdmin use global/Shop/province grants; with no scopes configured they use the existing global fallback. Environment is host-level, not a per-client dropdown. Webhook upsert reactivates; service has no standalone endpoint deactivation or stored secret reveal/rotation API. React exposes those exact capabilities, with write-only signing input and transient one-time API key modal. Dashboard history remains bounded (10 delivery/audit per client;100 global failures then scope filtering), not complete delivery history. Browser projection replaces unsafe error text, preserves safe metadata, and never includes raw payload/protected signing secret. Integration selector no longer inherits the shipment-only default Shop normalization. Backend Partner contracts/transport/persistence are unchanged; real-cookie tests queue events only, with hosted dispatch workers omitted.

Phase 8 exposes existing Shop settings/reporting workflows through actor-authorized adapters. Staff remains owner-only (Manager permission flags do not imply ownership); response/UserId is the update target, MembershipId is audit/persistence identity. Profile UI avoids editing masked snapshots; additional Shop uses canonical administrative divisions in Application. COD report/totals still cover at most10000 newest rows, audit SQLlimit<=500 and per-user notification SQLlimit<=200; these are bounded windows, not full-history financial totals or new paged protocols. Changing those reporting semantics is separate from renderer migration. ExportData remains independent from ViewCod/ViewAudit/ManageNotifications. Staff temporary passwords never enter query cache/storage. SPA mutations still require CSRF; backend permission semantics remain after renderer retirement.

Phase 7 adds self-scoped Shipper APIs/mobile workspace and cross-role lifecycle coverage. `Shipment.UpdateStatus(Cancelled)` now deactivates assignments, matching the existing `Cancel()` invariant; tests first reproduced both Assigned and PickingUp cases. Delivered positive COD keeps assignment until collection; zero-COD Delivered/Returned end it. Load counts retain existing ShipmentLoadStatuses rather than counting every active assignment. COD daily summary retains historical-assignment membership and UTC business dates. Notification evidence in the browser fixture is queued outbox events; workers are omitted, so it does not prove downstream delivery. No schema/Partner changes.

Phase 2–5 browser validation uses an opt-in Kestrel/Identity/Application fixture with isolated EF InMemory; it is not SQL transaction evidence. Existing LocalDB suites remain authoritative there. The AntD bundle is1007.82kB (gzip320.03kB) after Phase 8 and retains a >500kB warning; maxSize splitting caused a production initialization failure in Phase 2 and was reverted. Phase11 lazy loads feature routes and checks that public navigation does not fetch Admin/Integration chunks; vendor size remains a known limitation. Import/download now have React controls; file URLs remain and old `/app` deep links redirect to React root. Detail DTO has no assignment/proof projection. Editing masked detail is blocked unless ViewFullPii and ViewCod are granted. Browser ingress guards current SQL decimal capacity, but exceptional fee configurations and legacy import precision limits still need use-case hardening outside the frontend migration; no schema or broad fee-policy rewrite is included.

Import parser numeric fallback0 is legacy behavior; Phase 5 adds structured InvalidNumericFields so React requires explicit numeric correction. React consumes that metadata; no silent business-policy rewrite was made. Preview counters currently exclude edited rows from valid/invalid counts; edited tags distinguish pending revalidation. File endpoints retain legacy400/redirect responses; React shows safe generic download errors rather than treating legacy errors as files.

- **TASK-CI-06 — shipper capacity reservations.** Assignment paths lock the shipper's `AspNetUsers` row in the database transaction, read the current limit and active load under that lock, and commit assignment/outbox effects before releasing it. Automatic assignment allows three total attempts; manual assignment and reassignment reject a full shipper. See the focused SQL Server concurrency test.
- **TASK-CI-07 — partner create idempotency.** Partner create now holds one database transaction through shipment/reference/outbox creation, auto-assignment and final response snapshot. The capacity guard joins that transaction. Only the two external-reference unique indexes are translated; a losing request rolls back, clears tracking and reloads the winner for replay/conflict. SQL Server integration tests cover identical payload, different payload and external-order races without duplicate shipment, reference or outbox rows.
- **TASK-CI-08 — serialized route/fee configuration versions.** Admin writes take a transaction-owned SQL application lock per province or route type, deactivate old rows before inserting the next version, commit audit/configuration together, then invalidate local caches. Unique version and filtered unique active indexes enforce the invariant in SQL Server; concurrency and cache-refresh behavior is covered by LocalDB integration tests.
- **TASK-CI-09 — cross-instance configuration cache consistency.** Production route/fee/hub caches check shared Redis generation keys and keep only short-lived local snapshots; Redis outages fall back directly to SQL, while failed post-commit invalidation is logged and bounded by `ConfigurationCache:ConsistencyWindowSeconds`. New shipments persist route configuration and fee rule identifiers/versions. The two-provider Redis integration test runs when `MINILOGISTICS_TEST_REDIS` is configured.

Detailed evidence and remediation options are recorded in [historical review](../docs/Backend/history/review-code.md); re-check findings against current code before using them.

## Areas not to refactor opportunistically


- The `Shipment` aggregate/state machine: large but cohesive and covered by domain tests.
- Partner API authentication/scope/tenant/idempotency/error contracts.
- Outbox leasing and webhook retry/signing/secret protection/SSRF transport controls.
- Production configuration guard, trusted proxy and persisted Data Protection setup.
- Existing migration history and model snapshot.
- Shop staff permissions, PII masking and audit boundaries.
- The existing sea-blue/Be Vietnam Pro design intent is mapped to React Ant Design tokens; do not introduce an unrelated brand redesign.

Changes in these areas require an explicit task, end-to-end tracing and focused regression/security tests. Do not replace them with a new framework or pattern merely for consistency.

## Human decisions still needed

- Target deployment topology and whether multiple web/worker instances are required.
- Production SLOs, throughput/data-volume assumptions, retention/legal requirements and observability backend.
- Whether the stray root `.csproj` should be deleted and the retention policy for historical reports under `docs/Backend/history` and `docs/Service/history`.
- The desired public-tracking verification UX.
