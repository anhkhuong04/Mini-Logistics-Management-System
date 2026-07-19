# Business Rules

## Project Scope

Mini Logistics Management System supports the core workflow:

```text
Shop creates shipment
-> Operator assigns shipper
-> Shipper picks up package
-> Shipper delivers package
-> System tracks status and COD
```

The system is an MVP. Keep business rules simple, explicit, and easy to demo.

---

# Roles

## Admin

- Can manage all users.
- Can view and manage all shipments.
- Can manage fee rules.
- Can view all dashboards.
- Can handle COD settlement.

## Operator

- Can view all shipments.
- Can assign shipper.
- Can update operational shipment status.
- Can handle failed deliveries.

## Shop

- Can create shipments.
- Can view own shipments.
- Can cancel own shipments before pickup.
- Can view own COD information.

## Shipper

- Can view assigned shipments.
- Can update status of assigned shipments only.
- Can confirm COD collection.

## Receiver

- Can track shipment by tracking code.
- Does not need login.

---

# Shipment Rules

## Shipment Creation

- Sender information is required.
- Receiver information is required.
- Pickup address is required.
- Delivery address is required.
- Receiver phone number is required.
- Weight must be greater than 0.
- Goods value must be greater than or equal to 0.
- COD amount must be greater than or equal to 0.
- Tracking code must be unique.
- New shipment starts with `PendingPickup`.

---

## Shipment Assignment

- Only `Admin` or `Operator` can assign shipper.
- Only active shippers can be assigned.
- Only `PendingPickup` shipment can be assigned.
- One shipment can only have one active shipper assignment.
- After assignment, shipment status becomes `Assigned`.

---

## Shipment Status Flow

Valid main flow:

```text
PendingPickup
-> Assigned
-> PickingUp
-> PickedUp
-> InTransit
-> Delivering
-> Delivered
```

Valid failed flow:

```text
Delivering
-> DeliveryFailed
-> Returned
```

Valid cancellation flow:

```text
PendingPickup -> Cancelled
Assigned -> Cancelled
```

---

## Shipment Status Rules

- Shipment status must follow valid transitions.
- `Delivered`, `Cancelled`, and `Returned` are final statuses.
- Final status cannot be changed.
- Shipper can only update shipments assigned to them.
- Shop cannot update delivery status.
- Shop can only cancel own shipment before pickup.
- Delivered shipment cannot be cancelled.
- Picked up shipment cannot be cancelled by Shop.

---

# Tracking Rules

- Every status change must create a tracking history record.
- Tracking history must include:
    - ShipmentId
    - FromStatus
    - ToStatus
    - Note
    - UpdatedByUserId
    - CreatedAt
- Public tracking only shows safe shipment information.
- Public tracking must not expose internal user data.

---

# COD Rules

## COD Status

Valid COD statuses:

```text
NotRequired
PendingCollection
Collected
Settled
```

## COD Business Rules

- If COD amount is 0, COD status is `NotRequired`.
- If COD amount > 0, COD status is `PendingCollection`.
- COD can only be marked as `Collected` when shipment is `Delivered`.
- Only assigned shipper can confirm COD collection.
- Only Admin or Operator can mark COD as `Settled`.
- Returned or Cancelled shipment cannot have COD collected.
- COD amount must not be negative.

---

# Shipping Fee Rules

Shipping fee formula:

```text
ShippingFee = BaseFee + WeightFee + RouteFee
```

Rules:

- Base fee is required.
- Route type must be valid.
- Weight fee applies when weight exceeds configured threshold.
- Shipping fee must be greater than or equal to 0.
- Fee calculation should be simple for MVP.
- Do not implement complex real-world pricing rules unless requested.

---

# Dashboard Rules

## Admin Dashboard

Show:

- Total shipments
- Processing shipments
- Delivered shipments
- Failed shipments
- Total shipping fees
- Total COD collected

## Shop Dashboard

Show:

- Own total shipments
- Own processing shipments
- Own delivered shipments
- Own pending COD

## Shipper Dashboard

Show:

- Assigned shipments
- Delivered shipments
- Failed shipments

---

# Authorization Rules

- Always check current user role before executing restricted use cases.
- Shop can only access own shipments.
- Shipper can only access assigned shipments.
- Receiver can only access public tracking.
- Admin has full system access.
- Operator has operational access but cannot manage system users unless explicitly allowed.

---

# MVP Constraints

Do not implement:

- Real-time GPS tracking
- Payment gateway
- Route optimization
- Microservices
- Mobile app
- Complex warehouse management
- Complex financial reconciliation

Prefer simple and demo-friendly workflows.