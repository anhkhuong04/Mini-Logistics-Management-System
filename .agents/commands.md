# Development Commands

Run commands from the repository root. Use the solution file, not the stray root `.csproj`.

## Frontend migration command status

T01.01 has created `src/MiniLogistics.Frontend` with a pinned npm lockfile and frontend CI. Use Node 24.22.1 and npm 11.19.1 from the [toolchain pins](../docs/Frontend/react-migration-toolchain.md). The system Node may differ; use your version manager or a matching portable runtime without committing it.

Run from repository root:

```powershell
npm --prefix src/MiniLogistics.Frontend ci
npm --prefix src/MiniLogistics.Frontend run typecheck
npm --prefix src/MiniLogistics.Frontend run lint
npm --prefix src/MiniLogistics.Frontend test
npm --prefix src/MiniLogistics.Frontend run build
npm --prefix src/MiniLogistics.Frontend run dev
```

For browser tests, install the pinned Playwright browser first (`npx playwright install chromium` from the frontend directory), then run `npm --prefix src/MiniLogistics.Frontend run test:e2e`. Browser binaries/test state must remain ignored. `api:generate` intentionally exits with a clear error until T02.01 creates the real browser OpenAPI; do not add a placeholder schema to make it pass. The .NET commands below still run the current application; hosted SPA integration is verified separately in T01.02. Evidence belongs in the [validation log](../docs/Frontend/react-migration-validation.md).

## Prerequisites

- .NET 10 SDK.
- SQL Server LocalDB for Infrastructure integration tests and the default development connection. These tests are Windows-specific today.
- Redis and production-grade SQL/Data Protection configuration only when validating production mode.
- In production, `ConfigurationCache:KeyPrefix` must be unique to the environment and shared by all nodes; `ConsistencyWindowSeconds` bounds local cache fallback staleness (default 15 seconds).

## Restore, build and test

```powershell
dotnet restore Mini-logistics-manegemant-system.slnx
dotnet build Mini-logistics-manegemant-system.slnx -c Release
dotnet test Mini-logistics-manegemant-system.slnx -c Release
```

OpenAPI document generation is enabled in the Web project. Its design-time composition omits runtime hosted workers; no property override is needed for normal build/test.

Target one suite while iterating:

```powershell
dotnet test test/MiniLogistics.Domain.Tests/MiniLogistics.Domain.Tests.csproj -c Release
dotnet test test/MiniLogistics.Application.Tests/MiniLogistics.Application.Tests.csproj -c Release
dotnet test test/MiniLogistics.Infrastructure.Tests/MiniLogistics.Infrastructure.Tests.csproj -c Release
dotnet test test/MiniLogistics.Web.Tests/MiniLogistics.Web.Tests.csproj -c Release
```

Use `--no-build` only after the same configuration has built successfully. Use `--filter FullyQualifiedName~...` for a narrow regression test.

## Run locally

The development connection is in `src/MiniLogistics.Web/appsettings.Development.json` and defaults to LocalDB.

```powershell
dotnet run --project src/MiniLogistics.Web
dotnet run --project src/MiniLogistics.Web -- --migrate
dotnet run --project src/MiniLogistics.Web -- --migrate --seed
```

Seeding is explicit. Do not add automatic production migration/seeding or commit real passwords/API keys. Seed credentials are controlled by `Seeding` configuration/environment values.

## EF Core migrations

```powershell
dotnet ef migrations add <MigrationName> `
  --project src/MiniLogistics.Infrastructure `
  --startup-project src/MiniLogistics.Web `
  --output-dir Persistence/Migrations

dotnet ef database update `
  --project src/MiniLogistics.Infrastructure `
  --startup-project src/MiniLogistics.Web

dotnet ef migrations has-pending-model-changes `
  --project src/MiniLogistics.Infrastructure `
  --startup-project src/MiniLogistics.Web
```

Review the generated migration, designer and `MiniLogisticsDbContextModelSnapshot.cs`. Do not run `database update`, migration removal, seed, or destructive SQL against an environment not explicitly placed in scope.

## Optional coverage

```powershell
dotnet test Mini-logistics-manegemant-system.slnx --collect:"XPlat Code Coverage"
```

Do not treat coverage percentage as a substitute for invariant, authorization, transaction and concurrency tests.
