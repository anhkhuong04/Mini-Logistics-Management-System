# Coding Skills & Rules

These rules must be followed when generating, modifying, or reviewing code.

---

# Core Principles

- Keep code simple, readable, and maintainable.
- Prefer explicit code over clever code.
- Follow Clean Code and SOLID principles.
- Avoid over-engineering.
- Avoid premature optimization.
- Keep changes small and focused.

---

# Clean Code Rules

- Use meaningful names.
- Avoid unclear abbreviations.
- Keep methods short and focused.
- Keep classes small and cohesive.
- Prefer guard clauses over deep nesting.
- Avoid duplicated logic.
- Remove dead code.
- Do not create god classes or god services.
- Do not hide business rules in private random helpers.
- Comments should explain business decisions, not obvious code.

---

# SOLID Rules

## Single Responsibility

- One class should have one clear reason to change.
- Do not mix UI, business logic, persistence, and validation in one class.

## Open/Closed

- Prefer adding new behavior without modifying many existing files.
- Do not create complex abstraction too early.

## Liskov Substitution

- Avoid inheritance unless substitution is valid.
- Prefer composition over inheritance.

## Interface Segregation

- Prefer small, focused interfaces.
- Do not create large interfaces with unrelated methods.

## Dependency Inversion

- High-level policies should not depend on low-level details.
- Use dependency injection for services.
- Avoid direct instantiation of infrastructure services in application code.

---

# Clean Architecture Rules

- Web layer must not access DbContext directly.
- Web layer must not contain business rules.
- Domain layer must not depend on other layers.
- Application layer coordinates use cases.
- Infrastructure layer implements persistence and external services.
- Keep business rules inside Domain when possible.
- Keep validation close to use cases.

---

# Domain Rules

- Entities should protect their own invariants.
- Prefer behavior methods over public setters.
- Use enums for fixed statuses.
- Use value objects for important domain concepts when useful.
- Do not allow invalid state transitions.
- Avoid anemic domain model for core workflow logic.

Example:

```csharp
shipment.MarkAsDelivered();
```

Avoid:

```csharp
shipment.Status = ShipmentStatus.Delivered;
```

---

# Application Layer Rules

- Use request/response DTOs for use cases.
- Use FluentValidation for command/request validation.
- Use async/await for I/O operations.
- Accept CancellationToken where appropriate.
- Return clear business errors.
- Do not leak EF Core entities directly to UI.
- Do not put SQL or EF-specific logic here.

---

# EF Core Rules

- Use Fluent API configuration.
- Use `IEntityTypeConfiguration<T>`.
- Configure decimal precision explicitly.
- Use `AsNoTracking()` for read-only queries.
- Avoid lazy loading.
- Avoid business logic in DbContext.
- Use migrations for schema changes.
- Keep queries readable.
- Project to DTOs for list/detail views when appropriate.

---

# Blazor Rules

- Keep Razor components thin.
- Use application services/use cases from UI.
- Use `EditForm` for forms.
- Show validation messages.
- Extract reusable UI components when duplicated.
- Avoid large `.razor` files.
- Avoid direct database access from components.
- Avoid business workflow logic in components.

---

# Validation Rules

Use layered validation:

- UI: basic required fields and user feedback.
- Application: request validation with FluentValidation.
- Domain: critical business invariants.

Do not rely only on UI validation.

---

# Error Handling Rules

- Use Result-style return for expected business errors.
- Throw exceptions only for unexpected technical failures.
- Log technical errors.
- Do not swallow exceptions silently.
- Do not return `null` for failed operations.

---

# Security Rules

- Always enforce authorization in use cases.
- Never trust UI-only permission checks.
- Shop can only access own data.
- Shipper can only access assigned shipments.
- Do not expose sensitive internal data in public tracking.
- Validate all user input.

---

# Testing Rules

Prioritize tests for:

- Shipment status transitions
- Shipment creation validation
- Shipper assignment rules
- COD workflow
- Authorization-sensitive use cases

Avoid tests for:

- Simple getters/setters
- Framework behavior
- Trivial mappings unless critical

---

# Code Generation Checklist

Before finishing code generation, verify:

- Code compiles logically.
- Layer dependencies are valid.
- No business logic in UI.
- No DbContext usage in Blazor components.
- Business rules are enforced.
- Validation is included.
- Names are clear.
- No unnecessary abstraction.
- Important use cases are testable.