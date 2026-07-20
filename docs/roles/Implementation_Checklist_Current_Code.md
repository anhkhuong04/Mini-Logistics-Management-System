# Roles Implementation Checklist - Current Code Snapshot

Updated: 2026-07-20

This checklist is the implementation-ready source for role docs. Codebase remains the source of truth; older narrative sections in role analysis files should be interpreted through this checklist before refactor work.

## Status Legend

- Implemented: service/UI/domain behavior exists and is covered by current flow.
- Implemented but needs hardening: usable now, but needs production optimization, security, audit, or UX hardening.
- Partially implemented: core path exists, but expected production flow is incomplete.
- Missing: no meaningful implementation yet.
- Deferred: explicitly after query/permission/audit/shipper-core stabilization.

## Shop

| Feature | Status | Modules | Implementation checklist |
|---|---|---|---|
| Profile management | Implemented but needs hardening | `Shops/UpdateShopProfile`, `ShopProfile.razor` | Audit added; keep active-shop rule; add address normalization later. |
| Multi-shop selection/create | Implemented | `ShopAccess`, `CreateAdditionalShop`, `ShopProfile.razor` | Audit added for additional shop creation. |
| Create shipment | Implemented but needs hardening | `CreateShipmentService`, `CreateShipment.razor` | Audit added; query/report/label remain separate work. |
| Draft/edit/submit before pickup | Implemented but needs hardening | `DraftShipments/*`, `ShipmentDetail.razor` | Audit added for draft create/update/submit. |
| CSV import | Partially implemented | `ImportShipments/*` | Preview/confirm exists; audit summary added; background import remains missing. |
| Advanced filters/export/report/KPI/label | Implemented but needs hardening | `GetShipmentsForCurrentShop`, `ExportShopShipments`, `GenerateShipmentLabel`, `Shops/Reports`, `ShopShipmentFileEndpoints` | CSV export and label PDF endpoints exist; COD report/KPI services exist; dashboard UI still uses existing list model and should be switched fully to KPI service. |

## Shipper

| Feature | Status | Modules | Implementation checklist |
|---|---|---|---|
| Active assignment workspace | Implemented but needs hardening | `GetAssignedShipmentsForShipper`, `ShipperShipments.razor` | Server-side visibility/search/tabs/COD filter/paging exist. |
| Status lifecycle update | Implemented but needs hardening | `UpdateShipmentStatusService`, `ShipmentStatusHistory` | Failure reason code and optional GPS metadata are supported; UI sends standardized failure reason. |
| COD collection | Implemented but needs hardening | `MarkCodCollectedService`, `CodTransaction` | Actual collected amount, discrepancy amount and discrepancy note rule exist; Shipper UI captures actual amount/note. |
| Working area/capacity visibility | Implemented | identity + assignment selector | Shipper can toggle own availability; admin capacity override remains intact. |
| COD daily summary | Implemented | `GetShipperCodDailySummary`, `ShipperShipments.razor` | Summary is server-side and scoped to shipper assignment history. |
| POD/GPS/PWA | Partially implemented | `DeliveryProof`, `SubmitDeliveryProofService`, `ShipmentStatusHistory` | POD metadata and GPS persistence exist; full mobile geolocation/camera/PWA UI remains hardening work. |

## Operator

| Feature | Status | Modules | Implementation checklist |
|---|---|---|---|
| Operations board filters/SLA/paging UI | Implemented but needs hardening | `OperationsAssignments.razor`, `GetOperationsShipmentsService` | Server-side query and COD batch lookup added; list still maps page history for compatibility. |
| Pending pickup queue | Implemented but needs hardening | `GetPendingPickupShipmentsService` | Server-side filter/page added. |
| Assign/reassign/cancel assignment | Implemented but needs hardening | assignment command services | Permission service added; audit taxonomy retained/expanded. |
| Bulk retry auto assignment | Implemented but needs hardening | `BulkRetryAutoAssignmentService` | Permission service and summary audit added. |
| COD collect/settle | Implemented with split permissions | COD services | Operator may collect; only Admin has `operations.cod.settle`. |
| Permission-based authorization | Partially implemented | `Authorization/*`, `Program.cs` policy | Central catalog/mapping exists; route policy added; future claim-driven policies can replace role mapping. |

## Cross-Role Refactor Dependencies

1. Query/index baseline must pass build/test before adding report/dashboard features.
2. Operator permissions must remain behavior-compatible with current Admin/Operator roles.
3. Audit keeps `AdminAuditLogs` storage for now; neutral taxonomy is implemented through constants and service usage.
4. Shipper production core is now partially implemented: failure reason, optional GPS persistence, POD metadata, COD actual amount, daily summary and self availability.
5. Shop label/export/report/KPI backend is implemented; next hardening is switching dashboard fully to aggregate service and adding richer report UI.
