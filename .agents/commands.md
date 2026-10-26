# Development Commands

Run commands from the repository root. Use the solution file, not the stray root `.csproj`.

## Frontend, contracts and release

Use Node **24.22.1**, npm **11.19.1** from the [toolchain pins](../docs/Frontend/react-migration-toolchain.md). A matching portable runtime is supported; do not commit it or change global Node unnecessarily.

```powershell
npm --prefix src/MiniLogistics.Frontend ci
npm --prefix src/MiniLogistics.Frontend run typecheck
npm --prefix src/MiniLogistics.Frontend run lint
npm --prefix src/MiniLogistics.Frontend test
npm --prefix src/MiniLogistics.Frontend run build
npm --prefix src/MiniLogistics.Frontend run dev:cert
dotnet dev-certs https --trust
# Terminal 1:
dotnet run --project src/MiniLogistics.Web --launch-profile https
# Terminal 2:
npm --prefix src/MiniLogistics.Frontend run dev
```

Use Vite `https://localhost:5173/` for root React/HMR; appsettings enables `BrowserSpa:Enabled=true`, `ServeAtRoot=true`. Vite proxies HTTP/system/static paths to `https://localhost:7195` with certificate verification enabled. Override `MINILOGISTICS_BACKEND_URL`, `MINILOGISTICS_HTTPS_CERT`/`MINILOGISTICS_HTTPS_KEY` when needed; certificates/keys remain ignored. Build frontend before serving UI directly from Web. `BrowserSpa:DistPath` defaults to source dist in Development or `<publish>/frontend` otherwise. Disabling root serves React under `/app`; it does not restore Blazor after cleanup.

Build Web normally to generate `docs/Service/browser-api.openapi.json`, then `npm --prefix src/MiniLogistics.Frontend run api:generate` for committed TypeScript types. Regenerate twice and inspect drift. Do not hand-edit generated schemas/types; browser document/serializers are separate from Partner `v1`. Keep the Partner compatibility/drift workflow.

Install the pinned Chromium browser (`npx playwright install chromium` from the frontend directory), prepare dev certificate and Web Release build, then run:

```powershell
npm --prefix src/MiniLogistics.Frontend run test:e2e
$env:MINILOGISTICS_BROWSER_E2E = '1'
$env:MINILOGISTICS_E2E_ROOT = '1'
# Optional when global Node differs: absolute path to the pinned executable
$env:MINILOGISTICS_NODE = '<path-to-pinned-node.exe>'
dotnet test test/MiniLogistics.Web.Tests/MiniLogistics.Web.Tests.csproj -c Release --filter FullyQualifiedName~BrowserRealBrowserTests
Remove-Item Env:MINILOGISTICS_BROWSER_E2E
Remove-Item Env:MINILOGISTICS_E2E_ROOT
Remove-Item Env:MINILOGISTICS_NODE
```

Shell E2E starts HTTPS .NET/Vite in isolated `BrowserTests`, without Infrastructure hosted workers. Its unused local SQL connection is not a seeded business fixture; public read failures exercise the UI fallback. Identity/data scenarios are skipped there and run through the opt-in .NET fixture: real cookies/Identity/Application plus isolated EF InMemory. The fixture alone enables the import worker for batch completion; notification/webhook dispatch workers remain omitted. It verifies Shop/Operator/Shipper/Admin/integration lifecycles and DB audit/outbox state, not SQL locking or external delivery. Keep LocalDB persistence/concurrency/SSRF/signing tests. `MINILOGISTICS_E2E_GREP` optionally filters scenarios; clear it for complete validation. Test credentials/state/traces stay ignored. Counts and exact runs belong in the [validation log](../docs/Frontend/react-migration-validation.md).

Run .NET, Vitest and browser suites sequentially on constrained Windows machines. Optional `$env:DOTNET_PROCESSOR_COUNT='2'` and frontend `test -- --maxWorkers=1` reduce contention without extending timeouts or weakening assertions.

```powershell
pwsh -File scripts/Publish-WebWithFrontend.ps1 -OutputPath .agent/release
pwsh -File scripts/Test-PublishedWeb.ps1 -ArtifactPath .agent/release -RootMode
# Explicit alternate React /app hosting smoke:
pwsh -File scripts/Test-PublishedWeb.ps1 -ArtifactPath .agent/release
```

The publish script enforces pins, runs npm ci/typecheck/lint/test/build (Vitest uses one worker) and `dotnet publish -p:PublishBrowserSpa=true`, then writes a release manifest in the separate output directory. `-SkipInstall` is for an already installed matching lockfile. Normal .NET build/test/publish do not download npm dependencies; the conditional bundle target fails when explicitly requested without dist/index.html. `Test-PublishedWeb.ps1` starts a local BrowserTests process, not a production deploy, and exercises the published bundle with strict local certificate trust. Prepare dev certificate/browser first. To test a separately running host, set `MINILOGISTICS_E2E_BASE_URL=https://localhost:<port>/` and `MINILOGISTICS_E2E_ROOT=1`; `/app/` is the alternate-mode base URL.

Before release, configure absolute persistent `BannerImages__RootPath` outside releases and copy legacy uploads preserving names/URLs. Keep SQL/Redis, Data Protection keys/ApplicationName/certificate stable across releases. Never publish files over the upload store. Phase11 rollback after cleanup requires the recorded **precleanup artifact**, with root mode false for its Blazor UI; the final artifact cannot re-enable Blazor by configuration. See [operations](../docs/Service/operations.md#react-release-và-rollback-phase11). No database downgrade or external deployment is performed by these scripts.

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
