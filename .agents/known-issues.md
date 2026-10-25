# Known Issues and Change Boundaries

Verified against the repository on 2026-09-25. These are context for scoping and testing, not permission to fix them during unrelated work. Re-check the code before acting because this file may age.

Migration-specific notes below were rechecked against source on 2026-10-25. That check does not imply the older runtime/concurrency suites were rerun.

## Current issues

1. **Public tracking and Shop UI limiters remain process-local/non-atomic.** Route, fee and hub configuration caches now validate local snapshots against shared Redis generations in Production, with SQL fallback when Redis is unavailable. Public tracking and Shop UI limiters still use non-atomic distributed-cache get/set backed by memory and are not multi-instance safe.
2. **Verified public tracking is not wired through the UI.** The application service supports phone-last-four verification, but the public component currently calls it without that value.
3. **Documentation drifts.** README progress/test counts and some historic reports may lag current code. Use source/tests/migrations and this context hierarchy rather than copying old counts or schema descriptions.
4. **Banner administration needs a use-case boundary before HTTP exposure.** `Application/Banners/BannerService.cs` does not receive/check an actor for administrative actions; `AdminBanners.razor` currently supplies the Admin page gate and writes uploaded files directly. T09.04 must add use-case authorization and Infrastructure storage before exposing those actions as browser endpoints. This is a migration prerequisite for that feature, not a claim that an anonymous administrative HTTP endpoint already exists.
5. **Internal reads are not browser entry points.** Phase 6 adds an actor-authorized Operations read facade before HTTP exposure; raw GetOperationsShipments/GetPendingPickupShipments/GetCodSettlementCandidates/GetActiveShippers remain internal/legacy signatures. Settlement workspace requires CodSettle, preserving current Admin-only UI. OperationsActionService authorizes manual auto-assign before calling the internal system-capable service and passes requestedByUserId for audit. GetLogisticsHubs still needs an authorized entry point in T09.02; Phase 7 shipper profile must be self-only, never all active shippers.

## Migration preparation status

- React/TypeScript/Ant Design is the selected target. Phase 1–5 provides opt-in `/app`, JSON Identity/CSRF, auth/Shop context, dashboard, shipment reads and shipment form/actions. Other business renderers still run Blazor until their feature task. Read [migration contract/status](../docs/Frontend/react-migration-contracts.md) before interpreting future endpoints as implemented.
- Manual-report files under `test/manual` are deleted in the working tree. Their old links have been removed from current guides; do not restore those user deletions during this work. Migration evidence belongs in [the validation log](../docs/Frontend/react-migration-validation.md).
- Keep the summary-only public tracking behavior and current server-timezone semantics until an explicit behavior change is scoped. Moving browser rate-limit checks from components to ingress does not itself solve multi-instance limiter atomicity.
- Phase 0 baseline: 320 tests passed, none failed, one Redis integration skipped because its environment variable was absent; see validation log for the exact run, not a permanent test-count claim. Phase 1 installed/verified pinned frontend dependencies using portable Node24.22.1/npm11.19.1; global Node was not changed. BrowserTests omits Infrastructure workers for isolated shell/routing smoke, not data/auth workflow validation.

## Resolved in this branch

Phase 2–5 browser validation uses an opt-in Kestrel/Identity/Application fixture with isolated EF InMemory; it is not SQL transaction evidence. Existing LocalDB suites remain authoritative there. The AntD bundle is988.21kB (gzip314.50kB) after Phase 5 and retains a >500kB warning; maxSize splitting caused a production initialization failure in Phase 2 and was reverted. Keep route-based performance work scoped separately. Import/download now have React controls; old Blazor paths/file URLs remain. Detail DTO has no assignment/proof projection. Editing masked detail is blocked unless ViewFullPii and ViewCod are granted. Browser ingress guards current SQL decimal capacity, but exceptional fee configurations and legacy import precision limits still need use-case hardening outside the frontend migration; no schema or broad fee-policy rewrite is included.

Import parser numeric fallback0 is legacy behavior; Phase 5 adds structured InvalidNumericFields so React requires explicit numeric correction. Legacy mixed-validity Blazor confirmations do not consume that metadata; no silent business-policy rewrite was made. Preview counters currently exclude edited rows from valid/invalid counts; edited tags distinguish pending revalidation. File endpoints retain legacy400/redirect responses; React shows safe generic download errors rather than treating legacy errors as files.

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
- The existing UI token system in `wwwroot/app.css`: preserve it for Blazor maintenance and use its design intent when mapping React Ant Design tokens; the selected migration permits that mapping, not an unrelated brand redesign.

Changes in these areas require an explicit task, end-to-end tracing and focused regression/security tests. Do not replace them with a new framework or pattern merely for consistency.

## Human decisions still needed

- Target deployment topology and whether multiple web/worker instances are required.
- Production SLOs, throughput/data-volume assumptions, retention/legal requirements and observability backend.
- Whether the stray root `.csproj` should be deleted and the retention policy for historical reports under `docs/Backend/history` and `docs/Service/history`.
- The desired public-tracking verification UX.
