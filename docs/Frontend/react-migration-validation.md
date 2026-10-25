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

## 25/10/2026 — T01.03 theme, layouts và accessibility shell

- Shell/shared UI tests được viết trước implementation: RED vì thiếu AppLayout/AsyncContent → GREEN10/10 Vitest. Bao gồm skip/main, safe login, title/focus khi navigate, drawer Escape/focus return, navigation do caller cung cấp, app/AntD date locale, culture return URL và loading/error/empty/retry/status text.
- Regression HTTP bootstrap culture RED2/2 → GREEN; runtime config vẫn chỉ2 public paths, script đồng bộ html language theo server culture để Vite và published host nhất quán. Full Web79/79 pass.
- Chromium HTTPS dev11/11 pass: refresh/legacy/reserved paths, keyboard support/drawer, vòng Tab/Shift+Tab, skip link/Back/focus, cookie vi/en giữ path/query/reload, viewport390/768/1024/1440, CSS zoom200%. Đã xem screenshot4 viewport, mobile drawer và CSS zoom. Đây không thay thế native zoom/browser/OS QA toàn diện ở Phase 11.
- E2E RED chỉ ra Tab cuối drawer có thể đi vào browser chrome mà không phát focusin: thêm xử lý vòng Tab ở drawer, kiểm tra cả hai chiều và Escape/close/focus return đạt. AntD Layout.Content đã là main; gỡ nested main sau test phát hiện duplicate landmark.
- Một cold dev run gặp React hook error trong lúc dependency optimizer phát hiện graph mới; khai báo optimizeDeps.include cho graph hiện tại. Run8 browser workers cùng các suite khác có timeout chờ render; giới hạn Playwright2 workers, run đầy đủ11/11 pass.
- Typecheck/lint/build exit0; chia vendor theo React/AntD/common bằng [Rolldown codeSplitting](https://rolldown.rs/reference/OutputOptions.codeSplitting), không nâng warning threshold. Build entry9.11kB, React254.73kB, AntD453.06kB; không còn chunk-size warning ở lần build này.
- `dotnet test Mini-logistics-manegemant-system.slnx -c Release` exit0: Domain22, Application160, Infrastructure78 pass/1 Redis skip, Web79; tổng339 pass/0 fail/1 skip. Biến MINILOGISTICS_TEST_REDIS chưa được cấu hình; không kết luận Redis multi-node pass. Partner OpenAPI không drift.
- Publish bundle mới exit0; Chromium chạy trực tiếp trên published process đạt11/11, gồm culture và toàn bộ shell checks. Local Markdown links11 file thay đổi đạt, `git diff --check` exit0; fingerprint15/15 file dirty ban đầu giữ nguyên, không stage Operations/manual deletions/script local.

### Review cuối Phase 1

- Review độc lập range `a083e92..e791111` tìm1 Important và2 Minor. Important: Vite bỏ sót backend routes `/health`, `/openapi`, `/tracking`, `/Error`, `/not-found`; native backend tests chưa bắt Vite intercept. Đã viết regression RED rồi bổ sung proxy; health/OpenAPI giữ JSON/404, tracking/error/not-found vẫn Blazor.
- Nâng finding drawer navigation từ Minor lên Important vì không đạt keyboard focus acceptance: sau chọn trang, animation callback giành lại focus từ heading. Regression browser RED → GREEN; phân biệt dismiss và navigate, tắt cơ chế restore trùng của AntD, focus heading sau animation khi navigate; Escape/close vẫn trả trigger.
- Sau fix: frontend typecheck/lint/build exit0, Vitest10/10, Chromium HTTPS dev13/13. Entry9.30kB; vendor sizes giữ nguyên, không chunk warning.
- Re-publish exit0, Chromium trên published process13/13 pass sau fix. Không thay backend sau full solution339 pass/1 Redis skip; Partner OpenAPI và15 user fingerprints giữ nguyên.
- Minor hoãn: React chưa nạp riêng Be Vietnam Pro, font stack dùng Segoe UI/Arial fallback nếu máy chưa có font. Bổ sung font trong visual/assets follow-up; không coi font token declaration là bằng chứng font đã tải.
- Phần review để phase sau: tenant/session/cache/mutation/serialization ở Phase 2+, root cutover/atomic rollback/data-upload ở Phase 11, native zoom và browser/OS khác. Dirty user files giữ ngoài review; Redis skip giữ nguyên, không suy ra integration pass.

## 25/10/2026 — Phase 2 backend/transport checkpoint

- Browser JSON converters và status map: RED missing namespace → GREEN16 tests. DecimalString round-trip vượt JS safe integer, nullable, enum unknown/numeric rejection và UTC normalization; không thay serializer partner.
- JSON auth/reference/Shop context triển khai qua Identity và Application services. Web suite99/99 pass, bao gồm cookie thật, remember-me, CSRF identity rotation, invalid credential, lockout5 lần, inactive account, registration auto-sign-in, reference canonicalization và Application PII/owner/staff/cross-Shop checks. Fixture Identity dùng EF InMemory để kiểm tra HTTP/cookie, không dùng kết quả này để kết luận SQL atomicity; các suite SQL hiện có vẫn giữ.
- HTTP wrapper tests6/6 pass, thêm precision/DST/retry-policy tests3/3; fetch same-origin credentials, CSRF, multipart/download/204, abort và một POST duy nhất sau transport failure. React auth/context UI vẫn đang triển khai tại checkpoint này.
- Browser OpenAPI3.1.1 sinh từ endpoint metadata, document browser-v1/public-v1 tách partner; openapi-typescript7.13.0 sinh types local. Build vẫn design-time không worker; Partner artifact không đổi.

## 25/10/2026 — Phase 2 React authentication/context checkpoint

- React login/register/session/permission navigation và Shop context đã nối HTTP; chưa chuyển nội dung nghiệp vụ Phase 3+. Link chưa migrate mở document Blazor, direct React feature route có session/role/context gate rồi chuyển URL cũ.
- Vitest27/27: auth field behavior, loading không flash private content, 401 xóa session, safe return URL/role priority, reference province→ward reset, permission navigation, delayed response A→B abort/ignore và logout clear context.
- HTTPS Kestrel fixture + Chromium3 scenarios pass trên bundle thật: register auto-sign-in/reload/logout, remember-me/login-failure/lockout, owner nhiều Shop→staff masked/permission-limited→foreign Shop denied→expired cookie về login. Fixture dùng real Identity/Application + EF InMemory riêng, không fake principal, không chạm database ứng dụng. Bộ browser fixture là opt-in; shell suite thường skip3 scenario này và chạy13 shell/coexist tests.
- Body/cookie/credentials không lưu vào trace của fixture auth; output directories shell/identity tách riêng. Đã sửa Windows PEM/Schannel test fixture bằng certificate key handle trong memory. Lần chạy hai Playwright suite dùng chung output có artifact collision; output đã tách và shell13/13 pass khi chạy lại.
- Build pass với AntD chunk601.74kB (gzip198.36kB), giữ warning threshold. Thử maxSize split làm bundle lỗi runtime `Cannot destructure property LEFT ... undefined`; bỏ thay đổi split đó và fixture Chromium3/3 pass lại. Chưa tối ưu tải bundle trong checkpoint này.
- Bổ sung HTTP regression roles bị thu hồi/inactive với cookie đang có; targeted auth/contract/browser run23/23 pass. React hỗ trợ copy auth vi/en; các form cũ còn nguyên.

## 25/10/2026 — Phase 2 kiểm tra bàn giao

- Commits quan trọng: `310c5b2` cho backend/transport và `82e6478` cho React auth/session/context. Không sửa Domain/schema/worker/Partner contracts; không stage các thay đổi Operations/manual có sẵn.
- Rà soát lại failure paths và cache: thêm regression RED→GREEN khi reload session phát hiện tài khoản B mà query của A còn tồn tại; clear/cancel cache khi đổi user, ẩn Shop context khi session đang reload/lỗi. Forbidden resource chỉ tự refresh permission một lần để không tạo vòng lặp403 vô hạn.
- Decimal parser có regression RED→GREEN cho excess scale bị decimal.TryParse làm tròn âm thầm: giờ reject mất precision; kiểm tra min/max/0.10, nullable, pagination one-based, UTC và camelCase field paths. Tách lỗi request400, body413, unsupported media415 thành ProblemDetails safe; Retry-After cho429 và404 browser/public JSON. Đây là HTTP browser contract, không thay error envelope partner.
- Web suite110/110 pass với opt-in Chromium Identity fixture bật sau hardening; thêm pagination/decimal-limits contract test đạt trong contract suite26/26. Mật khẩu fixture sinh ngẫu nhiên mỗi process; không commit credential dùng được vào ứng dụng.
- Vitest29/29, typecheck/lint/build exit0. Browser shell/coexist/mobile-auth đạt15/15 trên published process (3 auth scenarios được skip ở lượt này vì đã chạy qua fixture Kestrel riêng). Đã xem screenshot login/register390px; province data chờ load trước snapshot. Native zoom/wider browser release QA vẫn Phase 11.
- OpenAPI/schema/types regenerate hai lần ổn định; browser document có Cookie auth/CSRF metadata, validator paths camelCase. CI thêm drift guard cho browser schema/types và job Windows real-cookie browser; chỉ xác nhận lệnh tương ứng chạy local, chưa có kết quả GitHub Actions remote trong lượt này.
- Tự review boundaries/authorization/CSRF/cache/proxy/publish; không có independent reviewer trong lượt Phase 2. Các giới hạn còn lại: Redis chưa cấu hình, AntD vendor601.74kB/gzip198.36kB warning, font Be Vietnam Pro chưa load riêng từ Phase 1. API/renderer nghiệp vụ Phase 3+ chưa được triển khai.
- Full handoff `dotnet test Mini-logistics-manegemant-system.slnx -c Release` với opt-in browser bật: Domain22, Application160, Infrastructure78 pass/1 Redis skip, Web111 pass; tổng371 pass/0 fail/1 skip. Web gồm real-cookie Kestrel fixture chạy3 Chromium auth/Shop scenarios. Default `.NET` run không bật opt-in sẽ có thêm1 skip của browser fixture; không nhầm lượt đó với lượt đã chạy ở đây.
- Sau đồng bộ copy navigation/context vi/en: Vitest29/29 và build/lint đạt; real-cookie browser fixture chạy lại pass. Publish artifact được cập nhật và shell/mobile-auth15/15 pass, không còn thay đổi renderer nào chưa build. Kiểm tra cuối giữ15/15 user fingerprints, Partner artifact không drift và không có broken local file links trong13 Markdown thay đổi.
- Bài kiểm tra reference data nằm cùng BrowserAuthenticationTests; browser Shop context contracts được đặt cạnh endpoint thay vì tách file chỉ chứa record. Không thêm package, migration hay user fixture vào runtime sản phẩm.

## 25/10/2026 — T03.01 Shop read API

- HTTP regression RED11/11 trước endpoint → GREEN12 read tests; auth dùng cookie Identity thật, actor lấy principal. Read API gọi SearchAsync có phân trang (spy ném lỗi nếu gọi full-list GetAsync), bind đầy đủ filter/sort/date/COD, không query DbContext trong endpoint.
- Test owner/staff/cross-Shop/not-found/inactive-history, PII/COD masking, enum/range/page invalid và cuối ngày UTC7 fractional digits. Existing Application/SQL policies giữ nguyên, không thay Domain/schema/repository.
- Caught defaults AsParameters về0 khi query paging vắng: adapter dùng nullable params và fallback1/25. Caught dashboard dictionary enum key serialization500; thêm named key converter, regression RED→GREEN. Contract + read targeted39/39 pass.
- Browser schema/types sinh từ endpoint metadata; Partner OpenAPI không đổi. `@ant-design/icons`6.3.4 đã có từ Phase 1, dùng tiếp cho Phase 3, không thêm thư viện trùng hoặc hard-code SVG/emoji.

## 25/10/2026 — T03.02 React shipment list/detail

- URL filter/UTC/decimal validation và page behavior tests RED vì chưa có module → GREEN6/6. Search/filter/sort/paging gọi typed paged API; key có user/Shop/filter, không giữ previous-tenant data. Legacy numeric status map theo enum0–10; giữ UTC7 fractional digits, reject invalid date/range/enum/page/decimal trước fetch.
- List/detail dùng Table/Descriptions/Timeline/StatusTag, hiển thị DTO snapshots/masking. Không dựng assignment/proof hoặc mutation giả; chưa có API mutation Phase 4. Browser Back và link về danh sách giữ filter/query.
- Browser real-cookie fixture mở rộng5 scenarios (3 auth/context +2 list/detail): owner search→detail→back/reload/invalid URL, limited staff masked/no COD,390px không tràn trang. Fix decorative icon accessible names bằng aria-hidden để label search đúng.
- Navigation/action icons dùng @ant-design/icons6.3.4 đã pin. Không thêm package trùng, không vẽ SVG path hoặc emoji để làm icon. Typecheck/lint/build pass; AntD chunk hiện912.15kB, ghi warning thật, không đổi threshold.

## 25/10/2026 — T03.03 dashboard và bàn giao Phase 3

- Các commit task trước: `3e8e40a` (T03.01 read API), `2433422` (T03.02 list/detail). T03.03 bổ sung dashboard dùng endpoint thật,4 KPI,7 nhóm trạng thái đếm mỗi status đúng một lần, bỏ nhóm0/không vẽ chart rỗng, finance theo quyền và latest5 đơn có server paging. Không tạo trend hoặc mutation giả.
- Vitest parity kiểm tra KPI failure không render0/empty, latest failure độc lập với KPI, limited staff không COD, inactive Shop không có create, single-Shop không có selector thừa; distribution kiểm tra tổng/percent/empty. Giữ toàn bộ HtmlRenderer/Blazor regression cũ.
- Kestrel real-cookie fixture mở rộng7 Chromium scenarios: auth/context3, list/detail2, dashboard2. KPI → list kiểm tra request thật gồm selected Shop/status/page và UTC cuối ngày `23:59:59.9999999Z`; owner/staff dùng Application thật với EF InMemory riêng. Không coi fixture này là bằng chứng SQL transaction; LocalDB suites vẫn chạy riêng.
- Xem ảnh dashboard390/1440px và kiểm tra overflow ở390/768/1024/1440px; staff masked, không có COD/create ngoài quyền, table scroll trong vùng focusable. Screenshot chỉ chứa dữ liệu synthetic đã masking, lưu trong `.agent` bị ignore; credentials/cookie không đưa vào trace.
- Rà soát failure paths bổ sung regression cho malformed/empty detail ID: trả404 UI trước fetch thay vì loader vô hạn. Adapter và URL parser reject pagination offset vượt int để tránh overflow. HTTP invalid-query test mới nằm trong full solution run.
- `dotnet test Mini-logistics-manegemant-system.slnx -c Release` với `MINILOGISTICS_BROWSER_E2E=1`, pinned Node/Chromium và dev cert: **385 pass/0 fail/1 skip** (Domain22, Application160, Infrastructure78 +1 Redis skip, Web125 gồm real-cookie fixture7 scenarios). Redis chưa có `MINILOGISTICS_TEST_REDIS`; không tuyên bố multi-node Redis đã pass.
- Frontend `test` đạt **43/43** trong13 files; `build` (bao gồm TypeScript) và `lint` exit0. Có regression riêng cho Shop inactive ngay cả khi được cấp ManageShipments. Hai lần `api:generate` có cùng SHA256 types; Partner OpenAPI không đổi. Icons dùng named imports `@ant-design/icons`6.3.4 đã có, thêm quy tắc không hardcode SVG/emoji vào standards.
- HTTPS .NET/Vite shell/coexist tests **15/15 pass**; `dotnet publish ... -p:PublishBrowserSpa=true` exit0 và Chromium trên published process **15/15 pass**. Mỗi shell run skip7 Identity/data scenarios vì chúng đã chạy bằng fixture riêng; không tính chúng là shell pass. Native zoom/các browser khác và root cutover/rollback vẫn thuộc Phase 11.
- AntD vendor933.79kB (gzip297.48kB) giữ warning >500kB; chưa tối ưu tải theo route. Be Vietnam Pro vẫn dùng fallback nếu máy chưa có font. Không thay runtime default SPA=false, Domain/Application/Infrastructure/schema/worker/Partner; create/draft/edit/cancel và import/download vẫn theo Phase 4–5.
- Tự review authorization/use-case boundary, snapshot/masking, stale query keys, URL/UTC/decimal, error/empty/loading, icon accessibility và phạm vi diff; không có independent reviewer ở Phase 3. Checklist Phase 3 và agent/developer docs cập nhật theo phần đã kiểm chứng. Handoff giữ15/15 user fingerprints, Partner không drift,14 Markdown thay đổi không có broken local file links, diff whitespace check exit0.

## Cách ghi evidence cho task tiếp theo

## 25/10/2026 — Phase 4 quote/command backend checkpoint

- T04.01 thêm QuoteShopShipment Application service: ManageShipments/active Shop kiểm tra trước normalize tỉnh/phường, classify route và calculate fee. Public reference endpoint giữ nguyên; client provinces chuyển sang shared module, registration dùng lại. Quote query350ms debounce, abort qua signal, key theo user/Shop/input, không retry POST tự động.
- T04.02 browser create/draft/update/submit/cancel gọi use case cũ, actor từ principal và Shop từ route. Draft không COD/assignment; submit tính lại phí theo rule mới; audit/history dùng actor phiên đăng nhập, body giả actor/fee/role bị bỏ qua. Create/draft chia sẻ limiter actor30/phút và store với Blazor, 429 giữ Retry-After. Invalid state/concurrency409, conflict/capacity mapping cũ giữ nguyên.
- Targeted HTTP tests9/9 pass bằng cookie Identity/Application thật + EF InMemory: quote success/normalization/invalid/CSRF/cross-Shop/staff/inactive, draft→edit→reprice→submit→cancel, create/Location, actor/audit/COD, permissions,429 và4 precision boundary cases. Đây không phải SQL transaction evidence. Direct fee-rule test update phải invalidate cache như admin workflow; sửa fixture rồi GREEN, không thay cache sản phẩm.
- Browser ingress thêm guard theo precision SQL hiện tại (weight10,3; dimensions10,2; money18,2; chargeable10,3), không sửa migration/model/Partner hoặc rounding Domain. Giới hạn input không bảo đảm mọi cấu hình fee bất thường đều lưu được; scope này không sửa historical service/fee configuration rules.
- Hook debounce và auth component targeted5/5 pass; browser schema/types sinh bằng tooling. T04.03 renderer/E2E và full solution/published checks vẫn cần hoàn tất ở checkpoint tiếp theo.

Mỗi task ghi: ngày/task ID, working tree hoặc commit được kiểm chứng, môi trường/dependency, command chính xác, exit code/kết quả, scenario/actor/Shop, failure/skip/blocker và artifact an toàn nếu có. Test mock, render server, HTTP test và browser test phải được phân biệt.

Không ghi credential, API key, raw PII, auth storage state hoặc local connection string. Không lấy manual report mô phỏng đã xóa làm bằng chứng; scenario mới nằm trong plan/inventory và được ghi kết quả ở đây khi thực sự chạy.
