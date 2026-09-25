# Known Issues and Change Boundaries

Verified against the repository on 2026-09-25. These are context for scoping and testing, not permission to fix them during unrelated work. Re-check the code before acting because this file may age.

## Current issues

1. **Assignment capacity is not reserved atomically.** Concurrent auto-assignment requests can both observe the final shipper slot as available. The assignment uniqueness constraint is per shipment and does not protect aggregate shipper capacity; see `review-code.md` TASK-CI-06.
2. **Partner idempotency has concurrent-request gaps.** Re-check unique-key and transaction behavior before changing partner shipment creation; see `review-code.md` TASK-CI-07.
3. **Some distributed-looking state is process-local/non-atomic.** Fee/route/hub caches invalidate only the current process. Public tracking and Shop UI limiters use non-atomic distributed-cache get/set and are configured with memory cache. Multi-instance behavior is not equivalent to partner Redis rate limiting.
4. **Verified public tracking is not wired through the UI.** The application service supports phone-last-four verification, but the public component currently calls it without that value.
5. **Documentation drifts.** README progress/test counts and some historic reports may lag current code. Use source/tests/migrations and this context hierarchy rather than copying old counts or schema descriptions.

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
- The desired public-tracking verification UX.
