# Agent Entry Point

This repository is a .NET 10 modular monolith for shipment operations. Treat this file as the entry point, not as complete project documentation.

## Read first

1. Read [`.agents/project.md`](.agents/project.md) for architecture, repository layout, roles, and business invariants.
2. Read [`.agents/standards.md`](.agents/standards.md) for the layer affected by the task.
3. Use [`.agents/workflows.md`](.agents/workflows.md) for implementation, bug-fix, refactor, review, and Definition of Done.
4. Use [`.agents/commands.md`](.agents/commands.md) for supported commands.
5. Check [`.agents/known-issues.md`](.agents/known-issues.md) before broad or cross-cutting work.

## Non-negotiable rules

- Make the smallest coherent change that satisfies the task. Do not perform adjacent cleanup or redesign unless requested.
- Inspect the current implementation, callers, persistence mapping, and relevant tests before editing. When documentation conflicts with code/tests/migrations, report the conflict and follow the verified current behavior.
- Preserve dependency direction: Domain has no project dependency; Application depends on Domain; Infrastructure depends on Application and Domain; Web is the composition root and may depend on Application and Infrastructure.
- Keep business invariants in Domain. Keep orchestration, authorization, and validation in Application. Keep EF Core, Identity, external I/O, and workers in Infrastructure. Razor components and endpoints must not query `MiniLogisticsDbContext` directly.
- Enforce authorization and Shop/API-client data isolation in the use case, not only in UI visibility.
- Do not add a package, architectural pattern, generic repository, mediator, service boundary, or abstraction without a concrete need in the task.
- Do not manually rewrite existing migration history or the EF model snapshot. Generate schema changes through EF tooling and review all generated files.
- Preserve partner API contracts, idempotency behavior, outbox atomicity, webhook signing/SSRF controls, audit trails, and PII masking unless the task explicitly changes them.
- Never commit secrets, demo passwords, generated binaries, downloaded packages, build output, logs, or local connection details.
- Do not use the stray root `Mini-logistics-manegemant-system.csproj`; build and test `Mini-logistics-manegemant-system.slnx` or a specific project.

## Validation baseline

- Add or update tests for changed behavior and important failure/authorization paths.
- Run the narrowest relevant tests first, then build/test the affected solution scope.
- The current OpenAPI design-time issue requires `-p:OpenApiGenerateDocuments=false` for a reliable build/test; see `.agents/known-issues.md`.
- Before handoff, inspect `git diff` and confirm no generated or unrelated files were changed.
