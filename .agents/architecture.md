# Architecture Guide

## Architecture Style

- Clean Architecture
- Modular Monolith

Goals:
- Maintainability
- Scalability
- Separation of concerns
- Testability
- Simplicity

---

# Solution Structure

```text
src/
├── MiniLogistics.Web
├── MiniLogistics.Application
├── MiniLogistics.Domain
├── MiniLogistics.Infrastructure
└── MiniLogistics.Shared
```

---

# Layer Responsibilities

## Web Layer

Project:

```text
MiniLogistics.Web
```

Responsibilities:

- Blazor UI
- Pages
- Components
- Authentication UI
- User interaction

Rules:

- No business logic
- No direct EF Core queries
- No domain rule implementation
- Use application services/use cases only

---

## Application Layer

Project:

```text
MiniLogistics.Application
```

Responsibilities:

- Use cases
- DTOs
- Validation
- Authorization
- Workflow orchestration

Contains:

- Commands
- Queries
- Services
- Validators
- Interfaces

Rules:

- No UI logic
- No direct infrastructure dependency
- No business persistence details
- Coordinate business flow only

---

## Domain Layer

Project:

```text
MiniLogistics.Domain
```

Responsibilities:

- Entities
- Value Objects
- Enums
- Business rules
- Domain behaviors

Contains:

- Shipment
- ShipmentAssignment
- ShipmentStatusHistory
- CodTransaction
- FeeRule

Rules:

- Pure business logic
- No EF Core attributes
- No infrastructure dependency
- No UI dependency
- No HTTP concepts

Examples:

Good:

```csharp
shipment.MarkAsDelivered();
```

Bad:

```csharp
shipment.Status = "Delivered";
```

---

## Infrastructure Layer

Project:

```text
MiniLogistics.Infrastructure
```

Responsibilities:

- EF Core
- SQL Server
- Identity
- Logging
- External services
- File storage

Contains:

- DbContext
- Configurations
- Repositories
- Identity services
- Persistence implementations

Rules:

- No business rules
- No UI logic
- Infrastructure only

---

# Dependency Rules

Allowed:

```text
Web -> Application
Application -> Domain
Infrastructure -> Domain
Infrastructure -> Application
```

Forbidden:

```text
Domain -> Infrastructure
Domain -> Web
Application -> Web
Web -> Infrastructure directly
```

---

# Feature Organization

Prefer feature-based structure.

Example:

```text
Application/
└── Shipments/
    ├── CreateShipment/
    ├── AssignShipment/
    ├── TrackShipment/
    └── UpdateShipmentStatus/
```

Avoid large generic folders with too many unrelated files.

---

# Entity Design Rules

## Entities

- Contain business behavior
- Protect invariants
- Avoid public setters when possible

Example:

Good:

```csharp
shipment.AssignToShipper(shipperId);
```

Bad:

```csharp
shipment.ShipperId = shipperId;
shipment.Status = ShipmentStatus.Assigned;
```

---

# Validation Rules

Use layered validation:

## UI Validation

- Required fields
- Basic user input validation

## Application Validation

- Use FluentValidation
- Validate requests/use cases

## Domain Validation

- Critical business invariants
- Status transitions
- Business consistency

---

# Database Rules

- Use EF Core Fluent API
- Use IEntityTypeConfiguration
- Configure decimal precision explicitly
- Use UTC datetime
- Use GUID primary keys

Avoid:

- Business logic in DbContext
- Large stored procedures
- Lazy loading

---

# Blazor Rules

- Keep Razor components thin
- Move logic into services/use cases
- Use reusable components
- Use EditForm for forms
- Avoid huge .razor files

Preferred:

```text
ShipmentCreatePage
    -> Application Service
        -> Domain
```

Avoid:

```text
ShipmentCreatePage
    -> DbContext
```

---

# Error Handling

Business errors:

- Use Result pattern or validation result

Unexpected technical errors:

- Throw exceptions
- Log using Serilog

Avoid:

- Swallowing exceptions
- Returning null for business failures

---

# Testing Strategy

Prioritize testing:

1. Domain rules
2. Use cases
3. Workflow validation

Important test scenarios:

- Invalid shipment status transitions
- Unauthorized shipment update
- Invalid COD workflow
- Shipment cancellation rules

Avoid testing trivial getters/setters.

---

# Architectural Priorities

Priority order:

1. Correct business workflow
2. Maintainability
3. Simplicity
4. Readability
5. Performance optimization

Always prefer:

- Simple code
- Clear business flow
- Explicit behavior
- Predictable structure