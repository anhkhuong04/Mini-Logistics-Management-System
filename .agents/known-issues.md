# Known Issues and Change Boundaries

Verified against the repository on 2026-09-24. These are context for scoping and testing, not permission to fix them during unrelated work. Re-check the code before acting because this file may age.

## Current issues

1. **OpenAPI design-time build starts hosted workers.** `OpenApiGenerateDocuments` is enabled in the Web project, while Infrastructure always registers outbox, webhook, retention and import workers. A default solution build can make LocalDB calls during document generation and fail. Use the documented property override until a dedicated design-time composition path is implemented.
2. **Blazor Interactive Server uses scoped repositories over scoped `MiniLogisticsDbContext`.** A Blazor scope is circuit-lived, so the context may be long-lived/stale and unsafe for overlapping events. Do not add more circuit-held EF state; a deliberate factory/per-operation unit-of-work migration needs focused tests.
3. **Business concurrency protection is incomplete.** `Shipment`, `CodTransaction` and mutable fee/route configuration lack optimistic concurrency tokens. Assignment capacity and partner idempotency use read-check-write flows. Any related change must consider two-context/request races and database conflict mapping.
4. **Shop registration crosses commit boundaries.** Identity user/role operations may commit before Shop persistence. Do not assume the use case is atomic; a fix needs transaction or compensation tests.
5. **Browser auth POST antiforgery metadata is missing.** Forms render tokens and middleware is present, but manually mapped register/login/logout POST handlers do not currently require/validate antiforgery metadata.
6. **Some distributed-looking state is process-local/non-atomic.** Fee/route/hub caches invalidate only the current process. Public tracking and Shop UI limiters use non-atomic distributed-cache get/set and are configured with memory cache. Multi-instance behavior is not equivalent to partner Redis rate limiting.
7. **Verified public tracking is not wired through the UI.** The application service supports phone-last-four verification, but the public component currently calls it without that value.
8. **Documentation drifts.** README progress/test counts and some historic reports may lag current code. Use source/tests/migrations and this context hierarchy rather than copying old counts or schema descriptions.

Detailed evidence and remediation options are recorded in root `review-code.md` when that local report is available.

## Areas not to refactor opportunistically

- The `Shipment` aggregate/state machine: large but cohesive and covered by domain tests.
- Partner API authentication/scope/tenant/idempotency/error contracts.
- Outbox leasing and webhook retry/signing/secret protection/SSRF transport controls.
- Production configuration guard, trusted proxy and persisted Data Protection setup.
- Existing migration history and model snapshot.
- Shop staff permissions, PII masking and audit boundaries.
- The existing UI token system in `wwwroot/app.css`.

Changes in these areas require an explicit task, end-to-end tracing and focused regression/security tests. Do not replace them with a new framework or pattern merely for consistency.

## Human decisions still needed

- Target deployment topology and whether multiple web/worker instances are required.
- Production SLOs, throughput/data-volume assumptions, retention/legal requirements and observability backend.
- Whether the stray root `.csproj` should be deleted and whether all progress-report Markdown files remain authoritative.
- The intended atomicity policy for Identity + Shop registration and the desired public-tracking verification UX.
