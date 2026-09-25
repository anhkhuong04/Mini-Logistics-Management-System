# Mini Logistics Management System

Mini Logistics is a .NET 10 modular monolith for small-parcel shipment operations. It covers shop shipment creation/import, dispatch and shipper workflows, tracking, cash-on-delivery (COD), administration, and a scoped Partner API with signed webhooks.

## Technology

- ASP.NET Core and Blazor Web App (Interactive Server)
- Entity Framework Core with SQL Server
- ASP.NET Core Identity and policy/application authorization
- FluentValidation and xUnit
- Redis for production Partner API rate limiting

## Quick start

Prerequisites: .NET 10 SDK and SQL Server LocalDB (or another local SQL Server).

```powershell
dotnet restore Mini-logistics-manegemant-system.slnx
dotnet run --project src/MiniLogistics.Web -p:OpenApiGenerateDocuments=false -- --migrate
dotnet run --project src/MiniLogistics.Web -p:OpenApiGenerateDocuments=false
```

The HTTPS launch profile uses `https://localhost:7195`. LocalDB is configured in `src/MiniLogistics.Web/appsettings.Development.json`; use local environment/provider configuration for overrides and secrets.

Demo data is opt-in and requires local credentials. See [Local development](docs/development/local-development.md#optional-demo-seed).

## Validate

```powershell
dotnet build Mini-logistics-manegemant-system.slnx -c Release -p:OpenApiGenerateDocuments=false
dotnet test Mini-logistics-manegemant-system.slnx -c Release -p:OpenApiGenerateDocuments=false
```

The OpenAPI property is a documented workaround for a design-time host issue. Infrastructure integration tests currently require Windows SQL Server LocalDB.

## Documentation

- [Documentation guide](docs/README.md)
- [Product overview](docs/product/overview.md)
- [Domain model and rules](docs/domain/model-and-rules.md)
- [Architecture overview](docs/architecture/overview.md)
- [Data model and persistence guarantees](docs/database/data-model.md)
- [Partner API reference](docs/partner-api.md) and [integration guide](docs/third-party-shipment-integration-guide.md)
- [Local development](docs/development/local-development.md)

Coding agents should begin with [AGENTS.md](AGENTS.md). It contains operational instructions and links to the focused `.agents` guidance; it is not product documentation.

## Repository layout

```text
src/      Domain, Application, Infrastructure, and Web projects
test/     Domain, Application, Infrastructure, and Web test projects
docs/     Product, domain, architecture, data, integration, and operations knowledge
postman/  Partner API collection and environment
scripts/  Contract, compatibility, load, and operational validation helpers
```

## License

This project is for educational and portfolio purposes.
