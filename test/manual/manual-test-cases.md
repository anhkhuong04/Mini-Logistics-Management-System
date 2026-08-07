# Manual Test Cases - Mini Logistics Management System

Document ID: ML-QA-TC-001  
Version: 1.0  
Status: Portfolio sample  
Basis: Current repository requirements and implementation as reviewed on 2026-08-06

## Test approach

The suite uses equivalence partitioning, boundary value analysis, negative testing, state-transition testing, role/permission testing, and API contract checks. It is designed for a local seeded environment; all values below are synthetic.

Common preconditions:

- Application and SQL Server are running locally.
- Demo data is seeded using local-only secrets.
- Test users exist for Shop, Operator/Admin, and Shipper roles.
- Partner API client scopes are configured per case.
- Browser cache is cleared before authentication/privacy cases.

Common test data:

| Field | Synthetic value |
|---|---|
| Sender | Nguyen Test A / `0900001234` |
| Receiver | Tran Test B / `0911115678` |
| Pickup | `1 Test Street`, `Ben Nghe`, `Ho Chi Minh City`, `Vietnam` |
| Delivery | `2 Sample Road`, `My Binh`, `An Giang`, `Vietnam` |
| Tracking code | `MLTEST202608060001` |
| External order ID | `INTVW-ORDER-0001` |
| API key | `<TEST_API_KEY>` |
| Idempotency key | `interview-create-0001` |

## A. Shipment creation and fee calculation

| ID | Scenario / technique | Pri. | Steps | Expected result |
|---|---|---:|---|---|
| ML-SHP-001 | Create a valid shipment / positive, smoke | P0 | Sign in as Shop; open `/shipments/create`; enter all valid data; submit once. | One shipment is created in `PendingPickup`; a tracking code and fee breakdown are shown; persisted values match the submitted data. |
| ML-SHP-002 | Required sender name / negative | P1 | Leave sender name empty; complete other fields; submit. | Inline validation is shown; no shipment or COD record is created. |
| ML-SHP-003 | Sender name length 100 and 101 / BVA | P1 | Submit once with 100 characters; repeat with 101. | 100 is accepted; 101 is rejected; no truncation or partial save occurs. |
| ML-SHP-004 | Phone equivalence partitions / EP | P1 | Try 8 digits, 9 digits, 15 digits, 16 digits, letters, and `+84900001234`. | Only 9-15 digits, optionally prefixed with `+`, are accepted; spaces may be normalized; invalid values are rejected. |
| ML-SHP-005 | Weight boundary / BVA | P0 | Submit with `0`, `-0.001`, `0.001`, and `9999` kg. | Non-positive values are rejected; valid positive boundary values follow UI constraints and server validation. |
| ML-SHP-006 | Parcel dimension boundary / BVA | P1 | Set length, width, or height to `0`, then to `0.01`. | Zero is rejected; positive values are accepted; fee preview recalculates consistently. |
| ML-SHP-007 | Negative money values / negative | P0 | Enter `-1` for goods value and COD amount in separate attempts. | Validation prevents submission and no negative monetary data is persisted. |
| ML-SHP-008 | Volumetric weight fee / calculation | P1 | Use actual weight `1 kg`, dimensions `50 x 40 x 30 cm`; request preview; create shipment. | Chargeable weight is `max(actual, LxWxH/5000)` = 12 kg; saved server-side fee matches the displayed rule inputs. |
| ML-SHP-009 | Insurance threshold / BVA | P1 | Compare goods values `999999`, `1000000`, and `20000000` VND. | Insurance fee is zero below threshold and follows the configured 0.5% rule at/above threshold without exceeding the cap policy. |
| ML-SHP-010 | Note length 500 and 501 / BVA | P2 | Submit with a 500-character note; repeat with 501. | 500 is accepted; 501 is rejected; the accepted note is preserved exactly after trimming rules. |

## B. Assignment, status lifecycle, and COD

| ID | Scenario / technique | Pri. | Steps | Expected result |
|---|---|---:|---|---|
| ML-OPS-001 | Happy-path lifecycle / state transition, smoke | P0 | Create shipment; assign shipper; advance `PickingUp -> PickedUp -> InTransit -> Delivering -> Delivered`. | Every transition succeeds once, history is chronological, and final status is `Delivered`. |
| ML-OPS-002 | Skip lifecycle state / negative transition | P0 | From `Assigned`, attempt to set `InTransit`. | Request is rejected; status remains `Assigned`; no misleading history entry is added. |
| ML-OPS-003 | Delivery failure requires note / validation | P0 | From `Delivering`, select `DeliveryFailed`; submit blank/whitespace note, then a valid note. | Blank note is rejected; valid note succeeds and appears in history. |
| ML-OPS-004 | Retry after delivery failure / state transition | P1 | Move `Delivering -> DeliveryFailed` with reason; then retry to `Delivering`. | Retry transition succeeds and both events are retained in order. |
| ML-OPS-005 | Cancel before pickup / positive | P0 | Cancel a `PendingPickup` shipment with a reason. | Status becomes `Cancelled`; active assignment, if applicable in a cancellable state, is deactivated; further updates are blocked. |
| ML-OPS-006 | Cancel after pickup / negative transition | P0 | Move shipment to `PickedUp`; attempt cancellation. | Cancellation is rejected; shipment remains `PickedUp`; active assignment remains consistent. |
| ML-OPS-007 | Terminal state immutability / state transition | P0 | For `Delivered`, `Returned`, and `Cancelled` shipments, attempt another status update. | Each update is rejected and no extra history record is written. |
| ML-OPS-008 | Reassign only while assigned / negative | P1 | Attempt reassignment in `PendingPickup`; assign, then reassign; attempt again after `PickingUp`. | Only the `Assigned` case succeeds; exactly one active assignment remains. |
| ML-COD-001 | COD collection after delivery / positive | P0 | Create COD shipment; complete delivery; mark COD collected. | COD becomes `Collected`; timestamp/user are recorded; active assignment is deactivated. |
| ML-COD-002 | COD collection before delivery / negative | P0 | On `InTransit`, attempt to mark COD collected. | Action is rejected; COD remains pending and assignment remains active. |
| ML-COD-003 | Delivered non-COD shipment / business rule | P1 | Create shipment with COD `0`; deliver it. | No collection is required and the active assignment is deactivated immediately. |
| ML-COD-004 | Return fee / calculation | P1 | Move an eligible shipment to `Returned`; compare original and final fee breakdown. | Return fee equals 50% of base plus extra-weight fee; it is applied once; assignment is inactive. |

## C. Public tracking and privacy

| ID | Scenario / technique | Pri. | Steps | Expected result |
|---|---|---:|---|---|
| ML-TRK-001 | Valid summary lookup / smoke | P0 | Open `/tracking`; enter a valid tracking code; search without phone verification. | Status, route provinces, and safe timeline appear; phone numbers and full addresses remain hidden. |
| ML-TRK-002 | Unknown tracking code / negative | P1 | Search a well-formed nonexistent code. | Generic not-found feedback appears and no shipment/customer information is disclosed. |
| ML-TRK-003 | Last-four verification / positive | P0 | Search valid code; enter sender or receiver phone last four digits. | Access level changes to verified and full permitted detail is displayed. |
| ML-TRK-004 | Invalid last-four partitions / EP | P1 | Try blank, 3 digits, 5 digits, letters, and wrong 4 digits. | Format errors are distinguished from non-match feedback; detail stays hidden. |
| ML-TRK-005 | Tracking rate limit / abuse | P1 | Repeat lookup/verification until configured limit is exceeded. | Request is throttled with retry guidance; no detail leaks; normal access resumes after the window. |
| ML-TRK-006 | Query-string lookup privacy / security | P1 | Open `/tracking?code=<valid>` in a new anonymous session; inspect summary before verification. | Auto-search follows the same privacy rules as manual search and does not expose full PII. |

## D. Partner API

Base URL: `https://localhost:7195/api/v1/partner`

| ID | Scenario / technique | Pri. | Steps | Expected result |
|---|---|---:|---|---|
| ML-API-001 | Create with valid scope / positive, smoke | P0 | POST `/shipments` with bearer API key, create scope, idempotency key, and valid JSON. | First request returns `201`; response schema is valid; shipment belongs to the authenticated shop. |
| ML-API-002 | Missing/invalid API key / auth negative | P0 | Call each endpoint without a key and with an invalid key. | `401` is returned with safe error payload; no protected data or secret detail is disclosed. |
| ML-API-003 | Insufficient scope / authorization | P0 | Use quote-only key to create, track, and cancel. | Each unsupported action returns `403`; no state change occurs. |
| ML-API-004 | Idempotent replay / reliability | P0 | Send the same create payload twice with the same idempotency key. | First response is `201`; replay returns `200` and the same shipment; only one database record exists. |
| ML-API-005 | Idempotency conflict / negative | P0 | Reuse the same idempotency key with a materially different payload. | `409` is returned; original shipment is unchanged; no second shipment is created. |
| ML-API-006 | Tracking tenant isolation / authorization | P0 | Client A attempts to read a tracking code owned by Shop B. | Access is denied/not found according to the contract; no cross-shop PII, external order ID, or timeline leaks. |
| ML-API-007 | Cancel conflict after pickup / business rule | P0 | Move shipment to `PickedUp`; POST its cancel endpoint. | `409` is returned and shipment status/history remain unchanged. |
| ML-API-008 | Rate-limit contract / negative | P1 | Exceed quote, create, track, and cancel limits separately. | `429` is returned for the exhausted bucket; other operation buckets behave independently; retry metadata is usable. |

## Detailed evidence checklist

For every executed case, record:

- Build/commit and environment.
- Account role or API client scope.
- Request and response with secrets redacted.
- Relevant UI screenshot or database query result.
- Actual result, status, executor, and timestamp.
- Defect ID for failed cases and retest evidence after a fix.

## Traceability summary

| Requirement area | Test cases | Repository evidence |
|---|---|---|
| Input validation and fee calculation | ML-SHP-001 to ML-SHP-010 | `CreateShipmentCommandValidator`, `README` fee rules |
| Lifecycle and terminal states | ML-OPS-001 to ML-OPS-008 | `Shipment.UpdateStatus`, `Shipment.Cancel`, state-machine tests |
| COD and assignment completion | ML-COD-001 to ML-COD-004 | `Shipment.CompleteCodCollection`, assignment deactivation rules |
| Public privacy verification | ML-TRK-001 to ML-TRK-006 | `Tracking.razor`, public tracking service |
| Partner API contract/security | ML-API-001 to ML-API-008 | `PartnerApiEndpoints`, OpenAPI artifact and contract tests |

