# Bao Cao Thuc Hien Partner API Production Readiness

Ngay cap nhat: 2026-08-02

## Ket Luan

**Trang thai: code-ready cho production-like staging, CHUA duoc phep go-live public.**

Tat ca hang muc implementation trong repository da duoc xu ly. Cac acceptance
criterion can ha tang that, nhieu replica, load/chaos test, dashboard/alert routing
va phe duyet Security/Product/SRE van la release gate chua hoan thanh.

Quy uoc:

- `[x]`: hoan thanh va da kiem tra trong repository.
- `[~]`: code/artifact da hoan thanh, con staging/ha tang/sign-off.
- `[ ]`: chua the hoan thanh trong moi truong local.

## Bao Cao Theo Task

| ID | Trang thai | Ket qua | Phan con lai |
| --- | --- | --- | --- |
| API-PROD-00 | [~] | Da co ADR, threat model, data-flow, SLO, retention va environment contract | Security/Product/Platform review va topology sign-off |
| API-PROD-01 | [x] | Tracking non-draft theo `ShopId`; ho tro shipment tu UI/CSV/API; `externalOrderId` duoc mask theo API client | Khong |
| API-PROD-02 | [~] | Timeline public dung message mapper chung, khong tra internal note/PII | Privacy sign-off doc lap |
| API-PROD-03 | [~] | HTTPS-only, chan private/reserved/metadata IP, mixed DNS, redirect va DNS rebinding; gioi han timeout/header/body/connection | Verify egress firewall va sink test tren staging |
| API-PROD-04 | [~] | Trusted proxy options, forwarded header middleware va spoofing tests | Test qua ingress/load balancer that |
| API-PROD-05 | [~] | Shared Data Protection key ring, fixed app name, certificate KEK, startup/readiness guard va cross-provider decrypt test | Rolling restart va backup/restore drill |
| API-PROD-06 | [~] | Redis Lua atomic quota, key theo env/client/action/window, timeout, circuit breaker va fail policy | Managed Redis test qua it nhat 2 replica |
| API-PROD-07 | [~] | SQL atomic claim/lease, rowversion, reclaim, DLQ, audited retry, secret snapshot/version, metrics va retention | Multi-worker soak/kill test va DLQ alert |
| API-PROD-08 | [~] | `/health/live`, `/health/ready`, production config guard, correlation scope, meters va runbook | Telemetry exporter, dashboard, alert routing, restore/rollback drill |
| API-PROD-09 | [~] | `ml_test_`/`ml_live_`, cross-environment reject, secret entropy va rotation-safe pending delivery | Provision isolated sandbox va partner acceptance flow |
| API-PROD-10 | [x] | Build-generated OpenAPI, Postman drift test, changelog/version headers, Node/C# examples va CI compatibility gate | Khong |
| API-PROD-11 | [~] | Throttle `LastUsedAtUtc`, retention cho audit, tracking index/query-plan test | Production-like SQL benchmark va audit write-volume measurement |
| API-PROD-12 | [~] | Functional, IDOR, SSRF, proxy, rate race, lease, E2E signed receiver, replay/out-of-order fixture va health/contract tests | k6 load/soak, chaos, container/SAST/secret scans tren staging/CI |
| API-PROD-13 | [ ] | Runbook va rollout sequence da co | Toan bo staging certification va Security/Product/SRE sign-off |

## Ket Qua Da Ban Giao

- Tracking read authorization theo shop; create/cancel/idempotency van theo API client.
- Public timeline deterministic voi `messageCode`, `message`, `locale=vi-VN`; khong
  fallback sang `ShipmentStatusHistory.Note`.
- Webhook SSRF hardening, secret version snapshot, at-least-once worker lease va DLQ.
- Redis rate limit atomic va production startup guard khong cho memory fallback.
- Shared Data Protection config, health checks, retention worker va telemetry meters.
- OpenAPI/Postman/docs/backend examples/load profile/CI contract checks.
- EF migrations:
  - `20260802040549_AddPartnerApiProductionReadiness`
  - `20260802041446_AddWebhookSecretVersion`

## Bang Chung Kiem Tra

- Release build: `0` warning, `0` error.
- Full test suite: `267` passed, `0` failed, `0` skipped.
  - Domain: `22`
  - Application: `151`
  - Infrastructure: `62`
  - Web: `32`
- SQL LocalDB tests: shop tracking query plan dung `IX_Shipments_TrackingCode`,
  two-worker atomic claim va expired-lease reclaim deu pass.
- OpenAPI compatibility checker: contract hien tai pass; endpoint removal fixture bi
  reject voi exit code `1`.
- NuGet vulnerability scan: khong phat hien vulnerable package da biet.
- EF model: khong co pending model changes; idempotent migration script tao thanh cong.
- Migration upgrade test: schema cu duoc migrate len ban moi va backfill dung secret
  snapshot/version cho delivery cung outbox dang cho xu ly.
- k6: profile da tao nhung may local chua cai `k6`, do do chua co load/soak evidence.

## Release Gate Con Lai

- [ ] Provision staging production-like: SQL, Redis, shared key ring + KEK, it nhat
  2 app replica va 2 worker instance.
- [ ] Verify trusted proxy/IP whitelist qua ingress that va forged header bi chan.
- [ ] Verify default-deny webhook egress, SSRF sink va DNS rebinding tren network that.
- [ ] Chay rolling restart va backup/restore Data Protection key ring.
- [ ] Chay k6 load/soak, Redis/SQL/DNS/receiver chaos va worker crash/reclaim drill.
- [ ] Export meters vao monitoring backend; tao dashboard va test alert den on-call.
- [ ] Chay container, dependency, SAST va secret scan; xu ly moi Critical/High finding.
- [ ] Hoan thanh sandbox partner flow va Security/Product/SRE sign-off.
- [ ] Rollout theo canary/pilot/cohort trong production runbook.

Chi chuyen trang thai sang **Ready for public production** sau khi tat ca checkbox
release gate co bang chung va nguoi co tham quyen ky duyet.
