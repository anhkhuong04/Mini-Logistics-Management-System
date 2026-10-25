# Frontend migration — validation log

## 25/10/2026 — Chuẩn bị tài liệu Phase 0

**Phạm vi:** đồng bộ entry point/.agents/developer docs, phân loại hiện trạng và kiến trúc đích, thêm ADR-004/status document, sửa liên kết manual report và cập nhật checkpoint tài liệu trong [kế hoạch](../../refactorfe.md).

**Baseline source:** HEAD `bec1f13135c743d1be1f97adddc42f6537b77110` lúc bắt đầu lượt tài liệu. Working tree có thay đổi Operations, `.gitignore`, file plan và script local; `test/manual/*` đang bị xóa. HEAD không đại diện đầy đủ behavior của working tree; các thay đổi đó không được reset/khôi phục trong lượt này.

### Kiểm chứng tài liệu

Kiểm tra trước bàn giao gồm:

- Đọc Markdown bằng UTF-8; kiểm tra local link target trong AGENTS/README/.agents/docs hiện hành, không tính các báo cáo lịch sử.
- Đếm phase/task, kiểm tra unique task ID, task reference và trạng thái checkbox trong plan.
- Đối chiếu trạng thái chưa scaffold với source; bảo đảm không trình bày npm/browser API/schema như chức năng đã có.
- `git diff --check` và đọc diff của các file tài liệu thay đổi.
- So sánh SHA-256 các source/test/script local có thay đổi sẵn và trạng thái file bị xóa với lúc bắt đầu lượt để phát hiện ghi đè ngoài phạm vi.

Kết quả đã chạy trong lượt này:

| Check | Kết quả |
| --- | --- |
| Python đọc/kiểm tra 28 Markdown hiện hành (AGENTS, README, plan, .agents, docs; bỏ history) | PASS: UTF-8/code fences hợp lệ, không còn local link target bị thiếu; ba heading anchors nội bộ hợp lệ |
| Python kiểm tra plan IDs/references/checklists | PASS: 12 phase, 40 task ID duy nhất, mọi task reference hợp lệ; chỉ ba checkpoint tài liệu đánh dấu hoàn tất |
| Đối chiếu filesystem | PASS: chưa có frontend project hoặc browser OpenAPI; trạng thái tài liệu phù hợp source |
| `git diff --check` | PASS: exit code 0; cảnh báo LF/CRLF của Git không phải whitespace error |
| Python kiểm tra whitespace ở plan và ba Markdown mới chưa tracked | PASS: không có trailing whitespace; các file mới cũng được kiểm tra ngoài phạm vi mặc định của git diff |
| SHA-256/status so với đầu lượt | PASS: 15 source/test/script paths có thay đổi sẵn giữ nguyên nội dung hoặc trạng thái bị xóa |
| Đọc diff tài liệu và nội dung file mới | Đã đối chiếu phạm vi: không sửa source ứng dụng, ADR backend/tích hợp, migration hoặc contract artifact |

Các check trên xác nhận phần chuẩn bị tài liệu; không chứng minh ứng dụng build/test pass. Toàn bộ T00.01/T00.02 vẫn chưa hoàn tất.

### Không chạy trong lượt tài liệu

Không chạy restore/build/test .NET, LocalDB/Redis integration, npm install/build/test, browser QA, migration/seed hoặc deployment. Không có thay đổi source, dependency hay runtime để kiểm chứng trong lượt này. Baseline thực thi vẫn là đầu việc mở của T00.01; version/contract matrix vẫn là đầu việc mở của T00.02.

## 25/10/2026 — Thực hiện T00.01 và T00.02

**Scope:** phân tích và baseline Phase 0 trên working tree đang có; không scaffold React, thay source ứng dụng, cài dependency vào sản phẩm, đổi Node runtime, chạy app migrate/seed hoặc deploy. Các databases test được fixture LocalDB tạo/migrate/cleanup theo suite hiện có.

**Source baseline:** HEAD vẫn `bec1f13135c743d1be1f97adddc42f6537b77110`, branch `agent/complete-shop-production-tasks` (không phải main). Operations chưa commit là một phần baseline. Fingerprint source/test và Partner OpenAPI được ghi trước build trong scratch ignored `.agent/refactorfe-phase0`; không lấy clean HEAD thay behavior working tree.

### Commands và kết quả thực tế

| Command/check | Kết quả |
| --- | --- |
| `dotnet --version` | SDK10.0.202 |
| `node --version`, `npm --version` | Local22.18.0 /10.9.3; chưa đủ cho bộ frontend pins, không đổi runtime máy trong lượt này |
| `sqllocaldb info` | Có MSSQLLocalDB; suite SQL integration thực sự đã chạy |
| `dotnet restore Mini-logistics-manegemant-system.slnx` | Exit0, up-to-date |
| `dotnet build Mini-logistics-manegemant-system.slnx -c Release` | Exit0, 0 warning, 0 error; build chuẩn có OpenAPI design-time |
| `dotnet test test/MiniLogistics.Domain.Tests/MiniLogistics.Domain.Tests.csproj -c Release --no-build` | Narrow check sau cùng Release build: exit0, 22 pass/0 fail/0 skip |
| `dotnet test Mini-logistics-manegemant-system.slnx -c Release` | Exit0; Domain22, Application160, Infrastructure78, Web60 pass; tổng320 pass,0 fail,1 skip |
| Redis integration | `RedisConfigurationCacheIntegrationTests.SeparateNodes_ObserveCommittedRouteFeeAndHubChangesThroughRedis` skip vì MINILOGISTICS_TEST_REDIS chưa cấu hình; không tuyên bố multi-node Redis đã được kiểm chứng |
| `git diff --exit-code -- docs/Service/partner-api.openapi.json` | Exit0; contract partner không drift |
| npm registry engines/peers + semver | 27 exact package pins,51 constraint checks,0 conflict/missing required peer cho Node24.22.1/npm11.19.1 |
| `npm install --package-lock-only --ignore-scripts --strict-peer-deps --dry-run --no-audit --no-fund` trong scratch candidate | Exit0, không ERESOLVE, không node_modules;35 engine warnings do chạy resolver bằng local Node/npm cũ. Không phải compile/runtime frontend validation |

Không cần chạy lại full suite sau thay đổi Markdown thuần túy khi source không đổi. Test class/tên và số lượng ở đây là evidence của lần chạy này, không phải số test cố định của repository.

### Đầu ra và đối chiếu source

- [Inventory](react-migration-inventory.md): 28 routed page files/30 route declarations,93 page/service bindings,78 method contracts, nested request/response record fields, form/auth/file/local actions và intended regression mapping. Tên browser tests là đích cần tạo, không tính như coverage đã có.
- [Toolchain](react-migration-toolchain.md): Node24.22.1/npm11.19.1,27 direct pins; TypeScript5.9.3 thay vì latest7 do peer conflict đã xác minh; không install library.
- [Contract/status](react-migration-contracts.md): decimal string, errors/status/field mapping, actor/permission, session/CSRF/cache, reference/quote, route ownership/deep links/aliases, fixture policy và file/limit baseline.
- Đã kiểm chứng banner5,242,880 byte với .png/.jpg/.jpeg/.webp/.svg/.gif; CSV stream2,000,000 byte, CsvContent validator2,000,000 ký tự,500 row; polling2s; bulk50 unique IDs; export10,000 row. Không coi extension whitelist là đầy đủ content security policy.

### Rulings trong phạm vi Phase 0

1. Baseline tại checkout hiện tại, không tạo worktree/commit product code: cần bao gồm Operations chưa commit; không có implementation cần chuyển workspace. Nếu bỏ qua working-tree diff, parity sẽ sai; fingerprint và source inventory giữ dấu vết này.
2. Dùng `.agent/refactorfe-phase0` đã bị Git ignore cho logs/candidate metadata/ledger Windows; không đưa log hoặc candidate manifest/lockfile vào sản phẩm. Public evidence chỉ giữ command/kết quả safe.
3. `routePaths.ts` là thiết kế mapping chốt trong Phase 0, file code chỉ tạo ở T01.03; tạo file sớm sẽ vượt ranh giới chưa scaffold.
4. DecimalString áp cho browser decimals vì SQL money precision vượt JS safe range; cost là phải có adapter/InputNumber stringMode và tests, tránh mất giá trị từ trước khi stringify. Partner contracts không đổi.
5. Proof UI/API và reveal/rotate secret webhook đã lưu không có trong UI/service hiện tại: loại khỏi yêu cầu parity bắt buộc, giữ backend capability/tests; thêm UI chức năng này cần task riêng. Cost là không có feature mới ngoài baseline trong migration.
6. Các Operations/pending/settlement-candidate/shippers/hub lookup thiếu actor guard phải có authorized use case entry trước HTTP exposure. Manual auto-assign truyền requestedByUserId thay vì dùng default system entry. Không sửa các service này trong Phase 0.
7. Giữ POST toggle banner để khớp action hiện có, không giả PUT set-state bằng read-then-toggle dễ race. Không auto retry mutation.

### Readiness

T00.01/T00.02 hoàn tất đầu ra phân tích và baseline; các kiểm chứng schema/browser runtime/publish thuộc task triển khai, không phải kết quả đã pass trong Phase 0. Trước T01.01 cần chuẩn bị Node/npm đúng pins; Redis test vẫn chưa chạy do thiếu cấu hình. Việc thực hiện Phase 1 nằm ngoài lượt Phase 0 này.

### Independent review và kiểm tra bàn giao

Reviewer độc lập đã đối chiếu working tree và evidence, xác nhận đủ28 page/30 route/93 binding; không còn finding Critical/Important mở. Đã sửa một lỗi quan trọng trong thiết kế staff update: dùng StaffUserId (response.UserId), không dùng MembershipId. Đã sửa lỗi mã hóa tiếng Việt trong toolchain document và kiểm tra lại nội dung UTF-8.

Các phần reviewer chưa đánh giá vì chưa được triển khai/chạy: runtime frontend sau install, implementation cookie/CSRF/browser security, published routing và Redis multi-node behavior. Giữ chúng là acceptance của task tương ứng, không ghi thành kết quả pass Phase 0.

Fingerprint sau build/test:834 source/test/script/partner-artifact paths giữ nguyên nội dung hoặc trạng thái missing so với lúc bắt đầu Phase 0. Không có thay đổi mã ứng dụng hay Partner OpenAPI. Check cuối gồm link/anchor/UTF-8/whitespace, task references và phạm vi checkbox; kết quả này không thay thế runtime test trong phase sau.

Check bàn giao đạt trên30 Markdown hiện hành và3 heading anchors nội bộ;12 phase/40 task IDs hợp lệ, chỉ13 checklist thuộc Phase 0 được đánh dấu hoàn tất. Inventory đủ93 bindings;27 version pins khớp metadata; staff update dùng UserId và toolchain không còn lỗi tiếng Việt. `git diff --check` exit0; chưa có frontend project hoặc generated browser artifact.

## 25/10/2026 — T01.01 frontend scaffold

- Prerequisite documents đã được commit riêng `a083e92`; Operations/manual deletions và script local không nằm trong commit đó.
- Node24.22.1 portable tải từ nodejs.org, đối chiếu SHASUMS256 trước khi giải nén vào `.agent/toolchains` bị ignore; npm11.19.1 đi kèm. Không đổi global Node/PATH.
- Scaffold strict TypeScript với27 direct pins, npm package-lock, scripts, Vitest/RTL/Playwright và frontend CI. npm install và npm ci đều exit0,330 packages, audit0 vulnerabilities tại thời điểm chạy.
- TDD harness: test mở/đóng support dialog ban đầu fail vì chưa có App, sau implementation đạt1/1. jsdom tắt motion trong test provider để kiểm tra state thay vì chờ animation CSS; browser test vẫn dùng motion thật.
- Sau npm ci: typecheck/lint/Vitest1 test/build đều exit0. Chromium entry flow1/1 pass. Build lúc scaffold có chunk565.74kB (gzip184.53kB), cảnh báo >500kB được ghi nhận để xử lý khi tổ chức shell ở T01.03, không nâng warning threshold để che.
- `api:generate` kiểm tra schema thật và exit1 có thông báo T02.01 khi schema chưa có; không sinh placeholder contract. Generated assets/dependencies/browser state được git-ignore đúng.
- T01.02 hosting tests đã được viết và chạy RED riêng (15 fail/1 pass), chưa có production hosting change trong commit scaffold; file test đó được giữ ngoài commit T01.01.

## 25/10/2026 — T01.02 coexist hosting

- Web hosting suite RED15 fail/1 pass → GREEN16/16; thêm regression môi trường BrowserTests không đăng ký Infrastructure workers. Full Web suite77/77 pass sau khi sửa timing cấu hình factory (connection setting phải có trước top-level DI).
- Chromium HTTPS dev proxy3/3 pass: refresh deep link, runtime config chỉ2 public paths, support keyboard interaction, API/auth/file/missing asset không trả SPA HTML, FAQ vẫn Blazor. Regression missing asset qua Vite ban đầu trả200 đã RED→GREEN bằng proxy đúng asset prefix.
- Frontend typecheck/lint/build exit0 sau cấu hình HTTPS/runtime config. Bundle-size warning còn là đầu việc tổ chức shell T01.03.
- `dotnet publish ... -p:PublishBrowserSpa=true` exit0, artifact có `frontend` riêng; process chạy từ output bằng BrowserTests và Chromium3/3 pass, không dựa vào source factory. Publish bật SPA với FrontendDistPath thiếu index fail đúng thông báo `React bundle is missing`.
- Không sửa worker lifecycle bình thường: BrowserTests là môi trường riêng cho UI smoke, không có Infrastructure hosted workers và không migrate/seed. Không suy ra browser auth/data workflows đã hoàn tất; thuộc Phase 2+.
- Runtime defaults Enabled=false/ServeAtRoot=false; root mode bị reject để giữ coexist. Cert/key, logs và publish output đều ignore, không commit.

## Cách ghi evidence cho task tiếp theo

Mỗi task ghi: ngày/task ID, working tree hoặc commit được kiểm chứng, môi trường/dependency, command chính xác, exit code/kết quả, scenario/actor/Shop, failure/skip/blocker và artifact an toàn nếu có. Test mock, render server, HTTP test và browser test phải được phân biệt.

Không ghi credential, API key, raw PII, auth storage state hoặc local connection string. Không lấy manual report mô phỏng đã xóa làm bằng chứng; scenario mới nằm trong plan/inventory và được ghi kết quả ở đây khi thực sự chạy.
