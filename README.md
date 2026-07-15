# Mini Logistics Management System

> A mini delivery management MVP simulating the core operational flows of a logistics platform (similar to GHN, GHTK, J&T) — built with **.NET 10 + Blazor Web App** following **Clean Architecture** and **Modular Monolith** principles.

---

## Table of Contents

- [Overview](#overview)
- [Tech Stack](#tech-stack)
- [Architecture](#architecture)
- [Features](#features)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Demo Accounts](#demo-accounts)
- [Pages & Routes](#pages--routes)
- [Partner API](#partner-api)
- [Running Tests](#running-tests)
- [Project Structure](#project-structure)
- [Business Rules](#business-rules)
- [Shipping Fee Calculation](#shipping-fee-calculation)

---

## Overview

Mini Logistics Management System is an end-to-end delivery management system built as an MVP, designed to:

- Demonstrate **Fullstack .NET** proficiency with Blazor Web App.
- Apply **Clean Architecture** and **Modular Monolith** patterns in practice.
- Simulate real-world logistics operations: order creation, dispatch, delivery, COD collection, and shipment tracking.

### High-Level Flow

```
Shop creates order -> Operator assigns Shipper -> Shipper delivers -> COD collected -> Public tracking
```

---

## Tech Stack

| Component      | Technology                            |
| -------------- | ------------------------------------- |
| Runtime        | .NET 10                               |
| Frontend       | Blazor Web App (Interactive Server)   |
| Architecture   | Clean Architecture + Modular Monolith |
| Database       | SQL Server / SQL Server LocalDB       |
| ORM            | Entity Framework Core 10              |
| Authentication | ASP.NET Core Identity                 |
| Validation     | FluentValidation 12                   |
| Testing        | xUnit                                 |

---

## Architecture

```
src/
├── MiniLogistics.Domain/          # Entities, Value Objects, Domain Rules
├── MiniLogistics.Application/     # Use Cases, Application Services, Validators
├── MiniLogistics.Infrastructure/  # EF Core, Identity, Repositories, Seeder
└── MiniLogistics.Web/             # Blazor UI, Partner REST API Endpoints

test/
├── MiniLogistics.Application.Tests/
├── MiniLogistics.Infrastructure.Tests/
└── MiniLogistics.Web.Tests/
```

**Dependency flow:**

```
Web -> Application -> Domain
          ^
   Infrastructure
```

> `Domain` has zero external dependencies. `Infrastructure` implements interfaces defined in `Application`.

---

## Features

### Shop

- Register and log in with a Shop account.
- Create shipments with real-time fee preview on the form.
- View shipment list and shipment detail pages.
- Cancel a shipment while it is still in a cancellable state.
- Look up tracking status by tracking code.

### Operator / Admin

- View `PendingPickup` shipments that could not be auto-assigned, with fallback reasons.
- Retry auto-assignment or manually override the shipper when needed.
- Assign / reassign with a warning when the shipper does not match the pickup area.
- Monitor active shipments: `Assigned`, `PickingUp`, `PickedUp`, `InTransit`, `Delivering`, `DeliveryFailed`.
- Support manual shipment status updates following lifecycle rules.
- Confirm COD collection for `Delivered` shipments with `PendingCollection` COD status.

### Shipper

- View assigned working area, auto-assignment availability, and current active load.
- Update shipment status following valid lifecycle transitions.
- A delivery failure note is **required** when transitioning to `DeliveryFailed`.
- Confirm COD collection directly from the shipper workspace.
- Completed shipments automatically disappear from the active workspace.

### Receiver / Public

- Track shipment summary by tracking code. Full public detail requires the tracking code plus the last 4 digits of the sender or receiver phone number.

### Partner API

- REST API for third-party e-commerce platform integration.
- Endpoints: fee quote, create shipment, tracking, cancel shipment.
- Webhooks: `shipment.created`, `shipment.status_changed`.
- Features: rate limiting, idempotency key support, HMAC request signature.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server or SQL Server LocalDB (bundled with Visual Studio / VS Build Tools)

---

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/<your-username>/Mini-logistics-management-system.git
cd Mini-logistics-management-system
```

### 2. Configure the connection string

The project defaults to **SQL Server LocalDB**. Update the connection string in:

```
src/MiniLogistics.Web/appsettings.Development.json
```

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=MiniLogisticsDb;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

Replace with a full SQL Server connection string if needed.

### 3. Run database migrations and seed demo data

```powershell
dotnet run --project src/MiniLogistics.Web -- --migrate --seed
```

> This creates the database schema and seeds all demo accounts and sample data.

To re-seed data on an existing schema:

```powershell
dotnet run --project src/MiniLogistics.Web -- --seed
```

### 4. Start the application

```powershell
dotnet run --project src/MiniLogistics.Web
```

The app runs at: **https://localhost:5221**

---

## Demo Accounts

| Role     | Email                        | Password        |
| -------- | ---------------------------- | --------------- |
| Shop     | shop@minilogistics.local     | Shop@123456     |
| Admin    | admin@minilogistics.local    | Admin@123456    |
| Operator | operator@minilogistics.local | Operator@123456 |
| Shipper  | shipper@minilogistics.local  | Shipper@123456  |

---

## Pages & Routes

| Route                     | Role           | Description                                                              |
| ------------------------- | -------------- | ------------------------------------------------------------------------ |
| `/`                       | Public         | Landing page                                                             |
| `/register`               | Public         | Shop account registration                                                |
| `/login`                  | Public         | Login                                                                    |
| `/tracking`               | Public         | Public shipment summary by tracking code; verified detail by phone last 4 |
| `/dashboard`              | Shop           | Overview dashboard                                                       |
| `/shipments`              | Shop           | Shipment list                                                            |
| `/shipments/create`       | Shop           | Create new shipment with real-time fee preview                           |
| `/shipments/{id}`         | Shop           | Shipment detail, tracking timeline, cancel action                        |
| `/operations/assignments` | Admin/Operator | Dispatch hub: assign shippers, monitor operations, update status, COD support |
| `/shipper/shipments`      | Shipper        | Shipper workspace: active shipments, status updates, COD confirmation    |
| `/partner/integrations`   | Admin          | Configure partner webhooks                                               |
| `/admin/users`            | Admin          | User management                                                          |

---

## Partner API

**Base URL:** `http://localhost:5221/api/v1/partner`

**Authentication:** `Authorization: Bearer {api_key}`

| Method | Endpoint                           | Description          | Rate Limit |
| ------ | ---------------------------------- | -------------------- | ---------- |
| POST   | `/shipping/quote`                  | Get a shipping quote | 60/min     |
| POST   | `/shipments`                       | Create a shipment    | 30/min     |
| GET    | `/shipments/{trackingCode}`        | Track a shipment     | 120/min    |
| POST   | `/shipments/{trackingCode}/cancel` | Cancel a shipment    | 30/min     |

For full details, see [`docs/partner-api.md`](docs/partner-api.md) and [`docs/third-party-shipment-integration-guide.md`](docs/third-party-shipment-integration-guide.md).

---

## Running Tests

Run the full test suite:

```powershell
dotnet test Mini-logistics-manegemant-system.slnx
```

Run infrastructure integration tests only (requires SQL Server LocalDB):

```powershell
dotnet test test/MiniLogistics.Infrastructure.Tests/MiniLogistics.Infrastructure.Tests.csproj
```

Current results: **14 passed / 0 failed**.

---

## Project Structure

```
Mini-logistics-management-system/
├── src/
│   ├── MiniLogistics.Domain/
│   │   ├── Shipments/        # Shipment aggregate, ShipmentStatus, TrackingCode
│   │   ├── CashOnDelivery/   # COD aggregate
│   │   ├── Operations/       # ShipmentAssignment
│   │   ├── Fees/             # FeeRule, ShippingFeeCalculator, InsuranceFeePolicy
│   │   ├── Shops/            # Shop entity
│   │   ├── Users/            # User entity
│   │   └── ValueObjects/     # Address, Weight, Money, ParcelDimensions
│   │
│   ├── MiniLogistics.Application/
│   │   ├── Shipments/        # CreateShipment, CancelShipment, UpdateShipmentStatus
│   │   ├── CashOnDelivery/   # MarkCodCollected
│   │   ├── Fees/             # ShippingFeeService
│   │   ├── Routing/          # RouteClassificationService
│   │   ├── Shippers/         # AutoAssignment service
│   │   ├── Identity/         # Login, Register
│   │   └── AdminUsers/       # User management
│   │
│   ├── MiniLogistics.Infrastructure/
│   │   ├── Persistence/      # DbContext, Migrations, Seeder, Repositories
│   │   └── Identity/         # ASP.NET Core Identity configuration
│   │
│   └── MiniLogistics.Web/
│       ├── Components/
│       │   ├── Pages/        # Blazor pages (CreateShipment, OperationsAssignments, ...)
│       │   └── Layout/       # NavMenu, MainLayout
│       ├── Endpoints/        # Partner REST API endpoints
│       └── Services/         # VietnamAdministrativeDivisionService, RateLimiter
│
├── test/
│   ├── MiniLogistics.Application.Tests/
│   ├── MiniLogistics.Infrastructure.Tests/
│   └── MiniLogistics.Web.Tests/
│
├── docs/
│   ├── partner-api.md
│   ├── partner-api.openapi.json
│   ├── third-party-shipment-integration-guide.md
│   └── roles/                # Role-specific flow documentation
│
├── postman/                  # Postman collection
├── DEMO-CHECKLIST.md         # Manual demo walkthrough
├── overview.md               # Detailed project overview and progress
└── fee.md                    # Shipping fee analysis and SPX comparison
```

---

## Business Rules

### Shipment Status Lifecycle

```
PendingPickup -> Assigned -> PickingUp -> PickedUp -> InTransit -> Delivering -> Delivered
                                                                              -> DeliveryFailed -> (retry or Returned)
```

- Invalid lifecycle transitions are rejected at the domain layer.
- `DeliveryFailed` requires a note.
- Terminal shipments (`Delivered`, `Returned`, `Cancelled`) cannot be updated further.

### Assignment

- On shipment creation, the system attempts **auto-assignment** based on pickup area:
  `PickupAddress.Province -> hub -> shipper working area`.
- Auto-assignment only selects shippers that are active, accepting auto-assignments, area-matched, and under `MaxActiveShipments`.
- If no eligible shipper is found, the shipment stays `PendingPickup` for Operations to handle manually.
- Admins and Operators can **manually override** with a warning when the shipper does not match the pickup area.

### Cash on Delivery (COD)

- A COD transaction is created together with the shipment.
- Only `Delivered` shipments can have their COD marked as collected.
- After COD is collected, the active assignment is deactivated.

### Assignment Lifecycle After Completion

| Shipment State                        | Assignment Action                          |
| ------------------------------------- | ------------------------------------------ |
| `Delivered` + COD `PendingCollection` | Kept active so the shipper can confirm COD |
| `Delivered` + COD `NotRequired`       | Deactivated immediately                    |
| COD `Collected`                       | Deactivated                                |
| `Returned`                            | Deactivated immediately                    |
| `Cancelled`                           | Deactivated immediately                    |

---

## Shipping Fee Calculation

### Formula

```
TotalFee = BaseFee + ExtraWeightFee + InsuranceFee + ReturnFee
```

| Component         | Description                                                                    |
| ----------------- | ------------------------------------------------------------------------------ |
| Chargeable weight | `max(actual_weight, volumetric_weight)` where `volumetric = L x W x H / 5000` |
| BaseFee           | Determined by route type and base weight from the active `FeeRule`             |
| ExtraWeightFee    | `ceil((chargeable - baseWeight) / step) x stepFee`                             |
| InsuranceFee      | 0.5% of declared value if >= 1,000,000 VND (capped at 20,000,000 VND)         |
| ReturnFee         | 50% x (BaseFee + ExtraWeightFee), applied when shipment transitions to `Returned` |

### Default Fee Table (seeded)

| Route         | Base weight | Base fee   | Step   | Extra per step |
| ------------- | ----------: | ---------: | -----: | -------------: |
| IntraProvince |      2.0 kg | 20,000 VND | 0.5 kg |      3,000 VND |
| IntraRegion   |      0.5 kg | 28,000 VND | 0.5 kg |      4,000 VND |
| InterRegion   |      0.5 kg | 35,000 VND | 0.5 kg |      8,000 VND |

> Fees are calculated **twice**: once on the UI for real-time preview, and again server-side in the application service before persisting the shipment — preventing any client-side data tampering.

---

## Additional Documentation

| File | Description |
| ---- | ----------- |
| [DEMO-CHECKLIST.md](DEMO-CHECKLIST.md) | Step-by-step manual demo walkthrough |
| [overview.md](overview.md) | Project overview, progress, and upcoming tasks |
| [fee.md](fee.md) | In-depth fee calculation analysis and comparison with SPX Express |
| [docs/partner-api.md](docs/partner-api.md) | Partner REST API reference |
| [docs/third-party-shipment-integration-guide.md](docs/third-party-shipment-integration-guide.md) | Integration guide for third-party partners |

---

## License

This project is for educational and portfolio purposes.
