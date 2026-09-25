# Product Overview

## Purpose

Mini Logistics manages the operational lifecycle of small-parcel shipments from shop creation through pickup, delivery or return, cash-on-delivery (COD) reconciliation, and customer tracking. It supports both human-operated workflows and a versioned Partner API for e-commerce integrations.

The current product is a single-deployment modular monolith. It is intended to keep shipment, assignment, fees, COD, identity, and partner-delivery behavior consistent without introducing distributed transaction boundaries.

## Actors and access boundaries

| Actor | Primary responsibilities | Data boundary |
| --- | --- | --- |
| Shop owner | Manage the shop, staff, shipments, imports, labels, notifications, audit views, COD reports, and integrations | Own shop only |
| Shop staff | Perform delegated shop tasks | Membership permissions within one shop |
| Operator | Dispatch, assign/reassign, retry auto-assignment, monitor operations, and update permitted statuses | Operational permissions, not shop ownership |
| Shipper | Work assigned shipments, capture outcome/evidence, and confirm COD collection | Active assignments belonging to that shipper |
| Admin | Manage users, shops, hubs, configuration, COD settlement, audits, and dashboards | Explicit admin/operation permissions |
| Integration admin | Manage Partner API clients and webhook configuration when granted | Explicit integration-management scope |
| API client | Quote, create, track, or cancel through granted scopes | Bound shop; some write/idempotency data is also bound to the originating client |
| Public visitor/receiver | Track a shipment using its tracking code, with restricted detail | Public-safe projection only |

Authorization is enforced in application use cases and endpoint policies. Hiding a UI control is not an authorization boundary.

## Supported journeys

### Shop shipment journey

1. A shop owner or permitted staff member creates a shipment directly or confirms a validated import batch.
2. The server normalizes addresses, classifies the route, recalculates the fee, creates the shipment and COD record, and attempts auto-assignment.
3. If no eligible shipper is available, the shipment remains `PendingPickup` for Operations.
4. Operations may retry assignment or manually assign/reassign a shipper.
5. The assigned shipper advances the shipment through pickup and delivery states. Application authorization restricts a shipper to their active assignments.
6. Delivery may finish as delivered, failed and retried, or returned. Cancellation is allowed only in early states.
7. COD, when required, is collected after delivery and later settled by an authorized admin workflow.
8. Shops receive queued in-app notifications for important shipment and COD events.

### Partner integration journey

1. A shop or integration administrator provisions an environment-bound API key, scopes, optional IP restrictions, and webhook endpoints.
2. The partner requests a quote or creates a shipment using a client-scoped idempotency key.
3. The API returns the stored response for an exact retry and rejects reuse of the same key with a different request.
4. The partner tracks shipments in its shop. An external order ID is disclosed only to the API client that owns that reference.
5. Shipment events are placed in the database outbox and delivered asynchronously as signed webhooks with retry and dead-letter behavior.

See the [Partner API reference](../partner-api.md) for the public contract. That reference and the generated OpenAPI are authoritative for request/response details.

### Public tracking journey

The public tracking page currently requests a summary using only the tracking code. The application service also supports a verified projection when the caller supplies the last four digits of the sender or receiver phone, but that input is not wired into the current page. Internal notes, actor identifiers, GPS coordinates, credentials, and other operational data must not leak into either public timeline.

## Product capabilities

- Shop registration, profile, staff permissions, shipment creation/import/export, labels, notifications, and COD reporting.
- Dispatch by pickup area, hub/working-area eligibility, shipper availability, and capacity.
- Shipment lifecycle and audit history, including failure reasons and optional delivery evidence/GPS.
- Configurable route regions and fee rules with fee snapshots stored on each shipment.
- COD collection, discrepancy recording, settlement, and reporting.
- Public tracking with a privacy-safe projection.
- Partner API credentials/scopes, idempotent creation, request audits, webhook configuration, and signed event delivery.
- Admin management for users, shops, hubs, system configuration, banners, audits, and operational dashboards.

## Scope boundaries

The repository does not establish the following as implemented product guarantees:

- Real carrier network optimization, route sequencing, or live vehicle dispatch.
- Payment processing or bank settlement integration; COD settlement is an internal operational record.
- Multi-currency accounting. Monetary rules currently assume the configured shipment currency and seeded fee policy.
- Exactly-once external delivery. Webhooks are at-least-once and consumers must deduplicate.
- Automatic production deployment or infrastructure provisioning.

## Glossary

| Term | Meaning |
| --- | --- |
| Shipment | The aggregate controlling parcel facts, fee snapshot, status history, and assignment lifecycle |
| Tracking code | Public shipment identifier; unique in the database |
| Shop | Merchant account that owns shipments and integrations |
| Assignment | Historical link between a shipment and shipper; at most one active assignment per shipment |
| Hub | Operational location used with working areas to select eligible shippers |
| Working area | Province/ward/zone coverage assigned to a shipper |
| COD | Cash expected, collected, and settled for a delivered shipment |
| Fee rule | Versioned configuration used to calculate a shipment fee at creation time |
| API client | Credential and scope boundary for a shop's Partner API access |
| External shipment reference | Client-owned mapping of idempotency key/external order ID to a shipment and stored response |
| Outbox | Durable database queue written with business changes before asynchronous processing |
| Webhook delivery | A queued, signed outbound HTTP attempt derived from an outbox event |
