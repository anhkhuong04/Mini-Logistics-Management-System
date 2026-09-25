# Local Development

Run commands from the repository root. Use `Mini-logistics-manegemant-system.slnx` or a specific project; the stray similarly named root `.csproj` is not the supported solution entry point.

## Prerequisites

- .NET 10 SDK.
- SQL Server LocalDB for the default Windows development setup and Infrastructure integration tests, or another reachable SQL Server with a local connection string.
- `dotnet-ef` only when creating or applying migrations.
- Redis only when exercising Redis Partner API rate limiting or production-like configuration.

## Configure local storage

Development defaults to LocalDB in `src/MiniLogistics.Web/appsettings.Development.json`. Override `ConnectionStrings__DefaultConnection` in local environment/provider configuration when needed. Do not commit machine-specific connection strings or credentials.

The application does not migrate or seed during ordinary startup. Use explicit command flags.

## Restore, build, and test

```powershell
dotnet restore Mini-logistics-manegemant-system.slnx
dotnet build Mini-logistics-manegemant-system.slnx -c Release -p:OpenApiGenerateDocuments=false
dotnet test Mini-logistics-manegemant-system.slnx -c Release -p:OpenApiGenerateDocuments=false
```

The OpenAPI override is a current workaround: normal generation starts the Web design-time host, whose registered background workers can reach infrastructure before a local database is available. Run the narrowest affected test project first while iterating:

```powershell
dotnet test test/MiniLogistics.Domain.Tests/MiniLogistics.Domain.Tests.csproj -c Release
dotnet test test/MiniLogistics.Application.Tests/MiniLogistics.Application.Tests.csproj -c Release
dotnet test test/MiniLogistics.Infrastructure.Tests/MiniLogistics.Infrastructure.Tests.csproj -c Release
dotnet test test/MiniLogistics.Web.Tests/MiniLogistics.Web.Tests.csproj -c Release -p:OpenApiGenerateDocuments=false
```

Infrastructure tests are Windows/LocalDB-dependent today. Do not publish a fixed test count in documentation; run the suite for the current result.

## Run locally

```powershell
dotnet run --project src/MiniLogistics.Web -p:OpenApiGenerateDocuments=false
```

The HTTPS launch profile uses `https://localhost:7195`. Health probes are `/health/live` and `/health/ready`; OpenAPI is mapped outside Production at `/openapi/v1.json`.

Apply migrations explicitly:

```powershell
dotnet run --project src/MiniLogistics.Web -p:OpenApiGenerateDocuments=false -- --migrate
```

## Optional demo seed

Seeding is disabled by default and requires local-only secrets:

```powershell
$env:Seeding__Enabled = "true"
$env:Seeding__DemoPartnerApiKey = "<local-demo-api-key>"
$env:Seeding__DemoAdminPassword = "<local-admin-password>"
$env:Seeding__DemoShopPassword = "<local-shop-password>"
$env:Seeding__DemoShipperPassword = "<local-shipper-password>"
$env:Seeding__DemoOperatorPassword = "<local-operator-password>"
dotnet run --project src/MiniLogistics.Web -p:OpenApiGenerateDocuments=false -- --migrate --seed
```

Never enable demo seeding in Production or place these values in committed settings.

## EF Core migrations

```powershell
dotnet ef migrations add <MigrationName> `
  --project src/MiniLogistics.Infrastructure `
  --startup-project src/MiniLogistics.Web `
  --output-dir Persistence/Migrations

dotnet ef migrations has-pending-model-changes `
  --project src/MiniLogistics.Infrastructure `
  --startup-project src/MiniLogistics.Web

dotnet ef database update `
  --project src/MiniLogistics.Infrastructure `
  --startup-project src/MiniLogistics.Web
```

Review all generated files and the migration SQL. Applying or removing a migration is environment-changing work and must target only an explicitly authorized database.

## Partner API contract changes

The Web project generates `docs/partner-api.openapi.json` during OpenAPI-enabled builds. A public change should update and validate, as applicable:

- endpoint/DTO implementation and authorization;
- generated OpenAPI;
- `docs/partner-api.md` and changelog;
- Postman artifacts and backend examples;
- compatibility and docs-link tests.

Do not hand-edit the generated JSON. Use the scripts and CI checks under `scripts/`/the repository pipeline for compatibility, load, or operational certification.

## Troubleshooting

### Build fails while generating OpenAPI

Build with `-p:OpenApiGenerateDocuments=false`. This bypasses the known design-time worker/database issue; it does not validate artifact freshness.

### Ready probe is unhealthy

Check SQL connectivity first. In Redis mode, check `ConnectionStrings:Redis`. Production also requires persistent encrypted Data Protection keys, trusted proxy configuration, restricted hosts, Live partner environment, and retention enabled.

### A Blazor operation reports a disposed context or tracking conflict

Interactive Server circuits can retain scoped services longer than an HTTP request. Avoid concurrent operations on a scoped service/DbContext and reload state through the use case. This is a known architectural constraint, not a reason to query the DbContext from components.

### Seed does not run

Confirm `Seeding__Enabled=true`, every required demo secret is non-empty, and `--seed` appears after `--` in the `dotnet run` command.
