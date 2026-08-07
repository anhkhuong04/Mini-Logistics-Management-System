# Simulated Bug Reports - Interview Sample

Document ID: ML-QA-BUG-001  
Version: 1.0  
Status: SIMULATED - not observed in production

## Usage note

Every report below is an intentionally seeded scenario based on a realistic logistics risk. The “actual result,” environment, attachments, resolution, and retest are fictional examples. Before presenting one, explicitly call it a simulated portfolio defect.

Severity describes user/business impact; priority describes recommended fix order.

## SIM-BUG-001 - Duplicate shipment created after repeated submit

| Field | Value |
|---|---|
| Module | Shop - Create Shipment |
| Severity / Priority | High / P0 |
| Type | Data integrity / concurrency |
| Environment | Simulated local build, Edge 138, SQL Server LocalDB |
| Related cases | ML-SHP-001, ML-API-004 |
| Status | Open (simulated) |

Precondition: Shop user is signed in and the create form contains valid synthetic data.

Steps:

1. Throttle the browser network to Slow 3G.
2. Click **Create shipment** twice before navigation completes.
3. Open the shipment list and query by receiver phone `0911115678`.

Expected: The UI prevents a repeated submit or the server treats it idempotently; exactly one shipment exists.

Simulated actual: Two shipments with different tracking codes are created from one user intent.

Impact: Duplicate pickup and COD collection risk; customer may be charged twice.

Evidence placeholder: `SIM-BUG-001_create-network.har`, UI screenshot with two tracking codes, redacted SQL result.

Suggested fix/verification: Disable submit while processing and introduce a server-side request token; retry with double-click, browser refresh, and network timeout scenarios.

## SIM-BUG-002 - Public summary exposes receiver name before verification

| Field | Value |
|---|---|
| Module | Public Tracking |
| Severity / Priority | High / P0 |
| Type | Privacy / authorization |
| Environment | Simulated anonymous Chrome session |
| Related cases | ML-TRK-001, ML-TRK-006 |
| Status | Open (simulated) |

Steps:

1. Sign out and open `/tracking` in an incognito window.
2. Search `MLTEST202608060001` without entering phone last four digits.
3. Inspect the summary card and page source/network response.

Expected: Only non-sensitive shipment summary fields are returned; sender/receiver identity, phone, and full addresses are hidden until verification.

Simulated actual: Receiver full name appears in the summary response and UI.

Impact: Anyone who obtains or guesses a tracking code can learn personal information.

Evidence placeholder: Redacted response JSON and screenshot; never attach real customer data.

Suggested fix/verification: Map a dedicated summary DTO with an allow-list; verify UI, raw response, query-string auto-search, and error responses.

## SIM-BUG-003 - Wrong phone last four remains verified after a second lookup

| Field | Value |
|---|---|
| Module | Public Tracking |
| Severity / Priority | High / P1 |
| Type | Privacy / stale UI state |
| Environment | Simulated Blazor interactive session |
| Related cases | ML-TRK-003, ML-TRK-004 |
| Status | Fixed and retested (simulated) |

Steps:

1. Verify shipment A with correct last four digits `1234`.
2. Search shipment B in the same browser tab.
3. Enter an incorrect value `9999` for shipment B.

Expected: Shipment B remains summary-only and all verified fields from shipment A are cleared immediately.

Simulated actual: The verified badge and full address from shipment A remain visible after the failed request for shipment B.

Root-cause hypothesis: Result model is not cleared before the verification request completes.

Simulated retest: Passed after clearing verified state on tracking-code change and before verification; regression covered success, failure, throttling, and back navigation.

## SIM-BUG-004 - DeliveryFailed accepted without a reason through direct request

| Field | Value |
|---|---|
| Module | Operations - Status Update |
| Severity / Priority | Medium / P1 |
| Type | Server validation / auditability |
| Environment | Simulated local API request |
| Related cases | ML-OPS-003 |
| Status | Fixed and retested (simulated) |

Steps:

1. Prepare a shipment in `Delivering`.
2. Submit a status-update request with `newStatus=DeliveryFailed` and `note="   "`.
3. Reload the shipment timeline and inspect the database record.

Expected: Server rejects the request and status remains `Delivering`.

Simulated actual: Status becomes `DeliveryFailed` with an empty history note.

Impact: Operations cannot distinguish recipient absence, wrong address, refusal, or another failure reason.

Simulated retest: Passed for null, empty, whitespace, and valid notes; invalid requests create no history row.

## SIM-BUG-005 - COD can be marked collected while shipment is InTransit

| Field | Value |
|---|---|
| Module | COD Collection |
| Severity / Priority | High / P0 |
| Type | Business rule / financial integrity |
| Environment | Simulated operator workspace |
| Related cases | ML-COD-001, ML-COD-002 |
| Status | Open (simulated) |

Steps:

1. Create a COD shipment for `250000 VND` and move it to `InTransit`.
2. Invoke the collection action using a stale browser tab or crafted request.
3. Refresh the shipment and COD report.

Expected: Collection is rejected until shipment status is `Delivered`.

Simulated actual: COD becomes `Collected` and disappears from the pending report.

Impact: Financial reconciliation becomes incorrect and the shipper assignment may close prematurely.

Suggested verification: Enforce the invariant in the domain/service transaction; test concurrency and transaction rollback.

## SIM-BUG-006 - Same idempotency key with different payload returns original success

| Field | Value |
|---|---|
| Module | Partner API - Create Shipment |
| Severity / Priority | High / P0 |
| Type | API reliability / data integrity |
| Environment | Simulated Postman collection |
| Related cases | ML-API-004, ML-API-005 |
| Status | Fixed and retested (simulated) |

Steps:

1. POST `/api/v1/partner/shipments` using idempotency key `interview-create-0001` and receiver A.
2. Repeat with the same key but receiver B and a different COD amount.

Expected: Second request returns `409` idempotency conflict and explains that the key was reused with a different request fingerprint.

Simulated actual: Second request returns `200` with the first shipment, making the partner believe receiver B was accepted.

Simulated retest: Passed for identical replay, changed nested address, changed COD, and concurrent duplicate requests.

## SIM-BUG-007 - Track-only client can read another shop's shipment

| Field | Value |
|---|---|
| Module | Partner API - Tracking |
| Severity / Priority | Critical / P0 |
| Type | Broken object-level authorization |
| Environment | Simulated clients for Shop A and Shop B |
| Related cases | ML-API-003, ML-API-006 |
| Status | Open (simulated) |

Steps:

1. Create a shipment under Shop B and record its synthetic tracking code.
2. Authenticate as a Shop A API client with `TrackShipment` scope.
3. GET `/api/v1/partner/shipments/{shopBTrackingCode}`.

Expected: `404` or contract-defined denial without revealing ownership or PII.

Simulated actual: `200` returns Shop B shipment details.

Impact: Cross-tenant data exposure. Treat as release-blocking and avoid copying real response data into public evidence.

Suggested fix/verification: Query by tracking code plus authenticated shop ID; add integration coverage for UI, CSV, and API-created shipments.

## SIM-BUG-008 - Rate-limit response has no actionable retry metadata

| Field | Value |
|---|---|
| Module | Partner API - Rate Limiting |
| Severity / Priority | Low / P2 |
| Type | API contract / usability |
| Environment | Simulated k6/Postman run |
| Related cases | ML-API-008 |
| Status | Accepted for next iteration (simulated) |

Steps:

1. Exceed the configured quote limit for one API client.
2. Inspect the `429` response headers and body.

Expected: Response includes a usable retry interval such as `Retry-After`, a stable error code, and a correlation ID.

Simulated actual: Only a generic message is returned, so clients cannot schedule a safe retry.

Impact: Partners may retry aggressively and extend the outage/throttling window.

## Defect review checklist

- Reproduce on the named build before changing status from Draft/Open.
- Attach only redacted evidence; never publish bearer tokens, connection strings, or real PII.
- Keep severity separate from scheduling priority.
- Verify the fix and one adjacent regression path.
- If a scenario cannot be reproduced, label it `Not reproducible` rather than silently closing it.

