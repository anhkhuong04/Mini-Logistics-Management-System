# Agent Workflows

## Common preparation

1. Restate the requested outcome and exclusions; do not expand scope silently.
2. Read `AGENTS.md`, `project.md`, the relevant sections of `standards.md`, and `known-issues.md`.
3. Check `git status`; preserve user changes and separate unrelated diffs.
4. Trace the affected path end to end: entry point → application use case → domain behavior → repository/configuration → side effects → tests.
5. Search for all callers, sibling implementations, contracts, migrations, docs and tests before choosing the change shape.
6. Prefer the current local pattern unless it causes the reported defect. Record assumptions that materially affect behavior.

## Implement a feature

1. Define acceptance cases, authorization/tenant rules, business invariants, failure codes and persistence/side effects.
2. Put each concern in its owning layer; avoid a new abstraction if the existing feature pattern is sufficient.
3. Extend Domain behavior before orchestration when a universal invariant changes.
4. Add Application validation and authorization independent of UI/API checks.
5. Add Infrastructure mapping/migration only when persisted shape changes; keep business state and outbox/audit atomic.
6. Update Web/API and public contract artifacts only as required.
7. Test success, invalid input, unauthorized/cross-tenant access, important failure, and concurrency when shared mutable state is involved.
8. Run targeted validation, then the affected solution baseline.

## Fix a bug

1. Reproduce or establish concrete code-path evidence; distinguish observed defect from hypothesis.
2. Add the narrowest regression test at the layer where the failure originates.
3. Fix the root cause without changing unrelated behavior or public contracts.
4. Check equivalent entry points (Shop UI, Partner API, import, worker) only where they share the same rule.
5. Run the regression test, nearby suite, then build/test scope. Document anything not reproducible locally.

## Refactor

1. Confirm the task explicitly permits refactoring and identify the measurable benefit.
2. Establish behavioral coverage before moving boundaries.
3. Keep schema, HTTP contracts, authorization, events, audit and timing behavior unchanged unless explicitly in scope.
4. Refactor in small compilable steps; avoid simultaneous framework/pattern replacement.
5. Do not add indirection merely to reduce file length or satisfy a design-pattern label.
6. Compare before/after behavior and inspect the final diff for accidental expansion.

## Code review

1. Do not modify code unless the request includes remediation.
2. Review correctness and security before style: authorization/data isolation, domain transitions, transaction boundaries, concurrency, idempotency, outbox/event consistency, query shape, secret/PII handling, then maintainability.
3. Trace at least the main success and failure flows across layers; inspect tests as evidence, not as proof of untested concurrency/runtime behavior.
4. Classify findings as confirmed defect, conditional risk, or improvement. Include severity, exact file/symbol, consequence and concrete remediation.
5. Avoid recommending a rewrite or new framework when a focused change solves the issue. Call out good design that should be preserved.

## Database change

1. Change Domain/Application shape first, then EF configuration.
2. Generate the migration using `.agents/commands.md`; inspect destructive/default/backfill behavior, indexes, constraints and snapshot.
3. Test upgrade behavior against SQL Server. Do not rely only on `EnsureCreated` or EF InMemory.
4. Never modify an already applied migration unless the task explicitly establishes that it is unreleased and safe.

## Definition of Done

- Requested behavior and explicit acceptance cases are satisfied; no unrelated refactor is included.
- Layer dependency, authorization, tenant isolation and domain invariants remain valid.
- Validation/error/status semantics match the entry point.
- Persistence changes include reviewed migration/constraints/indexes and a safe transaction/concurrency story.
- Outbox, audit, notification, webhook and cache effects were considered where relevant.
- Tests cover changed behavior and important negative paths; relevant commands ran successfully or the exact blocker is reported.
- API docs/Postman/user docs and agent context are updated only when their contract/rule changed.
- `git diff` contains no secrets, generated output, local artifacts or accidental user-file changes.
- Handoff states what changed, validation performed, and remaining risk/gap.
