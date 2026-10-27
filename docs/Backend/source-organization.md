# Source organization

Updated 26 October 2026. This document records the source-organization cleanup after
the React migration; it does not change business policies, HTTP contracts, schema,
outbox/worker semantics, or deployment ownership.

## Ownership

| Location | Responsibility |
| --- | --- |
| `src/MiniLogistics.Domain` | Aggregates, invariants, value objects; no project/framework dependencies |
| `src/MiniLogistics.Application` | Feature use cases, authorization, validation, DTOs and focused ports; depends on Domain |
| `Infrastructure/Persistence` | DbContext, EF configurations/migrations, repositories, transactions and seeding |
| `Infrastructure/Caching` | Configuration cache implementations and Redis generation store |
| `Infrastructure/Storage/Banners` | Persistent banner filesystem storage and content validation |
| `Infrastructure/Documents/Labels` | PDF/Code39 rendering through the Application renderer port |
| `Infrastructure/ReferenceData` | Administrative-division JSON reading/normalization; source file remains at `Web/wwwroot/data` |
| `Infrastructure/Auditing`, `Identity`, `RateLimiting`, `Health` | Audit persistence, Identity and technical I/O adapters |
| `Infrastructure/Shipments/Import`, `Outbox`, `PartnerApi` | Hosted import/outbox/webhook work; registration/lifecycle unchanged |
| `Web/Endpoints/Browser/{Auth,Shops,Operations,Shipper,Administration,Integrations,Public}` | Browser HTTP adapters; `Common` owns shared binding, claims, errors and serializers |
| `Web/Services` | HTTP hosting/session/configuration adapters and ingress limiters |
| `Frontend/src/app` | Composition, layouts/providers and lazy route ownership |
| `Frontend/src/features` | UI use cases, feature-specific queries/forms and public `index.ts` APIs |
| `Frontend/src/shared` | HTTP/generated types, routing, shared shipment query models, dates, i18n and UI primitives |

Web intentionally references Application and Infrastructure as the composition root.
Shared DbContext and transaction boundaries remain together; folder grouping does
not imply separately deployable modules. Existing Application operation scopes remain.

## Dependency contracts

- Domain has no project references. Application references only Domain. Infrastructure
  implements Application ports. Business endpoints never query DbContext directly.
- Shipment read ports contain declarations only. Required SQL search/paging methods
  cannot inherit a full-list fallback. Tracked aggregate loads live in the write port.
  Application test doubles supply their own explicitly test-only query behavior and
  reject unmodeled COD/SLA queries; Infrastructure tests remain SQL evidence.
- Browser features use `BrowserUserClaims`, `BrowserApiResultMapper` and common query
  validation, rather than reaching into another feature endpoint for helpers.
- Frontend `shared` cannot import `features`/`app`; features cannot import `app`.
  Cross-feature imports use public indexes. App may compose lazy feature pages.
  ESLint enforces static imports, re-exports and dynamic imports; its rule tests cover
  allowed and forbidden paths. Public indexes must not introduce reverse feature
  dependencies merely to expose implementation details.
- Shop-scoped command guards/download controls belong to `features/shop-context`.
  Pure download transport/file URLs live in `shared/api`; generic identifier/date
  utilities live in `shared/lib`. UTC and server-time-zone date semantics stay distinct.

## Formatting and validation

`.editorconfig` owns C#/text conventions. Format changed C# source with the installed
.NET formatter, excluding EF migration history and archived user files:

```powershell
dotnet format whitespace Mini-logistics-manegemant-system.slnx --no-restore --include <changed-files>
npm --prefix src/MiniLogistics.Frontend run format
npm --prefix src/MiniLogistics.Frontend run format:check
npm --prefix src/MiniLogistics.Frontend run test:architecture
```

Prettier3.6.2 is an exact-pinned development dependency; no runtime library was added.
Generated browser declarations remain generator-owned and excluded from formatting.
Frontend CI and the full-stack release helper enforce format and architecture checks.
Backend layer/port checks run inside Application tests. Use the normal build/tests
in [commands](../../.agents/commands.md), including SQL and browser checks as relevant.

## Retired material

- The unsupported root test `.csproj`, unused Domain assembly marker, unused legacy
  React route and 44 unreferenced Bootstrap files were removed; Git retains their history.
- Nine user-modified Blazor Operations files were moved byte for byte to
  [archive/legacy-blazor](../../archive/legacy-blazor/README.md). They are outside
  source/build/publish and are not formatted. Do not restore the deleted manual reports.
- The full original [frontend plan](../Frontend/history/refactorfe.md) was recovered
  from Git, with relative links rebased and historical checklists retained. Root
  [refactorfe.md](../../refactorfe.md) is a compatibility pointer. Current runtime
  guidance remains in the Frontend README/contracts, not the historical plan.
- Legacy banner files/images and the administrative JSON remain because persisted
  references or active UI may still need them. Data/upload deletion is not source cleanup.
- The solution filename remains unchanged so current commands and release references
  continue to work. Production rollout/Redis topology remain separate work.

## Verification

Verified on 26 October 2026 against the working tree based on `f947eb7`, with pinned
Node24.22.1/npm11.19.1 and .NET SDK10.0.202. Commands ran locally; no remote CI or
production deployment is implied.

| Check | Result |
| --- | --- |
| Solution Release build (`-m:1 --disable-build-servers --no-restore`) | Exit0, zero warnings/errors |
| Final backend project results | Domain24 + Application162 + Infrastructure97 + Web220 = **503 passed**, zero final failures; Redis1 and browser opt-in1 skips in baseline |
| New backend protections | Layer/port tests, PDF fields/barcode/xref consistency, authoritative reference-data DI without a Web host |
| Clean frontend install | `npm ci`:331 packages installed,332 audited, zero vulnerabilities; lockfile adds only Prettier, existing package entries unchanged |
| Frontend static checks | `format:check`, `test:architecture`, typecheck and lint exit0; architecture rule exercises5 allowed and5 forbidden cases |
| Frontend behavior/build | Vitest136/136 in32 files, one worker; Vite production build exit0 |
| C# formatting | All92 changed/new non-archived C# files pass `dotnet format whitespace --verify-no-changes`; migrations excluded |
| Real Identity browser fixture | Opt-in root fixture passes1 .NET test containing37 Chromium scenarios; real cookies/Application with isolated InMemory data |
| Local publish | `dotnet publish ... -p:PublishBrowserSpa=true -o .agent/source-organization/publish` exits0; no Bootstrap files in artifact |
| Published root/app smoke | `Test-PublishedWeb.ps1 -RootMode`:15 passed; alternate `/app`:21 passed/41 intentionally skipped (data/root cases verified separately) |
| Generated contracts | Web generation plus `npm run api:generate`; Browser/Partner schemas and generated TS have zero Git diff and preserve baseline SHA-256 |
| Preservation/documentation | All9 archived Operations files and the user script retain SHA-256; archived plan retains40 tasks/172 checkboxes; active and archived-plan relative links resolve; `git diff --check` clean |

Backend figures are assembled from successful project runs, rather than counting an
interrupted/failing solution attempt as green. The first broad run exposed an
unwanted Web-host dependency in reference-data registration and moved-source links;
these were fixed with explicit host-selected file registration, an additional DI
regression test and rebased links. A remaining old test constructor was corrected
before the final Infrastructure97-pass run. The unchanged PDF rendering body was
also compared with its original implementation, ignoring whitespace/class relocation.

After clean npm installation the machine lacked the required Chromium revision;
Playwright's pinned browser was installed while retaining older cached browsers.
One parallel integration E2E run timed out after the security response; its isolated
run passed. The helper now scopes the confirmation dialog and waits for it to close
before another action. All37 scenarios then passed together, retaining their
assertions and timeouts.

Redis remains opt-in/unconfigured. InMemory browser scenarios do not establish SQL
atomicity or external webhook delivery; LocalDB tests provide separate persistence
evidence. AntD vendor remains1014.17kB/gzip321.68kB with the existing size warning.
Previous migration evidence remains in the [migration log](../Frontend/react-migration-validation.md).
