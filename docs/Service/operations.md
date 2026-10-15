# Vận hành tích hợp và release evidence

Runbook dành cho developer/platform vận hành Mini Logistics. [API reference](partner-api.md) sở hữu hợp đồng consumer; [Threat model](security.md) sở hữu threat/control; [ADR-001](decisions/adr-001-partner-api-production-contract.md) ghi mục tiêu topology, SLO và retention. Những mục tiêu này cần được xác nhận bằng evidence ở môi trường triển khai.

## Cấu hình production

Shape không chứa secret: [production example](../../deploy/partner-api.production.example.json). Đưa secrets qua environment hoặc secret provider.

| Nhóm | Giá trị bắt buộc |
| --- | --- |
| Runtime | `ASPNETCORE_ENVIRONMENT=Production`, `PartnerApi__Environment=Live`, `Seeding__Enabled=false` |
| Data | `ConnectionStrings__DefaultConnection` trỏ SQL production; `ConnectionStrings__Redis` |
| Partner | `PartnerApi__RateLimiting__Mode=Redis`, `PartnerApi__Retention__Enabled=true` |
| Hosts/proxy | `PublicBaseUrl` HTTPS, `AllowedHosts` giới hạn, trusted proxy/network và hop count explicit |
| Data Protection | Shared `DataProtection__KeysPath`, cùng environment-specific `ApplicationName`, certificate path và `DataProtection__CertificatePassword` |
| Cache | `ConfigurationCache__KeyPrefix` riêng môi trường/chung các node; `ConsistencyWindowSeconds` mặc định 15 |

Production guard từ chối cấu hình thiếu/không phù hợp. Không tắt guard để deployment vượt startup.

## Ingress và egress

Đăng ký IP/CIDR của reverse proxy vào `TrustedProxy`, đặt `ForwardLimit` đúng số trusted hop, giữ forwarded-for/proto đối xứng. Kiểm tra allowlist qua ingress thật và thử forged forwarded header từ nguồn không được tin cậy.

Webhook transport chỉ gửi HTTPS đến public address/allowed port, kiểm tra DNS trước lưu và lúc connect, không redirect/proxy, giới hạn timeout/header/body/connection. Hạ tầng thêm default-deny egress và các đích public HTTPS cần thiết. Không bật ambient proxy để đi vòng connect-time validation.

## Deploy và rollback

1. Backup SQL và Data Protection key ring; xác nhận khả năng restore.
2. Generate/review migration script từ release:

   ```powershell
   dotnet ef migrations script --idempotent --project src/MiniLogistics.Infrastructure --startup-project src/MiniLogistics.Web --output artifacts/partner-api-migration.sql
   ```

3. Apply bằng one-off deployment job; không để replica chạy migration cạnh tranh.
4. Deploy canary, kiểm tra `/health/live` và `/health/ready` rồi mới nhận traffic.
5. Smoke auth, quote/create/replay/track, tracking đơn UI/import cùng Shop, signed test webhook, Redis quota và queue metrics.
6. Roll tiếp các replica khi các ngưỡng rollout đã được owner xác nhận đạt yêu cầu.

`live` kiểm tra process; `ready` kiểm tra SQL, Data Protection và Redis khi bật Redis mode. Rollback application theo compatibility của schema. Không downgrade database tự động; dùng kế hoạch recovery/forward-fix đã review.

## Data Protection và rotation

Các replica dùng cùng key directory read/write, cùng app name và certificate có private key để đọc key ring. Backup trước đổi key/certificate; giữ khả năng đọc keys cũ đến khi toàn bộ protected data/backup liên quan không còn cần chúng.

Restore drill: SQL và key ring tương ứng vào staging cách ly → chạy hai replica → giải mã/gửi test delivery của endpoint tồn tại trước backup. Không xóa keys cũ khi webhook secrets vẫn cần giải mã.

API key rotation và webhook secret-version overlap phải phối hợp với consumer theo [Integration guide](integration-guide.md).

## Failure và recovery

| Sự cố | Hành vi và xử lý |
| --- | --- |
| Redis quota lỗi | Create/cancel trả `503 PartnerApi.RateLimitUnavailable`; quote/tracking fail open. Theo dõi abuse/ingress limit; xác nhận atomic quota sau recovery |
| Redis cache lỗi | Configuration cache đọc SQL; invalidation lỗi được log và giới hạn staleness theo consistency window |
| Worker chết giữa lease | Row được reclaim sau lease expiry; kiểm tra queue age giảm sau restart |
| Outbox/webhook failed hoặc DLQ | Tìm event/delivery ID, xử lý nguyên nhân, retry có quyền tại `/partner/integrations`, kiểm tra audit và duplicate-safe processing |
| SQL unavailable | Readiness unhealthy; phục hồi dependency, kiểm tra queue/backlog trước mở traffic |
| Lộ credential | Deactivate/rotate client, thu hẹp scope/IP, xem audit bằng ID/trace, phối hợp secret rotation và lưu evidence |

Không sửa payload/protected secret trực tiếp trong SQL. Retention không tự xóa unresolved failed/dead-letter rows. Logs dùng ID, status và correlation; không chứa key, secret, raw request/response hoặc PII.

## Theo dõi

Partner response có `X-Correlation-ID`. Metrics quan trọng gồm `partner_api.rate_limit_store.errors`, `partner_api.rate_limit_store.circuit_open`; meter `MiniLogistics.PartnerWorkers` có `partner_worker.claimed`, `partner_worker.results`, `partner_worker.queue.age`, `partner_webhook.delivery.duration`, `partner_worker.retention.deleted`.

Theo dõi 5xx/latency, 401/403/429 spike, Redis outage, SQL latency/capacity, queue age, retry/DLQ và webhook success. Gắn alert với runbook/on-call; số ngưỡng và window phải được thống nhất ở deployment. SLO ban đầu trong ADR là mục tiêu, không phải số đo của hệ thống đang chạy.

## Checklist chứng nhận môi trường

Checklist này thay cho tài liệu production-readiness riêng để chỉ duy trì một nơi cho release gate. Code có các control cần thiết, nhưng repository test chưa chứng minh môi trường production đã được chứng nhận.

| Nhóm | Evidence cần thu |
| --- | --- |
| Contract/quyền | OpenAPI từ release SHA, Postman/partner acceptance, denied scope, cross-shop/client, idempotency race/replay/conflict |
| Ingress/egress | Proxy chain thật, spoofing rejection, DNS/rebinding/redirect/private-target test và firewall |
| Redis/cache | Ít nhất hai replica: quota, cache refresh, timeout/outage/recovery |
| Queue | Soak nhiều worker, kill trong lease, reclaim, deduplication, retry/DLQ/retention và alert |
| Secrets | Issue/rotate/revoke với secret store thật; decrypt qua replica và rolling restart |
| Recovery | SQL + key-ring backup/restore, migration/recovery và rollback drill |
| Capacity | Load/soak theo volume/SLO đã chốt, SQL plan, audit growth, queue throughput |
| Security/privacy | Scan dependency/secret/container, independent security review, response/log PII review |
| Consumer | Raw-body HMAC/timestamp/version, duplicate/out-of-order, reconcile và failure handling |
| Operations | Dashboard/exporter, ngưỡng alert, on-call route, canary/rollback trigger và owner approval |

Load scenarios: [test/load](../../test/load/README.md). CI: [partner-api-ci.yml](../../.github/workflows/partner-api-ci.yml). Lưu kết quả trong hệ thống delivery; không đưa test count hay báo cáo tạm vào guide.

Evidence record tối thiểu: release SHA/image digest, environment/date/topology, config checksum đã bỏ secret, migration, load/chaos/security report, backup/restore/rollback, partner acceptance, approvers và accepted risks. Product, Backend, Security, Platform/SRE và QA xác nhận trước rollout public.
