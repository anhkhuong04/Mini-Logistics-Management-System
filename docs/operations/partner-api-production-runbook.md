# Partner API Production Runbook

This runbook is the operator checklist for sandbox, staging, and production. Values
shown as placeholders must come from the platform secret manager or deployment
configuration, never from the repository or container image.

## Required Configuration

Use `deploy/partner-api.production.example.json` for non-secret shape. Supply secrets
through environment variables or the platform secret provider:

```text
ConnectionStrings__DefaultConnection
ConnectionStrings__Redis
DataProtection__CertificatePassword
```

Required production values:

- `ASPNETCORE_ENVIRONMENT=Production`
- `PartnerApi__Environment=Live`
- `PartnerApi__RateLimiting__Mode=Redis`
- `PartnerApi__Retention__Enabled=true`
- `PublicBaseUrl=https://<public-host>` and a restricted `AllowedHosts`
- At least one `TrustedProxy__KnownProxies__N` or `KnownNetworks__N`
- Shared `DataProtection__KeysPath`, fixed environment-specific `ApplicationName`,
  and readable PKCS#12 certificate path
- Production SQL and Redis connection strings
- `Seeding__Enabled=false`

Startup must fail if these invariants are not met. Do not override the guard to make
a deployment green.

## Ingress and Client IP

1. Record the load balancer's source IP/CIDR, not the public client CIDR, in trusted
   proxy configuration.
2. Set `ForwardLimit` to the exact number of trusted hops.
3. Preserve `X-Forwarded-For` and `X-Forwarded-Proto` symmetrically.
4. Verify through the real ingress that an allowlisted partner IP succeeds.
5. Send the same request directly with a forged header and verify `403`.

## Deployment

1. Back up SQL and the Data Protection key ring.
2. Generate and review the EF migration script:

   ```powershell
   dotnet ef migrations script --idempotent --project src/MiniLogistics.Infrastructure --startup-project src/MiniLogistics.Web --output artifacts/partner-api-migration.sql
   ```

3. Apply migrations as a one-off deployment job; application replicas must not race
   schema migration on startup.
4. Deploy one canary replica with readiness disabled at ingress.
5. Check `/health/live` and `/health/ready`, then enable canary traffic.
6. Verify authentication, tracking of API/UI/CSV shipments, one signed test webhook,
   Redis quota, and queue metrics.
7. Roll through remaining replicas. Keep at least one old healthy replica until the
   compatibility checks pass.

Rollback the application if 5xx, p95 latency, queue age, or DLQ exceeds the rollout
threshold. Database rollback must use a reviewed down/forward-fix script; do not run
an automatic destructive downgrade.

## Data Protection

- Mount the same key directory read/write on every replica.
- Keep the wrapping certificate private key in the secret store/mounted secret.
- Back up the full key ring before key or certificate changes.
- To rotate the KEK, deploy code/config capable of reading old keys, create/protect a
  new Data Protection key, verify all replicas can decrypt an existing webhook
  secret, then retire the old KEK only after the key-ring retention window.
- Restore drill: restore SQL plus the matching key-ring backup into isolated staging,
  start two replicas, and send a test delivery from an endpoint created before backup.

Never delete old Data Protection keys while protected webhook secrets still exist.

## Redis Rate Limiting

- Alert on `partner_api.rate_limit_store.errors` and
  `partner_api.rate_limit_store.circuit_open`.
- Create/cancel fail closed with `503 PartnerApi.RateLimitUnavailable` during outage.
- Quote/tracking fail open to preserve read availability; monitor abuse and ingress
  gateway limits while Redis is unavailable.
- After recovery, verify atomic Lua execution and that accepted requests across two
  replicas do not exceed the configured quota.

## Outbox and Webhook Recovery

Observe meter `MiniLogistics.PartnerWorkers`:

- `partner_worker.claimed`
- `partner_worker.results` by queue/status
- `partner_worker.queue.age`
- `partner_webhook.delivery.duration`
- `partner_worker.retention.deleted`

For failed/dead-lettered rows:

1. Correlate event/delivery ID with structured server logs.
2. Resolve endpoint, DNS, secret, SQL, or payload cause.
3. In `/partner/integrations`, retry the webhook delivery or pre-delivery outbox row.
4. Confirm the action appears in credential/admin audit.
5. Confirm the same event ID is not processed concurrently and queue age declines.

Do not edit payload JSON or protected signing secret in SQL. Dead-letter rows are not
automatically deleted.

## Webhook Egress

- Default-deny network egress except DNS and approved public HTTPS destinations.
- The webhook HTTP client bypasses system proxy settings so connect-time DNS/IP
  validation cannot be delegated around. A required corporate egress proxy needs a
  separately reviewed design and must not be enabled through ambient proxy variables.
- Keep automatic redirects disabled.
- Test loopback, RFC1918/ULA, link-local, metadata, integer/hex IP, mixed DNS, and DNS
  rebinding from controlled staging.
- A valid public HTTPS sink must still receive the signed event.

## Health and Alerts

`/health/live` checks process liveness. `/health/ready` checks SQL, Data Protection,
and Redis when Redis mode is enabled. Route traffic only on ready replicas.

Minimum alerts:

| Alert | Initial trigger |
| --- | --- |
| Partner API 5xx | > 0.5% for 5 minutes |
| Tracking p95 | > 300 ms for 10 minutes |
| 401/403 spike | > 3x seven-day baseline |
| 429 spike | > 5% for 10 minutes |
| Redis store errors | Any sustained for 2 minutes |
| Outbox/webhook max age | > 2 minutes |
| DLQ | Any new row |
| Webhook success | < 95% for 15 minutes, excluding known receiver incident |
| SQL pool/query latency | p95 over platform baseline for 10 minutes |

Each alert must link to this runbook and route to the current on-call channel. Alert
routing and restore/rollback evidence are staging certification items.

## Credential Incident

1. Deactivate or rotate the affected API client immediately.
2. Review credential audit, source IP, status codes, and affected shipment IDs. Do not
   paste the leaked key into tickets or chat.
3. Rotate the webhook secret if compromise is possible; pending deliveries retain
   their queued secret version, so coordinate receiver overlap.
4. Issue an environment-correct replacement key and least-privilege scopes.
5. Document impact, notify the shop, and preserve audit evidence.

## Certification Evidence

Record date, build SHA, topology, config checksum with secrets removed, test report,
k6 summary, security scan, ingress/egress proof, Redis/SQL chaos result, key restore,
rollback drill, dashboard screenshots, alert delivery, and Security/Product/SRE
sign-offs. Production rollout must not begin without this evidence.
