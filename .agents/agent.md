# Project AI Instructions

## Project

Mini Logistics Management System

A mini logistics and shipment management system inspired by GHN/GHTK workflow.

Main features:

- Authentication & Authorization
- Shipment Management
- Shipment Tracking
- Shipper Assignment
- COD Management
- Dashboard
- Public Tracking Page

Goal:

- Build MVP quickly
- Follow Clean Architecture
- Keep code maintainable and scalable
- Suitable for Fresher portfolio/CV

---

# Tech Stack

- .NET 10
- ASP.NET Core
- Blazor Web App
- SQL Server
- Entity Framework Core
- ASP.NET Core Identity
- FluentValidation
- Serilog
- xUnit

---

# Architecture

- Clean Architecture
- Modular Monolith
- Layered structure

Projects:

- MiniLogistics.Web
- MiniLogistics.Application
- MiniLogistics.Domain
- MiniLogistics.Infrastructure
- MiniLogistics.Shared

---

# Global Coding Rules

## Clean Code

- Use meaningful names.
- Keep methods short and focused.
- Prefer readability over cleverness.
- Avoid duplicated logic.
- Use guard clauses when appropriate.
- Keep classes small and cohesive.

## SOLID

- Follow Single Responsibility Principle.
- Prefer composition over inheritance.
- Use abstractions only when needed.
- Avoid unnecessary complexity.

---

# Layer Rules

## Web Layer

Responsibilities:

- UI rendering
- User interaction
- Form handling
- Calling application use cases

Forbidden:

- Business logic
- Direct DbContext usage
- Complex validation logic

---

## Application Layer

Responsibilities:

- Use cases
- Commands/Queries
- DTOs
- Validation
- Authorization checks
- Transaction orchestration

Rules:

- Coordinate business flow
- Do not contain infrastructure logic
- Do not contain UI logic

---

## Domain Layer

Responsibilities:

- Business rules
- Entities
- Value Objects
- Domain behaviors

Rules:

- Must be framework-independent
- Must not depend on Infrastructure
- Business rules belong here

---

## Infrastructure Layer

Responsibilities:

- EF Core
- SQL Server
- ASP.NET Identity
- External services
- File storage
- Logging

Rules:

- No business rules
- No UI logic

---

# Business Priorities

Prioritize:

1. Correct workflow
2. Maintainable code
3. Simplicity
4. Clean Architecture
5. MVP completion speed

Avoid:

- Over-engineering
- Premature optimization
- Microservices
- Complex patterns without real need

---

# Important Business Rules

- Tracking code must be unique.
- Delivered shipments cannot be cancelled.
- Only assigned shipper can update shipment status.
- COD can only be collected after successful delivery.
- One shipment can only have one active shipper assignment.

---

# UI Rules

- Keep UI clean and simple.
- Use reusable components when possible.
- Use validation messages in forms.
- Use status badges for shipment status.
- Avoid cluttered pages.

---

# Testing Rules

Add tests for:

- Shipment status transitions
- Shipment creation validation
- COD workflow
- Authorization-sensitive logic

Focus on important business rules instead of trivial tests.

---

# Development Style

When implementing features:

1. Understand business flow first
2. Follow existing architecture
3. Reuse existing patterns
4. Keep changes small and focused
5. Avoid large refactors unless requested

Before generating code:

- Read architecture.md
- Read business-rules.md
- Read database.md
- Follow skills.md