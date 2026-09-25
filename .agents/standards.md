# Engineering Standards

These are repository-specific rules. Match nearby code when a formatting/detail rule is not stated here.

## C# and design

- Target `net10.0` with nullable reference types and implicit usings enabled.
- Use file-scoped namespaces, clear domain names, constructor injection, and sealed classes where extension is not intended, consistent with surrounding code.
- Keep a change inside the existing feature slice. Reuse an established service, validator, repository, DTO, error, and test pattern before creating another abstraction.
- Expected validation/business/authorization failures return `Result`/typed errors. Unexpected technical failures may throw and must be handled/logged at the correct boundary; do not swallow exceptions or return null as failure.
- Use async APIs for I/O, propagate `CancellationToken`, and use injected `TimeProvider` for business/auditable timestamps so tests remain deterministic.
- Do not create interfaces only to satisfy a style rule. Add one for a cross-layer port, multiple implementations, or a valuable test seam. Do not introduce a generic repository or mediator without a task-specific reason.
- Comments explain a non-obvious invariant, security decision, or operational trade-off—not what the code already says.

## Domain and Application

- Domain entities protect state with behavior methods/private setters. Add a rule to Domain when it must hold across every entry point; keep request-shape validation in FluentValidation.
- Application services orchestrate authorization, tenant/scope checks, repositories, domain behavior, outbox/audit/notifications, and one clear save/transaction boundary.
- Never trust a caller-provided Shop, shipper, API-client, role, price, route, fee, or status without resolving/validating it server-side.
- Return DTOs/read models outside Application; do not expose tracked EF entities to Web.
- Required side-effect dependencies must not silently become optional. Follow existing Null Object use only when backward-compatible behavior is deliberate and tested.
- Do not duplicate shipment creation or transition rules across UI, partner API, import, and background paths. Prefer a shared focused policy/workflow when a task touches divergent behavior.

## Web and UI

- Razor components handle presentation state, input and calls to Application services. They do not resolve repositories or `MiniLogisticsDbContext`.
- Keep authorization in the use case even when a component/page has role gating.
- Use `EditForm`, validation messages, loading/disabled states, accessible labels, keyboard focus, and text plus color for status.
- Reuse CSS variables and shared primitives in `src/MiniLogistics.Web/wwwroot/app.css`; primary typography is Be Vietnam Pro and the existing sea-blue operational theme. Avoid one-off palettes, inline style systems, large decorative layouts, and premature component extraction.
- Interactive Server services may outlive an HTTP request. Do not retain request-only state or assume `HttpContext` remains current throughout a circuit.

## HTTP/API conventions

- Partner endpoints remain versioned below `/api/v1/partner` and use `Authorization: Bearer <api-key>`.
- Authenticate before resolving tenant data; then enforce API scope and Shop ownership in the use case/query.
- Use the existing partner error envelope with stable error code, safe description, and trace ID. Map validation/authorization/not-found/conflict/rate/unavailable failures to the existing 4xx/5xx semantics.
- Partner shipment creation requires `Idempotency-Key`; preserve `201` for a new resource and `200` for a valid replay. Treat unique-constraint races as part of the idempotency contract.
- Keep endpoint OpenAPI metadata and partner docs/Postman artifacts aligned when contracts change.
- Browser state-changing form endpoints require antiforgery validation. APIs using non-cookie credentials need an explicit documented CSRF model.
- Apply body limits, rate limits and safe logging at ingress. Never include credentials, webhook secrets, unmasked PII, or raw sensitive request bodies in logs/errors.

## EF Core and database

- SQL Server Code First is authoritative. Configure entities with `IEntityTypeConfiguration<T>` and Fluent API; do not add persistence attributes to Domain.
- Preserve `DateTimeOffset`/UTC semantics, explicit decimal precision, enum string conversions, max lengths, foreign keys and indexes consistent with existing configurations.
- Use `AsNoTracking` and projection for read-only/list queries. Page/filter/order in SQL; do not hide full-table reads in default interface methods or filter large sets in memory.
- Avoid including multiple collections in list queries. Load the aggregate needed for a command; use DTO projection or split/specialized queries for reads.
- Database constraints are the final protection for uniqueness, but pre-checks do not handle races. Catch/map the relevant database conflict and add concurrency tests where correctness depends on it.
- Keep all state plus outbox/audit data that must be atomic inside one transaction/unit of work. Do not call `SaveChanges` midway unless the partial commit is intentional and recoverable.
- Add indexes from observed query shape, not speculation. For query changes with scale impact, inspect generated SQL/query plan and add a representative integration test.
- Create migrations only through EF tooling. Review migration, designer and snapshot together; never edit or reorder old applied migrations.

## Testing

- Place tests in the matching project and name them by observable behavior. Follow existing xUnit style.
- Domain tests cover state machines, calculations and invariants without mocks.
- Application tests cover orchestration, validation, authorization/data isolation, side effects and failure paths.
- Infrastructure tests cover mappings, constraints, transactions, query behavior, leases, security transports and migrations against SQL Server LocalDB where relevant.
- Web tests cover endpoint contract/status/error shape, middleware/policy configuration and production guards. Use concurrency/integration tests for races; in-memory fakes cannot prove database atomicity.
- Every bug fix needs a regression test that fails for the root cause. Do not test framework getters or duplicate assertions solely to increase coverage.
- Infrastructure tests currently require Windows LocalDB. State that limitation when they cannot run; do not silently replace them with weaker in-memory assertions.

## Security and operations

- Enforce least privilege and tenant isolation at every data access/use-case boundary. Preserve PII masking by permission.
- Secrets belong in environment/secret stores. API keys are shown once, hashed at rest; webhook secrets remain protected by Data Protection. Never weaken the production configuration guard to make local tests pass.
- Preserve webhook URL validation, DNS/connect-time checks, HTTPS/port policy, no-redirect/no-proxy behavior, signing, timeout and response limits.
- Rate-limit public/auth/partner abuse paths with a store and atomicity appropriate to the deployment topology. Do not assume an in-memory cache coordinates multiple instances.
- Treat CSV/spreadsheet exports, file names, URLs and log fields as untrusted output as well as input.
- Use structured `ILogger<T>` messages. Include identifiers/trace IDs useful for operations, but exclude secrets and unnecessary PII.
