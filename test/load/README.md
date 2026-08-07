# Partner API Load and Chaos Validation

The k6 profile encodes the ADR tracking targets. Run only against an isolated,
production-like environment with synthetic shops and data.

```powershell
$env:BASE_URL = "https://staging-api.example"
$env:API_KEY = "<ml_test_key>"
$env:TRACKING_CODE = "<synthetic_non_terminal_tracking_code>"
$env:TRACKING_RPS = "100"
$env:DURATION = "10m"
k6 run test/load/partner-api.k6.js --summary-export artifacts/k6-summary.json
```

At high aggregate RPS, provide comma-separated `API_KEYS` for multiple synthetic API
clients or explicitly raise the staging-only tracking quota. Do not mistake expected
`429` responses from one client's 120/minute default quota for database capacity.

Write load is disabled by default because it creates real staging rows:

```powershell
$env:ENABLE_WRITE_LOAD = "true"
$env:CREATE_RPS = "5"
k6 run test/load/partner-api.k6.js
```

Capture app/SQL/Redis/worker metrics during the run. Repeat with two or more app and
worker replicas. Required staging drills:

1. Restart Redis and verify create/cancel fail closed while tracking follows the
   approved fail-open policy and the circuit metrics fire.
2. Kill one worker after claim and verify lease reclaim without simultaneous send.
3. Add receiver latency/timeouts and a webhook burst; queue age must recover.
4. Change controlled DNS from public to private and verify connect is blocked.
5. Rolling-restart app replicas and verify old webhook secrets remain decryptable.
6. Exercise SQL failover and verify readiness removes unhealthy replicas.

Store build SHA, topology, k6 summary, dashboards, alert evidence, and any accepted
risk with the staging certification record. A local script file is not load-test
evidence by itself.
