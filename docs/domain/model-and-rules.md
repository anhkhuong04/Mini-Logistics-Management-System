# Domain Model and Rules

This document describes business invariants that callers must preserve. Field validation and transport contracts remain authoritative in validators, domain types, tests, and the generated OpenAPI.

## Aggregate ownership

- `Shipment` owns status transitions, status history, fee snapshot, and assignment history.
- `CodTransaction` has a one-to-one relationship with a shipment and owns collection/settlement state.
- `Shop` owns merchant identity and status; shop staff membership/preferences/notifications are separate records scoped to it.
- `FeeRule`, `RouteRegionConfig`, `Hub`, and `ShipperWorkingArea` are operational configuration used when classifying, pricing, and assigning shipments.
- `ApiClient`, `ExternalShipmentReference`, `WebhookEndpoint`, and `WebhookDelivery` form the partner-integration boundary.
- `OutboxMessage` is delivery infrastructure state, not the source of truth for shipment status.

## Shipment lifecycle

`Draft` exists for import preparation. Direct UI/API creation produces a non-draft shipment and the application attempts auto-assignment.

```text
Draft ---------> PendingPickup -----> Assigned -----> PickingUp -----> PickedUp
  |                   |                  |                                  |
  +-> Cancelled       +-> Cancelled     +-> Cancelled                      v
                                                                        InTransit
                                                                           |
                                                                           v
                                                                       Delivering
                                                                       /    |    \
                                                              Delivered  Failed  Returned
                                                                          /   \
                                                                Delivering   Returned

PickedUp and InTransit may also transition to Returned.
```

Rules:

- The Domain rejects transitions not represented above.
- `DeliveryFailed` requires a non-empty note/failure explanation.
- `Delivered`, `Returned`, and `Cancelled` are terminal.
- Every accepted transition appends status history with actor and timestamp. Failure reason and GPS evidence are stored only when supplied by the use case.
- Application services must authorize the actor before invoking a transition. Admin/Operator permissions and assigned-Shipper access are separate paths.
- Public and partner timelines use a safe mapper; they do not expose internal history notes, actor IDs, GPS, phones, email, or addresses.

## Assignment and capacity

- A shipment can have historical assignments but no more than one active assignment. The database enforces this with a filtered unique index on `ShipmentId` where `UnassignedAtUtc` is null.
- Auto-assignment is attempted only for `PendingPickup` shipments.
- Selection considers an active shipper, availability for assignment, pickup-area eligibility, and current load below `MaxActiveShipments`.
- No eligible shipper is a valid outcome: the shipment remains `PendingPickup` for Operations to resolve.
- Manual assignment/reassignment is an authorized operational override and must leave assignment history intact.
- `Returned` and `Cancelled` deactivate the current assignment.
- `Delivered` deactivates it immediately when no COD is required. When COD is pending, the assignment remains active until collection is completed so the shipper can finish that responsibility.

Capacity selection currently uses read/check/write logic. There is no shipment-level optimistic concurrency token; concurrent dispatch changes require careful testing and database-constraint handling.

## Route classification and fees

Routes are classified as `IntraProvince`, `IntraRegion`, or `InterRegion` using normalized pickup/delivery provinces and active route-region configuration.

Fee calculation follows these concepts:

```text
volumetricWeightKg = lengthCm * widthCm * heightCm / 5000
chargeableWeightKg = max(actualWeightKg, volumetricWeightKg)
total = baseFee + extraWeightFee + insuranceFee + returnFee
```

- The active fee rule for the route determines base weight/fee and extra-weight steps.
- Insurance and return-fee policies are domain rules, not UI calculations.
- The application recalculates the fee server-side even if the UI displayed a preview.
- The full fee breakdown, chargeable weight, route type, and return-fee rate are stored on the shipment. Later configuration changes must not retroactively change that snapshot.
- A return transition applies the shipment's snapshotted return policy, rather than looking up a new rule.

Do not treat seeded fee values as permanent business policy. The persisted/configured rules and domain policy are authoritative.

## Cash on delivery

- A COD transaction is created with every shipment: zero amount is `NotRequired`; a positive amount starts as `PendingCollection`.
- Collection is allowed only after the shipment is `Delivered`.
- The collected amount may differ from the expected amount; the discrepancy and collection note are retained.
- Completing collection deactivates the shipment assignment that was kept active for COD.
- Settlement is separate from collection and requires the authorized admin/COD-settlement workflow.
- COD history and admin audit entries must remain traceable. UI visibility alone must never grant collect/settle permission.

## Shop and integration isolation

- Shop owners and staff may act only through an active membership/permission path for the target shop.
- An API client is bound to one shop and may call only granted scopes.
- Partner tracking is shop-scoped, so a client may track UI/import/API-created shipments in its shop when it has `TrackShipment`.
- Create/cancel ownership, external order IDs, response snapshots, and idempotency are client-scoped. The pair `(ApiClientId, IdempotencyKey)` and `(ApiClientId, ExternalOrderId)` are unique.
- Reusing an idempotency key with an identical request returns the stored response; reusing it with a different request is a conflict.
- Cross-shop lookups must not reveal whether a shipment exists.

## Asynchronous events

- Shipment/integration services queue outbox records in the same EF Core unit of work as the business records they accompany.
- Workers turn outbox records into notifications or webhook deliveries, then send webhooks asynchronously.
- Delivery is at-least-once. Stable event IDs and consumer deduplication are required; ordering across retries is not guaranteed.
- Failed work is retried with backoff and eventually remains failed/dead-lettered for operational intervention.

The detailed mechanism and trade-offs are recorded in [ADR-003](../architecture/adr-003-transactional-outbox-and-webhooks.md).
