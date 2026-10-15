> **Tài liệu lịch sử.** Báo cáo được giữ từ đợt review trước; các task, vị trí dòng và kết luận chưa được cập nhật theo source hiện tại. Nhiều vấn đề concurrency, scope và OpenAPI đã được xử lý. Đọc [Backend hiện tại](../README.md) và [Persistence](../persistence.md) trước khi sử dụng đề xuất.

# Báo cáo review kiến trúc và chất lượng mã nguồn

**Dự án:** Mini Logistics Management System
**Vai trò review:** Senior .NET Software Architect / Code Reviewer
**Phạm vi:** toàn bộ solution `Mini-logistics-manegemant-system.slnx`, gồm Domain, Application, Infrastructure, Web và các test project
**Nguyên tắc:** chỉ review và ghi nhận; không sửa mã nguồn production/test

## Phương pháp và bằng chứng xác minh

- Đã đọc cấu trúc project, dependency giữa các layer, các aggregate/domain model, application use case, EF Core mapping/repository, Identity, background worker, webhook, partner API, Blazor UI và test suite.
- Đã lần theo các luồng chính: tạo đơn từ Shop, tạo đơn Partner API, phân công shipper, chuyển trạng thái, COD, import/export, public tracking, outbox/webhook và cấu hình hệ thống.
- `dotnet build Mini-logistics-manegemant-system.slnx -c Release -p:OpenApiGenerateDocuments=false`: **thành công, 0 warning, 0 error**.
- `dotnet test Mini-logistics-manegemant-system.slnx -c Release --no-build -p:OpenApiGenerateDocuments=false`: **276/276 test pass** (Domain 22, Application 152, Infrastructure 66, Web 36).
- Build/test mặc định có bật sinh OpenAPI đã được chạy và **tái hiện lỗi**: host design-time khởi động `OutboxWorker` và `WebhookDeliveryWorker`, hai worker truy cập LocalDB rồi làm tiến trình sinh tài liệu thoát với mã lỗi. Đây là lỗi build có bằng chứng thực thi, không phải suy đoán.
- Không có coverage report trong repository, vì vậy số test pass không được diễn giải thành tỷ lệ coverage.

### Quy ước phân loại

- **Lỗi đã xác nhận:** có đường đi mã nguồn rõ ràng hoặc đã tái hiện bằng build/test.
- **Rủi ro đã xác nhận về thiết kế:** mã hiện tại chắc chắn thiếu cơ chế bảo vệ; hậu quả cần tải đồng thời, nhiều instance hoặc dữ liệu cụ thể để xuất hiện.
- **Cải tiến:** chưa phải lỗi chức năng hiện tại, nhưng làm giảm chi phí thay đổi/vận hành.

---

## 1. Executive Summary

Repository có nền tảng tốt hơn mức “mini project” thông thường. Dependency giữa bốn layer đi đúng chiều, Domain không phụ thuộc EF/ASP.NET Core, aggregate `Shipment` chứa phần lớn quy tắc vòng đời thay vì chỉ là data bag, partner API có API-key scope, tenant check, idempotency, rate limit và error envelope, còn luồng giao dịch dùng outbox/webhook với lease và retry tương đối bài bản. Mapping EF, index, `AsNoTracking`, cancellation token, production guard, PII masking và kiểm soát SSRF của webhook đều là các điểm đáng giữ.

Tuy nhiên, hệ thống chưa sẵn sàng để coi là production-safe khi có đồng thời hoặc chạy nhiều node. Ba nhóm rủi ro lớn nhất là:

1. `DbContext` scoped được dùng trong Blazor Server, nên scope thực tế sống theo circuit thay vì một HTTP request ngắn.
2. Các aggregate nghiệp vụ quan trọng (`Shipment`, `CodTransaction`, cấu hình giá/tuyến) không có optimistic concurrency; một số luồng còn dùng mô hình read-check-write để giữ capacity/idempotency.
3. Ranh giới transaction chưa bao phủ một số use case nhiều bước, điển hình là đăng ký Shop qua ASP.NET Identity.

Ngoài ra có hai lỗi có thể xử lý ngay: build mặc định thất bại khi sinh OpenAPI vì background workers chạy trong design-time host, và các POST endpoint xác thực đọc form thủ công nhưng không yêu cầu antiforgery validation. Public tracking đã có service hỗ trợ xác minh bốn số cuối điện thoại nhưng UI không bao giờ gửi giá trị này.

**Đánh giá tổng thể:** kiến trúc có chủ đích, test suite khá rộng và security posture tốt ở partner/webhook boundary, nhưng cần một đợt hardening về lifecycle, transaction và concurrency trước khi scale hoặc nhận tải thật. Điểm tổng hợp đề xuất: **6.9/10**.

---

## 2. Critical Issues — Actionable Tasks

Không phát hiện vấn đề ở mức **Critical** theo nghĩa có thể trực tiếp gây compromise toàn hệ thống hoặc mất dữ liệu chắc chắn ngay lập tức. Các vấn đề **High** dưới đây vẫn nên được xử lý ở P0 vì có thể gây sai dữ liệu, build không ổn định hoặc phá vỡ security boundary.

Mỗi mục dưới đây là một task độc lập. Task chỉ được coi là hoàn thành khi đạt toàn bộ **Goal**, **Acceptance criteria** và **Verification**; việc chỉ thêm abstraction, migration hoặc unit test dùng fake chưa đủ để đóng các task về transaction/concurrency.

### TASK-CI-01 — Tách `DbContext` khỏi lifetime của Blazor circuit

- **Loại:** rủi ro thiết kế đã xác nhận
- **Mức độ:** **High**
- **Vị trí:**
  - `src/MiniLogistics.Infrastructure/DependencyInjection.cs:45` đăng ký `AddDbContext<MiniLogisticsDbContext>` với lifetime scoped mặc định.
  - `src/MiniLogistics.Application/DependencyInjection.cs:17-24` đăng ký application service theo scoped lifetime.
  - `src/MiniLogistics.Web/Program.cs:208` bật Interactive Server Components.
  - Nhiều Razor component inject trực tiếp các scoped application service, ví dụ `src/MiniLogistics.Web/Components/Pages/Operations/OperationsAssignments.razor`.

Trong Blazor Server, scoped lifetime gắn với circuit, có thể tồn tại hàng phút/giờ, không phải một request. Vì repository giữ cùng `MiniLogisticsDbContext`, hệ quả là change tracker tăng dần, entity trở nên stale, cùng context có thể bị dùng bởi hai UI event chồng nhau, và `DbContext` vốn không thread-safe.

**Goal kiểm chứng được:** mỗi application operation được gọi từ Interactive Server sử dụng một `MiniLogisticsDbContext` ngắn hạn, độc lập với circuit; các thao tác cần atomicity vẫn dùng chung đúng một context/transaction trong phạm vi operation.

**Acceptance criteria:**

- [ ] Không application service/repository được Razor component giữ lại có thể giữ một `MiniLogisticsDbContext` qua hai lần gọi use case.
- [ ] Context được tạo và dispose theo operation; cancellation token vẫn được truyền xuyên suốt.
- [ ] Use case ghi nhiều aggregate/outbox/audit dùng cùng context và transaction trong một operation.
- [ ] Hai thao tác UI chồng nhau trên cùng circuit không gây lỗi thread-safety, không dùng chung tracked entity và không đọc lại state stale từ operation trước.
- [ ] Không làm thay đổi public API, authorization, tenant isolation hoặc transaction/outbox semantics.

**Verification:**

- Integration/component test mô phỏng ít nhất hai operation đồng thời trên cùng scope/circuit và nhiều operation liên tiếp.
- Test phải chứng minh các operation nhận context khác nhau, còn các repository trong cùng một operation nhận cùng context.
- Chạy Application, Infrastructure và Web test suites; kiểm tra không còn đường inject context circuit-lived bằng search/DI test.

**Phạm vi triển khai gợi ý:**

- Đăng ký `AddPooledDbContextFactory<MiniLogisticsDbContext>` hoặc `IDbContextFactory<MiniLogisticsDbContext>`.
- Mỗi use case tạo/dispose một context hoặc một unit-of-work ngắn; không giữ context trong service sống suốt circuit.
- Nếu cần transaction nhiều repository, tạo application transaction scope rõ ràng trên cùng context per-operation.
- Bổ sung test chạy hai event đồng thời trên cùng circuit và test nhiều thao tác liên tiếp để phát hiện tracker/stale state.

### TASK-CI-02 — Bảo vệ optimistic concurrency cho Shipment và COD

- **Loại:** rủi ro mất cập nhật đã xác nhận về thiết kế
- **Mức độ:** **High**
- **Vị trí:**
  - `src/MiniLogistics.Infrastructure/Persistence/Configurations/ShipmentConfiguration.cs:246-254` có index nhưng không có concurrency token/row version.
  - `src/MiniLogistics.Infrastructure/Persistence/Configurations/CodTransactionConfiguration.cs:67-71` tương tự.
  - `src/MiniLogistics.Application/Shipments/UpdateShipmentStatus/UpdateShipmentStatusService.cs:57-112` thực hiện load → kiểm tra → mutate → save.
  - `src/MiniLogistics.Application/CashOnDelivery/MarkCodCollected/MarkCodCollectedService.cs:46-134` có cùng dạng read-check-write.

Hai command đồng thời có thể cùng đọc một trạng thái hợp lệ, cùng vượt validation rồi ghi đè theo last-writer-wins. Đáng lo hơn, mỗi request có thể tạo status history/outbox/audit riêng, khiến trạng thái cuối và các event đã phát không còn tương ứng. `OutboxMessage` và `WebhookDelivery` đã có row version, nhưng aggregate nguồn chưa có.

**Goal kiểm chứng được:** hai context cùng sửa một `Shipment` hoặc `CodTransaction` từ cùng phiên bản không thể cùng commit; request thua nhận conflict nghiệp vụ ổn định và không phát sinh history/outbox/audit sai lệch.

**Acceptance criteria:**

- [ ] `Shipment` và `CodTransaction` có concurrency token được EF mapping và migration tạo đúng trên SQL Server.
- [ ] `DbUpdateConcurrencyException` được xử lý tại boundary phù hợp: API trả `409`; UI nhận lỗi conflict có hướng reload/retry, không rơi thành lỗi 500 chung.
- [ ] Transaction thua không để lại status history, COD mutation, outbox hoặc audit đã commit.
- [ ] Retry chỉ xảy ra với command thật sự idempotent; không tự động lặp một state transition có side effect.
- [ ] Contract thành công hiện tại và state machine Domain không đổi.

**Verification:**

- Integration test SQL Server dùng hai context đọc cùng phiên bản rồi cập nhật cạnh tranh cho cả Shipment và COD; chính xác một commit thành công.
- Contract/UI service test xác nhận conflict mapping và không có side effect dư.
- Chạy migration pending-model check cùng Domain, Application và Infrastructure tests.

**Phạm vi triển khai gợi ý:**

- Thêm `RowVersion`/concurrency token cho `Shipment`, `CodTransaction` và các aggregate có mutation cạnh tranh.
- Bắt `DbUpdateConcurrencyException`, trả `409 Conflict` cho API hoặc thông báo reload/retry cho UI.
- Với command có tính idempotent, dùng operation ID và unique constraint để retry an toàn.
- Bổ sung integration test thật với hai context cùng cập nhật một shipment/COD.

### TASK-CI-03 — Làm đăng ký Shop atomic

- **Loại:** lỗi thiết kế đã xác nhận
- **Mức độ:** **High**
- **Vị trí:**
  - `src/MiniLogistics.Application/Shops/RegisterShop/RegisterShopService.cs:42-78` lần lượt tạo user, gán role, tạo Shop rồi mới save Shop.
  - `src/MiniLogistics.Infrastructure/Identity/IdentityService.cs:25-67` dùng `UserManager.CreateAsync` và `AddToRoleAsync`; mỗi bước có thể commit vào Identity store trước bước kế tiếp.

Nếu gán role hoặc lưu Shop thất bại, hệ thống có thể để lại user mồ côi hoặc tài khoản thiếu role. Repository đã có `IApplicationDbTransactionManager` và các luồng admin sử dụng transaction, nhưng public registration chưa sử dụng.

**Goal kiểm chứng được:** đăng ký Shop có kết quả all-or-nothing ở góc nhìn hệ thống: thành công thì user, role và Shop đều tồn tại; thất bại ở bất kỳ bước nào thì không còn tài khoản/role/Shop có thể sử dụng dở dang.

**Acceptance criteria:**

- [ ] User, role assignment và Shop cùng commit trong một transaction khi dùng chung database/context; nếu không thể, compensation phải hoàn tất và được audit.
- [ ] Mỗi failure path trả lỗi ổn định, không lộ thông tin nhạy cảm và cho phép retry an toàn.
- [ ] Không xóa hoặc sửa nhầm user có sẵn khi compensation chạy.
- [ ] Happy path vẫn tạo đúng role, liên kết Shop và hành vi đăng nhập hiện tại.

**Verification:**

- Fault-injection integration test tại ba điểm: tạo user thất bại, gán role thất bại, lưu Shop thất bại.
- Sau mỗi lỗi, query database bằng context mới để xác nhận không có partial state; sau success xác nhận đủ cả ba phần.
- Chạy Application, Infrastructure và Web auth tests.

**Phạm vi triển khai gợi ý:** bọc user + role + Shop trong một transaction chung nếu Identity và domain dùng cùng database/context. Nếu kiến trúc không cho phép transaction chung, dùng compensation có điều kiện và audit; không mở rộng thành một saga framework tổng quát.

### TASK-CI-04 — Làm build/OpenAPI design-time không có runtime side effect

- **Loại:** lỗi đã xác nhận bằng thực thi
- **Mức độ:** **High**
- **Vị trí:**
  - `src/MiniLogistics.Web/MiniLogistics.Web.csproj:8-10` bật `OpenApiGenerateDocuments`.
  - `src/MiniLogistics.Web/Program.cs:30-64` nhận biết design-time OpenAPI nhưng vẫn gọi toàn bộ `AddInfrastructure`.
  - `src/MiniLogistics.Infrastructure/DependencyInjection.cs:169-172` luôn đăng ký bốn hosted service.

Khi `GetDocument` dựng host, worker bắt đầu poll database. Trong lần review này, `WebhookDeliveryWorker` và `OutboxWorker` đã truy cập LocalDB và làm bước OpenAPI thất bại. Design-time build không được tạo side effect hạ tầng hay phụ thuộc database sống.

**Goal kiểm chứng được:** `dotnet build` mặc định sinh OpenAPI thành công khi không có database/Redis chạy và không khởi động bất kỳ hosted worker nào; runtime bình thường vẫn đăng ký đủ worker.

**Acceptance criteria:**

- [ ] OpenAPI design-time composition không đăng ký/chạy outbox, webhook, retention hoặc import worker.
- [ ] Design-time host không poll database, không chạy migration/seed và không tạo side effect bên ngoài.
- [ ] Runtime composition vẫn có đủ bốn hosted service và production guard hiện tại.
- [ ] Không còn cần `-p:OpenApiGenerateDocuments=false` để build thành công.

**Verification:**

- CI/smoke test chạy `dotnet build Mini-logistics-manegemant-system.slnx -c Release` với connection đích không khả dụng và phải pass.
- DI test kiểm chứng worker vắng mặt trong design-time profile nhưng có mặt trong runtime profile.
- OpenAPI artifact vẫn được sinh và contract test hiện tại vẫn pass.

**Phạm vi triển khai gợi ý:** tách đăng ký persistence/runtime worker hoặc truyền option `registerHostedServices: false` khi `isOpenApiDocumentGeneration`; không tắt toàn bộ Infrastructure nếu OpenAPI endpoints cần các service đó để dựng metadata.

### TASK-CI-05 — Enforce antiforgery cho browser auth POST

- **Loại:** lỗi bảo mật đã xác nhận từ endpoint metadata
- **Mức độ:** **High**
- **Vị trí:**
  - `src/MiniLogistics.Web/Endpoints/AuthenticationEndpoints.cs:14-16` map `register-shop`, `login`, `logout`.
  - `AuthenticationEndpoints.cs:29,76` tự gọi `ReadFormAsync`.
  - Không endpoint/group nào gọi `RequireAntiforgery()` hoặc `IAntiforgery.ValidateRequestAsync`.
  - `src/MiniLogistics.Web/Program.cs:300` chỉ có `UseAntiforgery()`.

Các Razor form có render token, nhưng middleware chỉ validate khi endpoint có antiforgery metadata. Việc token xuất hiện trong HTML không tự bảo vệ endpoint đọc form thủ công. Hậu quả gồm login CSRF/session confusion, forced logout và tạo Shop ngoài ý muốn.

**Goal kiểm chứng được:** mọi browser endpoint dùng cookie và thay đổi auth state (`register-shop`, `login`, `logout`) từ chối request thiếu/sai antiforgery token và vẫn xử lý request có token hợp lệ.

**Acceptance criteria:**

- [ ] Cả ba POST endpoint có antiforgery metadata hoặc gọi validation tương đương tại server boundary.
- [ ] Request thiếu hoặc sai token bị từ chối trước khi tạo user, đăng nhập hoặc đăng xuất.
- [ ] Form hiện tại gửi token đúng; luồng đăng ký/đăng nhập/đăng xuất hợp lệ không bị phá vỡ.
- [ ] Partner API dùng Bearer key không bị áp nhầm antiforgery policy.

**Verification:**

- Web contract tests cho từng endpoint với ba case: thiếu token, token sai và token hợp lệ.
- Failure tests xác nhận không có auth/domain side effect.
- Chạy toàn bộ Web tests và kiểm tra endpoint metadata.

**Phạm vi triển khai gợi ý:** gọi `.RequireAntiforgery()` cho group hoặc từng POST endpoint. Rate limiting và account-enumeration response thuộc `TASK-M-03`, không cần gộp vào task này.

### TASK-CI-06 — Reserve shipper capacity atomically

- **Loại:** rủi ro đồng thời đã xác nhận về thiết kế
- **Mức độ:** **High**
- **Vị trí:**
  - `src/MiniLogistics.Application/Shipments/AssignmentSelection/ShipmentAssignmentSelector.cs:75-112` đọc số đơn active rồi chọn shipper có tải nhỏ hơn `MaxActiveShipments`.
  - `src/MiniLogistics.Application/Shipments/AutoAssignShipment/AutoAssignShipmentService.cs:57-135` chọn rồi mới mutate/save.
  - `src/MiniLogistics.Infrastructure/Persistence/Configurations/ShipmentAssignmentConfiguration.cs:40-42` chỉ bảo đảm một assignment active cho một shipment, không bảo đảm capacity của shipper.

Hai request có thể cùng thấy shipper còn một slot và cùng phân công, làm vượt capacity. Unique index hiện tại ngăn cùng một shipment có hai active assignment, nhưng không ngăn nhiều shipment cùng chiếm slot cuối.

**Goal kiểm chứng được:** với mọi số request auto-assignment đồng thời, số assignment active của một shipper không vượt `MaxActiveShipments`; request thua phải chọn lại ứng viên hoặc trả failure nghiệp vụ dự đoán được.

**Acceptance criteria:**

- [ ] Capacity check và reservation là một thao tác atomic ở database/transaction boundary, không chỉ là pre-check trong memory.
- [ ] Conflict không tạo duplicate assignment, history, notification, outbox hoặc audit.
- [ ] Manual assignment/override giữ đúng policy hiện tại và cũng không vô tình phá invariant capacity được chọn.
- [ ] Retry/reselection có giới hạn và hỗ trợ cancellation.

**Verification:**

- SQL Server concurrency test tạo một slot còn lại rồi cho nhiều context tranh slot; tổng active assignment sau cùng không vượt giới hạn.
- Test case có ứng viên thay thế và không có ứng viên; kết quả phải deterministic theo contract.
- Chạy assignment Application/Infrastructure tests và kiểm tra constraint/migration nếu schema đổi.

**Phạm vi triển khai gợi ý:** conditional update/counter, transaction isolation phù hợp hoặc application lock theo shipper; không dùng process-local lock vì không bảo vệ nhiều node.

### TASK-CI-07 — Hoàn thiện partner idempotency dưới concurrent requests

- **Loại:** rủi ro đồng thời đã xác nhận về thiết kế
- **Mức độ:** **High**
- **Vị trí:**
  - `src/MiniLogistics.Application/PartnerApi/PartnerCreateShipmentService.cs:70-100` kiểm tra idempotency key/external order trước khi insert.
  - `PartnerCreateShipmentService.cs:174-187` mới thêm reference và save.
  - `src/MiniLogistics.Infrastructure/Persistence/Configurations/ExternalShipmentReferenceConfiguration.cs:65-69` có unique index đúng đắn cho cả idempotency key và external order.

Unique index bảo vệ database khỏi duplicate đã commit, nhưng hai request đồng thời vẫn có thể cùng qua pre-check; request thua nhận `DbUpdateException` chưa được chuyển thành replay hoặc `409`. Như vậy dữ liệu có thể vẫn đúng nhưng API contract không ổn định.

**Goal kiểm chứng được:** hai request đồng thời có cùng `Idempotency-Key` không bao giờ tạo hai shipment hoặc trả lỗi 500; cùng payload tạo một response mới và một replay, khác payload tạo một shipment và một `409 Conflict`.

**Acceptance criteria:**

- [ ] Unique-constraint race được nhận diện đúng constraint, không nuốt các `DbUpdateException` khác.
- [ ] Cùng key + cùng request hash: một request trả `201`, request còn lại trả `200` replay cùng resource theo contract hiện tại.
- [ ] Cùng key + khác request hash: chỉ một resource được tạo, request còn lại trả `409` với partner error envelope ổn định.
- [ ] Chỉ một shipment/external reference và một bộ business side effect/outbox tương ứng được commit.
- [ ] Tracker/transaction của request thua được rollback/clear trước khi reload winner.

**Verification:**

- Partner API integration test gửi request song song cho cả same-payload và different-payload cases trên SQL Server.
- Query database bằng context mới để đếm shipment, external reference và outbox; xác nhận không có 500.
- Chạy Partner API contract và Infrastructure test suites.

**Phạm vi triển khai gợi ý:** dùng transaction/upsert hoặc xử lý unique conflict rồi reload winner; giữ nguyên unique indexes và HTTP semantics hiện tại.

### TASK-CI-08 — Serialize thay đổi version/active route và fee configuration

- **Loại:** rủi ro đồng thời đã xác nhận về thiết kế
- **Mức độ:** **High**
- **Vị trí:**
  - `src/MiniLogistics.Application/AdminSystemConfiguration/AdminSystemConfigurationService.cs:98-123` deactivate bản cũ, tính version và thêm route config.
  - `AdminSystemConfigurationService.cs:147-186` làm tương tự cho fee config.
  - `src/MiniLogistics.Infrastructure/Persistence/Configurations/FeeRuleConfiguration.cs:87` và `RouteRegionConfigConfiguration.cs:37-38` không thể hiện invariant unique đủ mạnh cho version/active set.

Hai admin request đồng thời có thể chọn cùng version hoặc tạo nhiều cấu hình active. Đây là dữ liệu điều khiển giá và routing nên hậu quả lan sang đơn mới.

**Goal kiểm chứng được:** sau mọi cặp request admin đồng thời, mỗi loại/scope cấu hình có version hợp lệ, không trùng và tối đa một bản active; hoặc cả hai request được serialize thành công, hoặc một request nhận conflict rõ ràng.

**Acceptance criteria:**

- [ ] Database constraint bảo vệ uniqueness phù hợp cho version và active set, không chỉ dựa vào application pre-check.
- [ ] Deactivate bản cũ, tạo version mới và invalidate cache nằm trong cùng transaction.
- [ ] Concurrency conflict được map thành failure nghiệp vụ/UI rõ ràng, không thành 500 hoặc last-writer-wins im lặng.
- [ ] Shipment mới dùng một snapshot/version cấu hình nhất quán trong toàn bộ phép tính route/fee.

**Verification:**

- SQL Server integration test dùng hai admin context cập nhật route đồng thời và fee đồng thời.
- Sau test, query xác nhận version không trùng, tối đa một active record cho đúng scope và cache không phục vụ bản đã mất hiệu lực.
- Chạy migration check, configuration Application/Infrastructure tests và fee calculation regression tests.

**Phạm vi triển khai gợi ý:** serializable transaction, database/application lock theo configuration key hoặc row-version conflict; tránh global lock làm nghẽn mọi loại cấu hình.

### TASK-CI-09 — Đồng bộ cache cấu hình giữa các instance

- **Loại:** rủi ro multi-instance
- **Mức độ:** **High** khi chạy nhiều node, **Medium** khi chỉ một node
- **Vị trí:** `CachedHubRepository`, `CachedRouteRegionConfigRepository` và `FeeRuleCache` trong `src/MiniLogistics.Infrastructure/Persistence`.

TTL hiện tại khoảng 30–60 phút và invalidation chỉ tác động memory của node xử lý request admin. Trong khi production guard định hướng Redis/multi-instance, các node khác vẫn có thể tính phí, route hoặc hub bằng cấu hình cũ trong cả một khoảng dài.

**Goal kiểm chứng được:** sau khi route/fee/hub configuration được commit, mọi instance đang phục vụ request ngừng dùng version cũ trong consistency window đã cấu hình; shipment/audit ghi được version/rule thực tế đã áp dụng.

**Acceptance criteria:**

- [ ] Có một nguồn version/invalidation dùng chung giữa các node; process-local invalidation không còn là cơ chế duy nhất.
- [ ] Cache chỉ publish invalidation sau commit; rollback không làm node khác loại bỏ snapshot hợp lệ.
- [ ] Cache miss/failure có fallback được định nghĩa và không âm thầm tính tiền bằng dữ liệu stale quá consistency window.
- [ ] Shipment hoặc audit lưu đủ identifier/version để tái lập quyết định route/fee.
- [ ] Nếu deployment chính thức chỉ hỗ trợ một node, phải cấu hình/production guard chặn multi-node và ghi rõ quyết định; khi đó task được đóng theo hướng chấp nhận rủi ro, không được tuyên bố đã hỗ trợ distributed cache.

**Verification:**

- Multi-node path: integration test dựng hai service providers/cache instances dùng chung Redis, cập nhật ở node A và xác nhận node B dùng version mới trong consistency window cấu hình.
- Test commit/rollback và Redis unavailable; ghi rõ expected fallback.
- Single-node path: configuration/startup test chứng minh topology bị giới hạn và tài liệu vận hành nêu rõ constraint.

**Phạm vi triển khai gợi ý:** Redis pub/sub hoặc version key, immutable versioned snapshot, hoặc TTL ngắn kèm version check; quyết định topology là prerequisite của task.

### Medium Issues — Actionable Tasks

#### TASK-M-01 — Hoàn thiện public tracking verification UI

- **Loại:** lỗi chức năng đã xác nhận
- **Mức độ:** **Medium**
- `GetPublicTrackingService.GetAsync(trackingCode, phoneLast4)` hỗ trợ trả chi tiết khi đúng bốn số cuối, nhưng `src/MiniLogistics.Web/Components/Pages/Public/Tracking.razor:231` luôn chỉ truyền `normalizedCode`.
- `TrackingVerificationFormModel.PhoneLast4` tồn tại ở dòng 348 nhưng không được render/gửi.
- **Goal:** người dùng có thể nhập bốn số cuối để nhận chi tiết được phép; thiếu/sai giá trị chỉ nhận safe summary và không phân biệt được tracking code có tồn tại hay trạng thái xác minh.
- **Acceptance criteria:** form render/gửi `PhoneLast4`; input được validate; response sai/không tồn tại có status/message/timing policy nhất quán; không mở rộng dữ liệu PII ngoài DTO đã được service cho phép.
- **Verification:** component/E2E test cho không nhập, đúng, sai và tracking code không tồn tại; assertion request thực sự truyền `PhoneLast4` vào application service.

#### TASK-M-02 — Làm public/shop UI rate limiting atomic và multi-instance safe

- **Loại:** rủi ro bảo mật/vận hành
- **Mức độ:** **Medium**
- `DistributedCachePublicTrackingRateLimiter.cs:37-50` và `DistributedCacheShopUiActionRateLimiter.cs:37-50` dùng `Get` rồi `Set`; concurrent request có thể làm mất increment.
- `src/MiniLogistics.Web/Program.cs:159` đăng ký `AddDistributedMemoryCache`, nên tên “Distributed” gây hiểu nhầm và giới hạn không chia sẻ giữa node.
- Public tracking lấy client identity từ `HttpContext` trong Blazor circuit; sau handshake context có thể null/stale và rơi về cùng key fallback.
- **Goal:** nhiều request và nhiều node dùng chung một quota atomic theo client identity ổn định; concurrent increments không bị mất và các user không bị gom vào fallback key chung.
- **Acceptance criteria:** production dùng shared atomic store; partition key được lấy tại request boundary với trusted-proxy policy; outage behavior fail-open/fail-closed được cấu hình và log; single-node memory implementation chỉ được phép ở Development/Test.
- **Verification:** concurrency test qua hai limiter instances dùng chung Redis xác nhận đúng threshold; test identity/proxy/fallback; startup guard test từ chối cấu hình production không hỗ trợ topology công bố.

#### TASK-M-03 — Thêm abuse protection cho login/register

- **Loại:** rủi ro bảo mật
- **Mức độ:** **Medium**
- Rate policy hiện tập trung ở partner API; login/register không có per-IP/account throttle. Identity lockout sau nhiều lần sai không thay thế rate limiting và còn có thể bị lợi dụng để lockout tài khoản.
- **Goal:** brute-force và registration abuse bị giới hạn theo cả IP và normalized account mà không tạo oracle tiết lộ account state.
- **Acceptance criteria:** login/register có policy riêng; vượt quota trả response ổn định (`429` và `Retry-After` nếu phù hợp); message/timing policy không phân biệt tài khoản không tồn tại, sai mật khẩu hoặc bị khóa; logout hợp lệ không bị policy gây denial-of-service ngoài ý muốn.
- **Verification:** Web tests cho dưới/ngang/vượt threshold, reset window, hai IP/một account và một IP/nhiều account; log/telemetry không chứa password hoặc raw credential.

#### TASK-M-04 — Neutralize CSV formula injection

- **Loại:** lỗ hổng đầu ra có điều kiện
- **Mức độ:** **Medium**
- `ExportShopShipmentsCsvService.cs:96-111,156-159` quote/escape CSV nhưng không neutralize ô bắt đầu bằng `=`, `+`, `-`, `@`; receiver name/address là dữ liệu người dùng.
- `ShipmentImportBatchQueryService.Escape` cũng có thể echo client order code vào CSV kết quả.
- Quote CSV không ngăn spreadsheet thực thi formula.
- **Goal:** mọi field CSV có nguồn từ user không thể được spreadsheet diễn giải thành formula khi mở file, trong khi escaping/encoding CSV vẫn hợp lệ.
- **Acceptance criteria:** áp dụng một policy dùng chung cho giá trị bắt đầu bằng `=`, `+`, `-`, `@` (kể cả sau whitespace/control character được policy xác định); không làm đổi field số/ngày do hệ thống tạo; cả export shipment và import-result CSV dùng cùng policy.
- **Verification:** unit tests cho từng prefix nguy hiểm, quote/comma/newline/Unicode và field bình thường; parse lại output bằng CSV parser để chứng minh cấu trúc cột không đổi.

#### TASK-M-05 — Đưa proof-of-delivery invariant vào domain policy

- **Loại:** thiếu business invariant
- **Mức độ:** **Medium**
- `SubmitDeliveryProofService.cs:52-102` kiểm tra quyền nhưng không buộc loại proof phù hợp trạng thái pickup/delivered.
- Validator kiểm enum/độ dài/GPS nhưng không giới hạn `CapturedAtUtc` ở tương lai và không ràng buộc `ResourceUri` vào storage scheme/host tin cậy.
- **Goal:** proof chỉ được ghi khi loại proof phù hợp với trạng thái/operation shipment, timestamp nằm trong cửa sổ cho phép và resource là reference do storage tin cậy phát hành.
- **Acceptance criteria:** business owner chốt matrix `proof type × shipment state/operation` và time tolerance trước khi code; invariant áp dụng cho mọi entry point; URI tùy ý từ client bị từ chối; failure không lưu proof/audit/outbox một phần.
- **Verification:** Domain tests cho toàn bộ matrix hợp lệ/không hợp lệ và timestamp boundary; Application/Infrastructure tests cho trusted/untrusted resource reference và transaction rollback.

#### TASK-M-06 — Loại cartesian explosion khỏi list queries

- **Loại:** rủi ro hiệu năng
- **Mức độ:** **Medium**
- `ShipmentRepository.cs:274-275` và `347-348` include cả `Assignments` lẫn `StatusHistory` trước khi materialize. Hai collection trên cùng query làm số row nhân chéo theo số phần tử.
- **Goal:** list/search operations và assignments không materialize tích Descartes giữa hai collections, vẫn filter/order/page hoàn toàn trong SQL và trả cùng dữ liệu quan sát được.
- **Acceptance criteria:** ưu tiên DTO projection; nếu cần aggregate thì dùng split/specialized query có lý do; không thêm N+1; pagination xảy ra trước khi materialize collection lớn.
- **Verification:** integration test với shipment có nhiều assignment/history xác nhận result/count/paging; inspect generated SQL hoặc command interceptor để chứng minh không có một query join đồng thời cả hai collection; benchmark dataset đại diện nếu có SLO.

#### TASK-M-07 — Loại synchronous database I/O khỏi route classification

- **Loại:** code smell/hiệu năng
- **Mức độ:** **Medium**
- `RouteClassificationService.Classify` là sync nhưng gọi `IRouteRegionConfigSource.GetProvinceRegions()`; implementation `RouteRegionConfigRepository.cs:16-26` dùng EF `ToList()` đồng bộ khi cache miss.
- **Goal:** request path phân loại route không thực hiện synchronous EF/database I/O; caller nhìn thấy rõ operation async hoặc chỉ đọc immutable in-memory snapshot.
- **Acceptance criteria:** không còn `ToList`/sync query trên cache-miss path; cancellation được truyền nếu chọn async; cache/snapshot semantics và failure behavior được test; kết quả classification không đổi.
- **Verification:** Application/Infrastructure tests cho cache hit/miss, cancellation và database failure; search/static assertion xác nhận không còn sync EF call trong implementation nguồn config.

#### TASK-M-08 — Hợp nhất core shipment creation workflow

- **Loại:** lỗi nhất quán/cải tiến kiến trúc
- **Mức độ:** **Medium**
- `CreateShipmentService.cs:103-130` normalize province/ward qua `IAdministrativeDivisionService`; `PartnerCreateShipmentService.cs:113-153` dùng địa chỉ partner trực tiếp trước route classification.
- Hai service lặp phần tạo tracking code, route, fee, shipment, COD, outbox nhưng đã khác validation/normalization.
- **Goal:** Shop UI và Partner API dùng chung một core workflow cho canonical address, route, fee, COD, aggregate creation và outbox; khác biệt contract/auth/idempotency vẫn ở entry-point adapter.
- **Acceptance criteria:** cùng business input tạo cùng canonical address, route, fee breakdown và domain state; partner vẫn giữ idempotency/scope/HTTP contract; Shop vẫn giữ permission/UI behavior; không tạo generic mediator/repository chỉ để gom code.
- **Verification:** characterization tests trước refactor và cross-entry-point parity tests sau refactor; regression tests cho address alias, invalid division, fee/COD và outbox; toàn bộ Domain/Application/Partner contract tests pass.

#### TASK-M-09 — Chuẩn hóa error contract của Shop file endpoints

- **Loại:** lỗi API contract
- **Mức độ:** **Medium/Low**
- `src/MiniLogistics.Web/Endpoints/ShopShipmentFileEndpoints.cs:61-63,90-92,118-120,138-143` trả `BadRequest` cho nhiều loại failure.
- **Goal:** endpoint trả status và error body phản ánh đúng loại failure mà không tiết lộ resource cross-tenant.
- **Acceptance criteria:** validation → 400, forbidden → 403, not found → 404 theo policy tenant hiện tại; error body dùng `ProblemDetails`/format thống nhất và có trace ID; success content type/file name không đổi.
- **Verification:** Web contract tests cho success, invalid input, unauthenticated, forbidden/cross-tenant và not found; security review xác nhận mapping không tạo resource-enumeration oracle.

#### TASK-M-10 — Làm tracking-code generation chịu được collision

- **Loại:** rủi ro tải cao
- **Mức độ:** **Medium/Low**
- `TrackingCode.Generate` kết hợp timestamp theo giây với khoảng 9.000 số ngẫu nhiên; service pre-check tối đa một số lần rồi insert. Unique index bảo vệ dữ liệu, nhưng collision đồng thời có thể thành exception chưa được retry ở database boundary.
- **Goal:** collision ở database boundary được retry an toàn và không trở thành 500; format tracking code vẫn đáp ứng contract/public UX đã công bố.
- **Acceptance criteria:** giữ unique index là lớp bảo vệ cuối; tăng entropy hoặc thay generator có đánh giá compatibility; chỉ retry đúng unique violation của tracking code với số lần hữu hạn/cancellation; không duplicate shipment/outbox khi retry.
- **Verification:** deterministic test ép generator collision rồi thành công ở lần sau; test hết retry trả failure ổn định; concurrency integration test xác nhận tất cả code đã commit là unique và không có side effect dư.

---

## 3. Architecture Review

### 3.1 Dependency và Clean Architecture

Dependency project hiện đúng chiều:

```text
Domain
  ↑
Application
  ↑
Infrastructure
  ↑
Web (composition root/UI/API)
```

- `MiniLogistics.Domain` không tham chiếu EF Core, ASP.NET Core hay Infrastructure.
- Application phụ thuộc Domain và định nghĩa port như repository, identity, audit, clock, cache/config source.
- Infrastructure implement các port và chứa EF/Identity/Redis/webhook.
- Web tham chiếu Application + Infrastructure để compose host. Đây là dependency hợp lệ ở composition root.

Không phát hiện circular project reference hoặc Domain gọi ngược Infrastructure. Đây là nền tảng nên giữ nguyên.

Một điểm cần cân nhắc: `VietnamAdministrativeDivisionService` nằm trong Web nhưng implement application port và ảnh hưởng business normalization. Nếu use case sau này chạy từ worker/CLI ngoài Web, implementation này sẽ không còn sẵn. Nên chuyển implementation/data asset sang Infrastructure hoặc một module reference-data riêng; mức ưu tiên P2.

### 3.2 Domain model

Domain không anemic. `Shipment` quản lý transition, assignment, cancel, history và các invariant quan trọng; `CodTransaction`, `FeeRule`, address/phone/tracking value object cũng có hành vi và validation. `Shipment.cs` dài nhưng phần lớn vẫn xoay quanh một aggregate lifecycle, vì vậy không nên chia file/chia class chỉ để giảm số dòng.

Các invariant còn rơi ra ngoài Domain:

- Proof of delivery và trạng thái shipment.
- Capacity assignment dưới concurrency.
- Version/active invariant của cấu hình fee/route.
- Một phần canonicalization địa chỉ phụ thuộc từng application entry point.

Không có domain event abstraction; application service tự thêm outbox. Với quy mô hiện tại, cách này chấp nhận được vì transaction boundary nhìn thấy rõ. Không nên thêm domain-event framework chỉ vì “đúng mẫu”. Chỉ nên chuyển khi một business action có nhiều consumer độc lập và cần bảo đảm side effect nhất quán mà code orchestration đang lặp.

### 3.3 Application layer và use case boundary

Use case được tách theo feature, tên rõ và nhìn chung application service không chứa HTTP/EF cụ thể. Authorization tenant/role được kiểm tra lại ở application layer thay vì chỉ tin UI, đây là điểm tốt.

Các vấn đề chính:

- Nhiều constructor dependency cho phép null rồi thay bằng Null Object. Với audit/outbox/notification quan trọng, cấu hình thiếu có thể im lặng bỏ side effect. Dependency bắt buộc nên fail fast; Null Object chỉ dùng ở mode được khai báo rõ.
- `DependencyInjection.cs:17-24` scan mọi concrete type kết thúc bằng `Service` rồi đăng ký scoped. Quy tắc dựa trên tên có thể vô tình public một implementation mới hoặc gán lifetime sai. Nên explicit-register các service có transaction/side effect quan trọng; scanning chỉ dùng cho nhóm convention thật sự đồng nhất.
- Có rất nhiều interface một implementation cho từng use case. Điều này giúp mock hiện tại, nhưng interface không phải boundary hạ tầng vẫn tạo ceremony. Không cần xóa hàng loạt; khi refactor feature, chỉ giữ interface nơi có nhiều implementation, cross-layer port hoặc test seam có giá trị thực.
- `IShipmentReadRepository` rất lớn và có default method tự lấy toàn bộ list rồi paginate/filter trong memory. Production implementation đã override nhiều method, nhưng implementation mới/test double có thể vô tình chạy unbounded. Nên bỏ data-access behavior khỏi default interface method, tách query port theo use case hoặc bắt implementation explicit.

### 3.4 Infrastructure và background processing

Outbox + webhook delivery là phần mạnh:

- Shipment mutation, external reference và outbox dùng cùng EF unit-of-work ở các luồng chính.
- Worker có lease/retry, row version và test cho cạnh tranh.
- Webhook signing secret được protect, request có HMAC, timeout/size bound.
- SSRF defense kiểm HTTPS/port, DNS public address ở cả lúc cấu hình và lúc connect, tắt redirect/proxy và dùng connect callback.

Cần sửa lifecycle `DbContext`, design-time hosted service và distributed invalidation như đã nêu. Comment/pre-warm EF model trong `Program.cs` để tránh background resolution issue là dấu hiệu composition đang hơi chặt; sau khi tách factory/scope, nên xác minh còn cần workaround đó hay không.

### 3.5 Web/API boundary

Partner API có versioned route, error envelope có trace ID, body limit, scope + tenant authorization, 201 cho create mới và 200 cho replay, cùng mapping 400/401/403/404/409/429/503 khá rõ. Đây là thiết kế tốt.

`PartnerApiEndpoints.cs` khoảng hơn 500 dòng, hiện gom authentication, rate limit, audit, request mapping và response mapping. Chưa phải lỗi runtime, nhưng nên tách endpoint/filter theo operation khi tiếp tục phát triển để contract dễ review. Authentication browser hiện trực tiếp dùng `UserManager<ApplicationUser>` trong endpoint, làm Web biết model Identity infrastructure; có thể chấp nhận ở adapter Web, nhưng antiforgery/rate-limit/error policy cần được đóng gói nhất quán.

---

## 4. Code Quality

### Điểm tốt

- Nullable reference types bật ở các project; naming và feature folder tương đối nhất quán.
- `Result`/error code làm luồng lỗi nghiệp vụ rõ hơn exception-driven flow.
- Cancellation token được truyền xuyên qua phần lớn async call.
- Value object và validator chặn input sớm, đồng thời Domain vẫn tự bảo vệ invariant cốt lõi.
- PII masking được dùng ở DTO/audit thay vì dựa hoàn toàn vào UI.
- Không phát hiện hard-coded production secret trong appsettings.

### Vấn đề và code smell

- `PartnerCreateShipmentService`, `CreateShipmentService`, import service và webhook management service đang gánh nhiều orchestration; duplication đã dẫn đến khác biệt normalization thật, không còn chỉ là vấn đề thẩm mỹ.
- Default implementation trong repository interface che giấu query không hiệu quả và làm contract khó hiểu.
- Convention DI theo hậu tố `Service` và optional dependency làm sai cấu hình khó được phát hiện lúc startup.
- Root có `Mini-logistics-manegemant-system.csproj` không nằm trong solution, gần như không có source và tên còn sai chính tả. File này có thể làm tool/CI chọn nhầm project; nên xác định mục đích hoặc xóa ở P2.
- README ghi số test cũ (267), trong khi lần review chạy được 276; tài liệu đang drift.

### Null handling và validation

Nhìn chung tốt ở command validator và value object. Các khoảng trống đáng chú ý là URI/timestamp của delivery proof, canonical address khác nhau giữa Shop và Partner, và một số uniqueness chỉ được kiểm trước khi insert mà chưa xử lý race ở DB exception boundary.

### Abstraction thừa/thiếu

- **Thừa nhẹ:** interface một implementation cho gần như mọi application service; không đem lại isolation nếu chỉ phục vụ mock-based test.
- **Thiếu quan trọng:** explicit transaction/concurrency abstraction cho business command, short-lived DbContext boundary trong Blazor, atomic reservation cho shipper capacity và distributed cache invalidation.
- **Không nên thêm:** generic repository mới, MediatR chỉ để chuyển method call thành message, hoặc chia aggregate `Shipment` chỉ theo số dòng. Các thay đổi đó tăng ceremony nhưng không giải quyết defect hiện tại.

---

## 5. Database & Performance Review

### EF Core query và tracking

Phần lớn read query dùng `AsNoTracking`, có pagination và index phù hợp. `ShipmentConfiguration` có unique tracking code, các index theo Shop/Status/CreatedAt; external reference, outbox, webhook và audit cũng có index phục vụ truy vấn chính. Đây là điểm tốt.

Các vấn đề:

1. Hai collection `Assignments` + `StatusHistory` được include cùng lúc ở operations/assigned search, tạo cartesian multiplication. Project DTO hoặc split query sẽ an toàn hơn.
2. Một số tracked load cũng luôn include toàn aggregate dù command chỉ cần một phần. Tách query read model khỏi aggregate command load sẽ giảm data transfer.
3. Route config cache miss chạy EF sync trong `Classify`, có thể block thread pool.
4. Một số màn admin lấy toàn bộ users/shops rồi dựng danh sách, chưa có server-side pagination. Chấp nhận ở dữ liệu nhỏ nhưng nên thêm trước khi số shop tăng.
5. Preview/import có nhiều validation theo từng row; cache giúp giảm tải nhưng cần benchmark ở giới hạn batch thực tế và tránh N+1 reference lookup.

### Transaction boundary

Các luồng shipment + COD + external reference + outbox thường dùng cùng context và một `SaveChanges`, nên atomicity nội bộ tốt. Các boundary còn thiếu:

- Register user/role/shop.
- Cấu hình version/active state dưới hai request đồng thời.
- Capacity reservation.
- Idempotency replay sau unique conflict.

### Caching

Cache fee/route/hub giảm query tốt ở single node, nhưng invalidation không đồng bộ multi-node. Cần phân biệt rõ:

- cache chỉ là optimization, stale vẫn đúng; hoặc
- cache chứa business configuration mà stale làm sai tiền/route.

Trường hợp hiện tại thuộc nhóm thứ hai, nên cache key gắn version và lưu version đã áp dụng vào shipment/audit.

### Index và constraint đề xuất

- Thêm row version cho `Shipment`, `CodTransaction`, cấu hình mutable.
- Unique/version constraint cho route config và fee config phù hợp với mô hình business.
- Giữ unique index external reference và tracking code; bổ sung xử lý exception/retry, không chỉ pre-check.
- Không cố biểu diễn shipper capacity bằng một unique index đơn giản; cần atomic counter/reservation hoặc lock transaction.

---

## 6. Security Review

### Điểm mạnh

- Production startup guard kiểm tra HTTPS/CORS/AllowedHosts, SQL connection, Redis, Data Protection persistence/certificate, retention và cấm seed nguy hiểm.
- API key dùng random bytes mạnh, chỉ lưu hash và có prefix môi trường.
- Partner authorization kiểm scope và tenant ở nhiều lớp; rate limiter partner dùng Redis atomic.
- Webhook SSRF defense, secret protection, HMAC signature, timeout và response size limit được thiết kế kỹ.
- Query EF/LINQ parameterized; không phát hiện SQL injection từ input người dùng.
- PII được mask theo quyền; audit log có trace/context hữu ích.

### Lỗ hổng/rủi ro

1. Auth POST thiếu antiforgery enforcement — High.
2. Login/register thiếu rate limit riêng và phản hồi có thể hỗ trợ account-state probing — Medium.
3. CSV formula injection — Medium.
4. Public tracking chỉ xác minh bằng bốn số cuối nên không được xem là authentication mạnh; atomic rate limit và response indistinguishability là bắt buộc trước khi mở UI chi tiết.
5. `ResourceUri` của delivery proof cần allowlist/safe object reference để tránh lưu/phát URL tùy ý.
6. Public/UI rate limiter memory và non-atomic không phù hợp multi-node.

Không thấy bằng chứng API key/plaintext secret được log hoặc lưu trực tiếp. Không thấy lỗ hổng SSRF rõ trong webhook path; phần này nên được giữ và tiếp tục regression-test.

---

## 7. Testing Review

### Hiện trạng

- Tổng kết lần chạy: **276 test pass** khi tắt OpenAPI generation.
- Domain test kiểm transition/value object/rule cốt lõi.
- Application test bao phủ nhiều authorization/error branch bằng fake.
- Infrastructure test có LocalDB integration cho mapping/query/worker lease và các tình huống persistence quan trọng.
- Web test có `WebApplicationFactory` cho partner API contract, production guard và security-related behavior.

### Độ tin cậy

Test suite có giá trị, nhưng chưa đủ để kết luận an toàn concurrency/Blazor lifecycle:

- Nhiều application test dùng fake/manual stub; chúng xác nhận orchestration nhưng không phát hiện transaction boundary, EF concurrency hoặc unique-index race.
- Không có test hai `DbContext` đồng thời cập nhật Shipment/COD/config.
- Không có test hai request đồng thời cùng idempotency key hoặc cùng slot shipper.
- Không có test antiforgery endpoint metadata/token failure.
- Không có component/E2E test chứng minh public tracking gửi `PhoneLast4`.
- Không có test mô phỏng Blazor circuit dài và event concurrent.
- Infrastructure test phụ thuộc Windows LocalDB, làm CI đa nền tảng khó hơn. SQL Server container/Testcontainers sẽ gần production và portable hơn.
- Build mặc định không nằm trong một test gate đủ mạnh, nên lỗi OpenAPI host lọt qua dù test logic pass.

### Test nên bổ sung theo thứ tự

1. Build smoke test với OpenAPI generation mặc định và assertion không có worker side effect.
2. Antiforgery contract test cho register/login/logout.
3. Concurrency integration test: status transition, COD, assignment capacity, config version, partner idempotency.
4. Registration rollback test với lỗi ở role và lỗi lưu Shop.
5. Blazor/circuit test dùng operation scope ngắn sau khi đổi DbContext factory.
6. Multi-instance cache invalidation/rate-limit test bằng Redis thật.
7. CSV injection, proof timestamp/URI/state, verified public tracking UI.

Nên thu coverage theo line/branch để tìm khoảng trống, nhưng không đặt một con số coverage làm mục tiêu duy nhất. Các invariant và boundary nêu trên quan trọng hơn việc tăng tỷ lệ bằng test getter/mapping đơn giản.

---

## 8. Refactoring Plan

### P0 — Sửa trước khi release/scale

1. **Làm build deterministic:** không đăng ký/chạy hosted worker trong OpenAPI design-time host; đưa build mặc định vào CI.
2. **Sửa Blazor persistence lifetime:** dùng `IDbContextFactory`/pooled factory và per-operation unit-of-work; loại scoped context sống theo circuit.
3. **Bật antiforgery thật:** `.RequireAntiforgery()` cho auth POST và thêm test token.
4. **Thiết lập optimistic concurrency:** `Shipment`, `CodTransaction`, cấu hình mutable; chuẩn hóa mapping conflict thành 409/UI reload.
5. **Sửa transaction đăng ký Shop:** atomic transaction hoặc compensation có audit.
6. **Atomic hóa business race:** shipper capacity, partner idempotency và config version/active update; viết concurrent integration test cho từng invariant.

### P1 — Hardening correctness, security và performance

1. Dùng Redis atomic cho public/shop UI rate limiting; thêm auth limiter.
2. Dùng distributed version/invalidation cho fee/route/hub caches và lưu applied config version.
3. Hợp nhất core shipment creation pipeline để Shop/Partner cùng normalize địa chỉ, route, fee, COD và outbox.
4. Project read DTO trong SQL; bỏ multiple collection include khỏi list query; benchmark query plan/dataset lớn.
5. Đưa proof-of-delivery invariant vào Domain/policy và khóa URI/timestamp.
6. Neutralize CSV formula; map HTTP status/ProblemDetails đúng ở file endpoints.
7. Hoàn thiện public tracking verification UI và regression test.
8. Đổi route classification/config source sang async hoặc snapshot không I/O.

### P2 — Giảm chi phí bảo trì

1. Thu gọn `IShipmentReadRepository`, bỏ default method truy vấn toàn bộ trong memory; tách query port theo use case khi cần.
2. Explicit-register critical services, giảm optional dependency/Null Object im lặng.
3. Rà lại interface một implementation khi chạm vào từng feature; không refactor hàng loạt chỉ vì style.
4. Tách `PartnerApiEndpoints` theo operation/filter và chuẩn hóa endpoint policy.
5. Thêm server-side pagination cho admin users/shops.
6. Chuyển infrastructure integration test sang SQL Server container để CI portable.
7. Dọn root project thừa/sai chính tả và cập nhật README/test count.
8. Di chuyển administrative-division implementation khỏi Web nếu use case cần chạy ngoài web host.

### Trình tự triển khai logic

```text
Build/CI ổn định
  → DbContext per-operation
  → row version + conflict contract
  → transaction/atomic reservation/idempotency
  → security boundary tests
  → distributed cache/rate limit
  → query/read-model optimization
  → giảm duplication và ceremony
```

Thứ tự này tránh refactor kiến trúc lớn trên một persistence lifecycle chưa an toàn, đồng thời tạo regression tests trước khi hợp nhất các workflow.

---

## 9. Final Assessment

| Hạng mục | Điểm /10 | Nhận xét ngắn |
|---|---:|---|
| Architecture | 7.0 | Layer đúng chiều; lifecycle Blazor và transaction/concurrency boundary còn yếu |
| Domain Design | 7.5 | Aggregate/value object có hành vi; POD/capacity/config invariant còn ngoài Domain |
| Application Design | 6.5 | Use case rõ, auth tốt; duplication, optional dependency và atomicity cần cải thiện |
| API Design | 7.5 | Partner contract tốt; auth antiforgery và Shop file status cần sửa |
| Infrastructure | 6.5 | Outbox/webhook/EF mapping tốt; design-time worker, context lifetime và cache multi-node là điểm trừ lớn |
| Code Quality | 7.0 | Naming/nullability/result tốt; interface/repository lớn và service duplication tăng ceremony |
| Security | 7.0 | Partner/webhook/production guard mạnh; CSRF, auth throttling, CSV và UI limiter còn thiếu |
| Performance | 6.5 | Index/AsNoTracking/paging tốt; cartesian include, sync I/O và long-lived tracker có rủi ro |
| Testing | 7.5 | 276 test pass, có integration/contract test; thiếu concurrency/circuit/CSRF và build gate |
| Maintainability | 6.5 | Feature organization rõ; convention DI, duplication, docs/project thừa làm tăng chi phí |
| **Tổng hợp** | **6.9** | Nền tảng tốt, cần P0 hardening trước production scale |

### Top 5 vấn đề quan trọng nhất

1. Scoped `DbContext` sống theo Blazor circuit.
2. Thiếu optimistic concurrency/atomicity cho Shipment, COD, capacity và config.
3. Đăng ký Shop không atomic qua Identity + domain data.
4. Auth POST chưa enforce antiforgery; rate limiting auth chưa có.
5. Build/OpenAPI design-time khởi động worker và phụ thuộc database.

### Những phần đang tốt và không nên thay đổi chỉ vì refactor

- Chiều dependency Clean Architecture và Domain độc lập framework.
- Rich aggregate/value object cho shipment, COD, fee và contact/address.
- Outbox cùng transaction với business change; webhook lease/retry/HMAC.
- SSRF protection nhiều lớp ở webhook transport.
- API-key hashing, scope/tenant authorization và partner Redis rate limit.
- Production configuration guard, persistent Data Protection và PII masking.
- Unique index/index strategy hiện có; chỉ cần bổ sung concurrency handling, không thay bằng generic repository.
- `Result`-based business error và cancellation propagation.

Kết luận: không cần viết lại hệ thống hay thay framework. Một đợt refactor tập trung vào **lifetime → concurrency → transaction → security boundary → distributed state** sẽ mang lại phần lớn giá trị. Sau P0/P1 và các test cạnh tranh tương ứng, kiến trúc hiện tại đủ tốt để tiếp tục phát triển theo modular monolith.
