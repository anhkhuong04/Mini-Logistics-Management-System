# Agent Context Map

The files in this directory contain only repository-specific context that is costly or risky to infer repeatedly. They are not a replacement for reading the affected source and tests.

| File | Read when |
|---|---|
| [`project.md`](project.md) | Always: product scope, layers, repository map, roles, and core invariants |
| [`standards.md`](standards.md) | Writing or reviewing code, API, EF Core, UI, tests, or security-sensitive behavior |
| [`workflows.md`](workflows.md) | Implementing a feature, fixing a bug, refactoring, reviewing, or preparing handoff |
| [`commands.md`](commands.md) | Restoring, building, running, testing, or creating migrations |
| [`known-issues.md`](known-issues.md) | Any cross-cutting change or when validation fails unexpectedly |
| [`../docs/Frontend/history/refactorfe.md`](../docs/Frontend/history/refactorfe.md) | Archived frontend migration task order, interfaces and acceptance evidence |
| [Migration contract/status](../docs/Frontend/react-migration-contracts.md) | Frontend/browser API work: accepted direction, current implementation and remaining preparation |

## Source-of-truth order

User instructions and accepted change decisions define the intended scope. The following order resolves claims about the implementation that exists today; it does not make an existing Blazor convention override the user's React/TypeScript/Ant Design decision.

1. Executable behavior and tests.
2. Domain types, application use cases, EF configurations, migrations, and endpoint contracts.
3. This agent context.
4. README/progress reports, which may lag implementation.

Update the relevant agent document when a task intentionally changes a rule recorded here. Do not copy the same rule into several files.

Keep existing behavior, accepted target decisions, proposed implementation details and verified task completion distinct. The plan owns task sequencing; the migration contract/status owns readiness; the [validation log](../docs/Frontend/react-migration-validation.md) records checks actually run. Do not promote planned artifacts into the current repository map or supported commands before they exist.
