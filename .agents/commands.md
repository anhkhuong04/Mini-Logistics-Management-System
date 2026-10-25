# Development Commands

Run commands from the repository root. Use the solution file, not the stray root `.csproj`.

## Frontend migration command status

React + TypeScript + Ant Design is the selected target, but `src/MiniLogistics.Frontend`, its package scripts and browser OpenAPI artifact do not exist yet. The npm examples in [the plan, section 7](../refactorfe.md#7-lệnh-validation-chuẩn-khi-bắt-đầu-thực-thi) are future validation commands, not currently supported commands.

Keep using the .NET commands below for the current application. T00.02 has locked [toolchain pins](../docs/Frontend/react-migration-toolchain.md); local Node/npm need upgrading to that target before T01.01 installation. T01.01 must create and verify scripts before adding supported frontend commands here. Add browser contract generation checks with T02.01 and publish/cutover instructions with T11.04–05. Actual baseline results and the Redis test skip are in the [validation log](../docs/Frontend/react-migration-validation.md); version/peer checks are not frontend runtime tests.

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
