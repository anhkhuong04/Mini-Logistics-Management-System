# Bao Cao San Sang Production - Partner Tracking API

Ngay danh gia lai: 2026-08-02

## Quyet Dinh

**GO cho production-like staging certification. NO-GO cho public production.**

Nhung diem chan o implementation ban dau da duoc khac phuc trong codebase. He thong
chua the duoc xac nhan production-ready chi bang local/CI tests: egress firewall,
ingress that, managed Redis, shared key store, multi-replica soak/chaos, monitoring
backend va phe duyet van can bang chung staging.

## Trang Thai Cac Diem Chan Ban Dau

| Diem chan | Trang thai implementation | Bang chung chinh | Gate con lai |
| --- | --- | --- | --- |
| Chi track shipment cua cung API client | Da khac phuc | Query theo `trackingCode + ShopId`; UI/CSV/API va cross-client tests | Staging partner flow |
| Webhook URL co nguy co SSRF | Da khac phuc trong app | HTTPS/port policy, public A/AAAA validation, connect-time pinning, no redirect, bounded response | Egress firewall va controlled DNS test |
| IP whitelist chua trust reverse proxy dung cach | Da khac phuc trong app | Explicit proxy/network/hop config va spoof tests | Real ingress test |
| Data Protection key ring ephemeral | Da khac phuc ve config/code | Shared path, fixed app name, PKCS#12 KEK, readiness roundtrip, cross-provider decrypt test | Rolling restart va backup/restore |
| Rate limit memory/non-atomic | Da khac phuc trong production path | Redis Lua atomic increment/TTL, timeout, circuit breaker, endpoint fail policy | Managed Redis, 2-replica load/outage drill |
| Worker khong co distributed claim/lease | Da khac phuc | SQL `UPDLOCK/READPAST/ROWLOCK`, rowversion, lease/reclaim, DLQ, unique event ID | Multi-worker soak va kill test |
| Tracking tra raw timeline note | Da khac phuc | Shared public mapper; contract/PII tests; OpenAPI khong co `note` | Privacy sign-off |
| Thieu sandbox/OpenAPI/health/load/monitoring | Artifact/code da co mot phan | Env-bound keys, OpenAPI/Postman, probes, meters, k6 profile, CI | Provision sandbox, execute load/chaos, exporter/dashboard/alerts |

## Implementation Da Hoan Thanh

### Authorization va public contract

- `GET /api/v1/partner/shipments/{trackingCode}` yeu cau scope `TrackShipment` va
  authorize theo `ShopId`.
- API client co the track non-draft shipment cua shop duoc tao tu portal, CSV, chinh
  client do hoac client khac trong cung shop.
- Cross-shop lookup tra `404` dong nhat. `externalOrderId` chi duoc tra cho reference
  cua current API client; cac truong hop con lai la `null`.
- Create, cancel va idempotency van bi rang buoc boi originating `ApiClientId`.
- Partner API va public tracking UI dung cung public status mapper. Internal note,
  actor, phone, email, address, GPS va token khong xuat hien trong timeline public.

### Webhook va credential security

- Webhook chi chap nhan absolute HTTPS URL, port allowlist va public destination.
- Tat ca DNS answers duoc validate; loopback, RFC1918/ULA, link-local, multicast,
  CGNAT, reserved/documentation va metadata address bi reject.
- Destination duoc resolve/validate lai trong `SocketsHttpHandler.ConnectCallback` va
  socket ket noi den IP da validate. Automatic redirect bi tat.
- Connect/request timeout, response header/body limit va connection cap duoc enforce.
  Response body khong duoc luu; transport error luu/log o dang generic.
- API key co prefix theo environment (`ml_test_`, `ml_live_`) va key sai environment
  bi reject truoc database lookup.
- Webhook secret yeu cau toi thieu 32 UTF-8 bytes. Secret rotation tang version; queued
  delivery giu protected secret snapshot va version cu de khong mat event.

### Horizontal scale va reliability

- Production bat buoc Redis rate limiter. Lua script thuc hien atomic increment,
  expiry va TTL voi key co environment/client/action/window.
- Redis store co timeout va local circuit breaker. Create/cancel fail closed bang
  `503`; quote/tracking fail open va phat metrics theo ADR.
- Outbox va webhook delivery co `LockedBy`, `LockedUntilUtc`, `AttemptId`, rowversion,
  atomic SQL claim va reclaim sau lease timeout.
- Retry exhaustion chuyen sang DLQ. Dashboard hien outbox failure truoc delivery va
  webhook DLQ; manual retry duoc authorize, sanitize va audit.
- Retention worker xoa theo batch chi succeeded queue rows va expired audits; unresolved
  failure/DLQ khong bi xoa tu dong.

### Operations va developer experience

- `/health/live` kiem tra process; `/health/ready` kiem tra SQL, Data Protection va
  Redis khi Redis mode duoc bat.
- Production startup guard reject Sandbox key mode, memory limiter, local SQL,
  wildcard host, missing trusted proxy, missing shared key path/KEK, seeding va
  disabled retention.
- Partner requests co server-generated correlation ID/log scope va request
  count/duration/status metrics. Worker/rate-store queue, result, latency, retention
  va circuit metrics da duoc instrument.
- OpenAPI duoc generate khi build. Postman va docs co contract tests; CI reject artifact
  drift va unversioned breaking removals/narrowing.
- Da co changelog/deprecation policy, Node.js/C# backend examples, partner integration
  guide, threat model, production config example, runbook va k6 profile.

### Database va performance

- `LastUsedAtUtc` duoc conditional-update toi da mot lan trong moi 15 phut thay vi
  `SaveChanges` tren moi request.
- Request audit van la mot append write/request de giu security evidence; retention
  duoc batch hoa. Capacity cua write path nay phai duoc do trong staging.
- Tracking query tiep tuc dung composite unique index `IX_Shipments_TrackingCode`
  (`ShopId`, `TrackingCode`). SQL LocalDB execution plan xac nhan `Index Seek`.
- Due-queue indexes va lease fields duoc them qua hai EF migrations; idempotent
  migration script da tao thanh cong.

## Bang Chung Kiem Tra Hien Tai

| Kiem tra | Ket qua |
| --- | --- |
| `dotnet build ... -c Release --no-restore` | Pass, 0 warning, 0 error |
| Full solution tests | 267 pass, 0 fail, 0 skip |
| Domain/Application/Infrastructure/Web | 22 / 151 / 62 / 32 |
| Shop/cross-client/cross-shop tracking | Pass |
| Public timeline PII/internal-note tests | Pass |
| SSRF, mixed DNS, encoded IP, rebinding, redirect bounds | Pass |
| Trusted/untrusted/multi-hop forwarded headers | Pass |
| Cross-provider Data Protection decrypt | Pass |
| Atomic limiter race/failure/circuit policy | Pass voi shared fake atomic store |
| SQL two-worker claim va expired lease reclaim | Pass tren LocalDB |
| Outbox -> delivery -> signed receiver | Pass, cung event ID |
| Receiver signature/timestamp/duplicate/out-of-order fixture | Pass |
| Health, config guard, OpenAPI/Postman/docs contract | Pass |
| OpenAPI compatibility positive/negative fixture | Pass |
| NuGet vulnerable package scan | Khong phat hien finding da biet |
| EF pending model changes | Khong co |
| Upgrade migration va secret snapshot backfill | Pass tren schema truoc migration moi |
| k6 load/soak | Chua chay; local chua cai `k6` |

## Release Gate Bat Buoc

1. Provision production-like staging voi managed SQL, Redis, shared key ring + KEK,
   it nhat hai app replica va hai worker instance.
2. Test allowlisted client qua ingress that; test direct forged forwarded header bi
   chan; chot exact proxy CIDR va hop limit.
3. Bat default-deny egress va verify SSRF/redirect/DNS-rebinding suite bang controlled
   public sink tu network staging.
4. Tao secret tren replica A, decrypt/send tren replica B, rolling restart, backup va
   restore SQL + key ring trong isolated staging.
5. Chay k6 tracking/create/webhook burst tai SLO target; tiep tuc soak du lau de quan
   sat audit growth, SQL plan, Redis quota va queue recovery.
6. Chaos Redis/SQL/DNS/receiver timeout va kill worker giua lease; xac nhan policy,
   readiness, reclaim, DLQ va alert hoat dong.
7. Cau hinh OpenTelemetry/monitoring exporter phu hop platform; tao dashboard va test
   alert routing cho 5xx, latency, 401/403/429, Redis, queue age, DLQ va webhook rate.
8. Chay dependency/container/SAST/secret scans trong release pipeline va xu ly moi
   Critical/High finding hoac co accepted risk bang van ban.
9. Provision sandbox isolated, cap `ml_test_` key va de mot partner hoan thanh
   create -> track -> webhook -> cancel/reconciliation.
10. Luu certification evidence theo build SHA va lay Security, Product, SRE, Backend,
    QA sign-off truoc canary production.

## Dieu Kien Chuyen Sang Ready

Chi doi ket luan thanh **Ready for public production** khi tat ca release gate tren co
bang chung. Rollout phai theo canary, 1-3 pilot shops va cohort 10% -> 25% -> 50% ->
100%, voi rollback trigger cho error rate, latency, queue age va webhook failure.

Tai lieu lien quan:

- [`architecture/adr-001-partner-api-production-contract.md`](architecture/adr-001-partner-api-production-contract.md)
- [`security/partner-api-threat-model.md`](security/partner-api-threat-model.md)
- [`operations/partner-api-production-runbook.md`](operations/partner-api-production-runbook.md)
- [`partner-api.md`](partner-api.md)
- [`partner-api.openapi.json`](partner-api.openapi.json)
