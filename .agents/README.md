# Agent Context Map

The files in this directory contain only repository-specific context that is costly or risky to infer repeatedly. They are not a replacement for reading the affected source and tests.

| File | Read when |
|---|---|
| [`project.md`](project.md) | Always: product scope, layers, repository map, roles, and core invariants |
| [`standards.md`](standards.md) | Writing or reviewing code, API, EF Core, UI, tests, or security-sensitive behavior |
| [`workflows.md`](workflows.md) | Implementing a feature, fixing a bug, refactoring, reviewing, or preparing handoff |
| [`commands.md`](commands.md) | Restoring, building, running, testing, or creating migrations |
| [`known-issues.md`](known-issues.md) | Any cross-cutting change or when validation fails unexpectedly |

## Source-of-truth order

1. Executable behavior and tests.
2. Domain types, application use cases, EF configurations, migrations, and endpoint contracts.
3. This agent context.
4. README/progress reports, which may lag implementation.

Update the relevant agent document when a task intentionally changes a rule recorded here. Do not copy the same rule into several files.
