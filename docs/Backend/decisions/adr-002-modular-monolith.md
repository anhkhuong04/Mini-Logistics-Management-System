# ADR-002: Modular Monolith with Inward Dependencies

- Status: Accepted
- Date: 2026-09-24
- Owners: Backend architecture

## Context

Shipment status, assignment, fee snapshots, COD, shop access, and partner events change together and require strong consistency. The system is small enough to deploy and operate as one application, while still needing clear boundaries so UI, persistence, and business policy do not become coupled.

## Decision

Use a modular monolith split into four projects with inward dependencies:

- Domain owns business state and invariants and has no project dependency.
- Application owns use cases, authorization, validation, and interfaces required from infrastructure.
- Infrastructure implements persistence, Identity, external I/O, cryptography, Redis, and background processing.
- Web is the composition root and owns Blazor/HTTP transport concerns.

Features are organized primarily by use case inside Application rather than behind generic repositories, a mediator, or a service-per-entity layer. EF Core repositories implement the specific ports required by those use cases.

## Consequences

Positive:

- Cross-feature changes can commit atomically in one SQL database.
- Domain rules can be tested without ASP.NET Core or EF Core.
- Transport and persistence can change without moving business invariants out of Domain.
- Deployment, local development, and operational tracing remain simple.

Trade-offs:

- Module isolation is enforced by project references and review, not by process boundaries.
- A scoped EF Core context is shared by repositories; hidden tracked changes can be persisted by another repository's `SaveChangesAsync`.
- Blazor Interactive Server circuits can outlive normal HTTP request scopes, so long-lived scoped persistence dependencies require special care.
- Scaling one background workload scales the web deployment unless workers are later hosted separately.

## Guardrails

- Do not introduce a new architectural pattern, generic repository, mediator, or service boundary without a concrete use case and an updated decision record.
- UI and endpoints never enforce an invariant alone and never access the DbContext directly.
- Authorization and shop/client isolation live in application use cases even when endpoint policies provide a first check.
- Infrastructure-specific types must not leak into Domain.
- Split deployment boundaries only when measured scaling, availability, or ownership needs justify the added distributed-system cost.

## Alternatives considered

- A layered project with business rules in controllers/components was rejected because it couples policy to transport and is difficult to test safely.
- Microservices were rejected because the current consistency boundaries and operational scale do not justify distributed transactions, independent deployments, and duplicated platform concerns.
- CQRS/mediator and generic repository abstractions were not selected because current use cases are explicit and those abstractions would add indirection without removing a demonstrated constraint.
