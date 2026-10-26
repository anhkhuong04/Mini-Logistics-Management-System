# Frontend migration — inventory route/action và binding contract

Đối chiếu baseline ngày 26/10/2026. Inventory bao gồm 28 routed page files/30 route declarations, 93 binding gọi service và các browser form/file/local action. Phase 1–9 đã triển khai auth/reference/Shop context, dashboard, shipment read/quote/mutation, CSV import và file downloads; Administration/banner đã triển khai; nghiệp vụ Phase 10+ vẫn là **đặc tả adapter tương lai**. Trạng thái/evidence theo [migration contracts](react-migration-contracts.md) và [validation log](react-migration-validation.md), không suy ra endpoint đã có chỉ từ bảng. Shop adapters có prefix `/api/v1/app`; A049–A066, A074/A076 đã có, file routes giữ `/shop/files`, renderer tương ứng chạy dưới `/app`, URL Blazor cũ được giữ.

## Quy ước đọc

- Endpoint không có `P` dùng prefix `/api/v1/app`; `P` là `/api/v1/public`. GUID path là target resource; actor lấy claims. Endpoint mới không dùng API key partner.
- Shop scope `{shopId}` phải được use case xác minh; permission tên ngắn Operations tham chiếu OperationPermissions; scope integration không chỉ là role.
- Mỗi action dùng request/response binding ở bảng service contracts cuối file: bỏ actor fields và path IDs khỏi body, camelCase, decimal string theo migration contract; Result được unwrap. Void/Result success trả 204, DTO đọc/update 200; create resource 201; import confirm/bulk/test queue trả 200 DTO, không tự retry.
- Filter/search/paging/refresh của cùng use case cùng một read endpoint. Modal/selection/preset/confirm/focus là interaction local; inventory handler được giữ để không quên parity, không tạo HTTP endpoint giả cho mỗi click.
- Tên test trong bảng là **test dự kiến cần tạo** dưới Web.Tests/Browser hoặc frontend E2E, không phải bằng chứng đã có coverage.
- Tests backend hiện có được cross-reference ở migration contract/status và validation log. Sources link là file hiện có, không phải scaffold path.

## AdminAuditLogs

**Source:** [AdminAuditLogs.razor](../../src/MiniLogistics.Web/Components/Pages/Administration/AdminAuditLogs.razor). **Routes:** `/admin/audit-logs`.

**React page:** `administration/AdminAuditPage.tsx` (đã triển khai dưới `features/`).

**Query bind hiện tại:** không có SupplyParameterFromQuery.

| ID | Action/use case hiện tại | Endpoint hiện có (prefix `/api/v1/app`) | Quyền/ràng buộc | Task / regression |
| --- | --- | --- | --- | --- |
| A001 | `GetAdminAuditLogsService.GetAsync` | `GET /admin/audit` | Admin | T09.01 / `BrowserAdminTests` |

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `GetCurrentUserIdAsync`, `LoadAsync`, `OnFromDateChanged`, `OnInitializedAsync`, `OnToDateChanged`.

## AdminBanners

**Source:** [AdminBanners.razor](../../src/MiniLogistics.Web/Components/Pages/Administration/AdminBanners.razor). **Routes:** `/admin/banners`.

**React page:** `administration/AdminBannersPage.tsx` (đã triển khai dưới `features/`).

**Query bind hiện tại:** không có SupplyParameterFromQuery.

| ID | Action/use case hiện tại | Endpoint hiện có (prefix `/api/v1/app`) | Quyền/ràng buộc | Task / regression |
| --- | --- | --- | --- | --- |
| A002 | `BannerService.CreateBannerAsync` | `POST /admin/banners` | Admin; actor guard tại Application | T09.04 / `BrowserBannerTests` |
| A003 | `BannerService.DeleteBannerAsync` | `DELETE /admin/banners/{bannerId}` | Admin; actor guard tại Application | T09.04 / `BrowserBannerTests` |
| A004 | `BannerService.GetAllBannersAsync` | `GET /admin/banners` | Admin; actor guard tại Application | T09.04 / `BrowserBannerTests` |
| A005 | `BannerService.ToggleBannerStatusAsync` | `POST /admin/banners/{bannerId}/toggle` | Admin; preserve toggle semantics, no automatic retry | T09.04 / `BrowserBannerTests` |
| A006 | `BannerService.UpdateBannerAsync` | `PUT /admin/banners/{bannerId}` | Admin; actor guard tại Application | T09.04 / `BrowserBannerTests` |

**Upload bổ sung:** `POST /admin/banners/images` multipart file; Admin use-case guard + Infrastructure storage, max 5,242,880 bytes; T09.04 / BrowserBannerTests. Không lấy ghi file trong Razor làm lý do để đưa I/O vào endpoint.

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `CancelBannerAction`, `ClearForm`, `ConfirmBannerActionAsync`, `DeleteBannerAsync`, `EditBanner`, `HandleFileSelected`, `LoadAsync`, `OnInitializedAsync`, `RemoveImage`, `RequestDeleteBanner`, `RequestToggleStatus`, `SaveBannerAsync`, `ToggleStatusAsync`.

## AdminCod

**Source:** [AdminCod.razor](../../src/MiniLogistics.Web/Components/Pages/Administration/AdminCod.razor). **Routes:** `/admin/cod`.

**React page:** `administration/AdminCodPage.tsx` (đã triển khai dưới `features/`).

**Query bind hiện tại:** không có SupplyParameterFromQuery.

| ID | Action/use case hiện tại | Endpoint hiện có (prefix `/api/v1/app`) | Quyền/ràng buộc | Task / regression |
| --- | --- | --- | --- | --- |
| A007 | `GetAdminCodReportService.GetAsync` | `GET /admin/cod-report` | Admin | T09.01 / `BrowserAdminTests` |
| A008 | `MarkCodSettledService.MarkSettledAsync` | `POST /operations/shipments/{shipmentId}/cod/settle` | Admin/CodSettle; collected | T06.03 / `BrowserOperationsActionTests` |

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `GetCurrentUserIdAsync`, `LoadAsync`, `OnFromDateChanged`, `OnInitializedAsync`, `OnToDateChanged`, `SelectTabAsync`, `SetSettlementNote`, `SettleAsync`.

## AdminDashboard

**Source:** [AdminDashboard.razor](../../src/MiniLogistics.Web/Components/Pages/Administration/AdminDashboard.razor). **Routes:** `/admin`, `/admin/dashboard`.

**React page:** `administration/AdminDashboardPage.tsx` (đã triển khai dưới `features/`).

**Query bind hiện tại:** không có SupplyParameterFromQuery.

| ID | Action/use case hiện tại | Endpoint hiện có (prefix `/api/v1/app`) | Quyền/ràng buộc | Task / regression |
| --- | --- | --- | --- | --- |
| A009 | `GetAdminDashboardService.GetAsync` | `GET /admin/dashboard` | Admin | T09.01 / `BrowserAdminTests` |

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `ApplyPresetAsync`, `GetCurrentUserIdAsync`, `LoadAsync`, `OnFromDateChanged`, `OnInitializedAsync`, `OnToDateChanged`.

## AdminHubs

**Source:** [AdminHubs.razor](../../src/MiniLogistics.Web/Components/Pages/Administration/AdminHubs.razor). **Routes:** `/admin/hubs`.

**React page:** `administration/AdminHubsPage.tsx` (đã triển khai dưới `features/`).

**Query bind hiện tại:** không có SupplyParameterFromQuery.

| ID | Action/use case hiện tại | Endpoint hiện có (prefix `/api/v1/app`) | Quyền/ràng buộc | Task / regression |
| --- | --- | --- | --- | --- |
| A010 | `CreateHubService.CreateAsync` | `POST /admin/hubs` | Admin | T09.03 / `BrowserAdminTests` |
| A011 | `GetAdminHubsService.GetAsync` | `GET /admin/hubs` | Admin | T09.03 / `BrowserAdminTests` |
| A012 | `SetHubActiveStatusService.SetAsync` | `PUT /admin/hubs/{hubId}/active` | Admin | T09.03 / `BrowserAdminTests` |
| A013 | `UpdateHubService.UpdateAsync` | `PUT /admin/hubs/{hubId}` | Admin | T09.03 / `BrowserAdminTests` |

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `CancelToggleHub`, `ClearForm`, `ConfirmToggleHubAsync`, `EditHub`, `GetCurrentUserIdAsync`, `LoadAsync`, `OnInitializedAsync`, `RefreshAsync`, `RequestToggleHub`, `SaveHubAsync`, `SetHubReason`, `ToggleHubAsync`.

## AdminShops

**Source:** [AdminShops.razor](../../src/MiniLogistics.Web/Components/Pages/Administration/AdminShops.razor). **Routes:** `/admin/shops`.

**React page:** `administration/AdminShopsPage.tsx` (đã triển khai dưới `features/`).

**Query bind hiện tại:** không có SupplyParameterFromQuery.

| ID | Action/use case hiện tại | Endpoint hiện có (prefix `/api/v1/app`) | Quyền/ràng buộc | Task / regression |
| --- | --- | --- | --- | --- |
| A014 | `GetAdminShopsService.GetAsync` | `GET /admin/shops` | Admin | T09.02 / `BrowserAdminTests` |
| A015 | `SetShopActiveStatusService.SetAsync` | `PUT /admin/shops/{shopId}/active` | Admin | T09.02 / `BrowserAdminTests` |

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `CancelToggleShop`, `ConfirmToggleShopAsync`, `GetCurrentUserIdAsync`, `LoadAsync`, `OnInitializedAsync`, `RefreshAsync`, `RequestToggleShop`, `ToggleShopAsync`.

## AdminSystemConfig

**Source:** [AdminSystemConfig.razor](../../src/MiniLogistics.Web/Components/Pages/Administration/AdminSystemConfig.razor). **Routes:** `/admin/system-config`.

**React page:** `administration/AdminSystemConfigPage.tsx` (đã triển khai dưới `features/`).

**Query bind hiện tại:** không có SupplyParameterFromQuery.

| ID | Action/use case hiện tại | Endpoint hiện có (prefix `/api/v1/app`) | Quyền/ràng buộc | Task / regression |
| --- | --- | --- | --- | --- |
| A016 | `AdminSystemConfigurationService.CreateFeeRuleVersionAsync` | `POST /admin/configuration/fees` | Admin; serialized version | T09.03 / `BrowserAdminTests` |
| A017 | `AdminSystemConfigurationService.GetAsync` | `GET /admin/configuration` | Admin | T09.03 / `BrowserAdminTests` |
| A018 | `AdminSystemConfigurationService.UpsertRouteRegionAsync` | `PUT /admin/configuration/routes` | Admin; serialized version | T09.03 / `BrowserAdminTests` |

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `GetCurrentUserIdAsync`, `LoadAsync`, `OnInitializedAsync`, `SaveFeeRuleAsync`, `SaveRouteRegionAsync`.

## AdminUsers

**Source:** [AdminUsers.razor](../../src/MiniLogistics.Web/Components/Pages/Administration/AdminUsers.razor). **Routes:** `/admin/users`.

**React page:** `administration/AdminUsersPage.tsx` (đã triển khai dưới `features/`).

**Query bind hiện tại:** không có SupplyParameterFromQuery.

| ID | Action/use case hiện tại | Endpoint hiện có (prefix `/api/v1/app`) | Quyền/ràng buộc | Task / regression |
| --- | --- | --- | --- | --- |
| A019 | `CreateInternalUserService.CreateAsync` | `POST /admin/users` | Admin | T09.02 / `BrowserAdminTests` |
| A020 | `GetAdminUsersService.SearchAsync` | `GET /admin/users` | Admin | T09.02 / `BrowserAdminTests` |
| A021 | `GetAdminHubsService.GetAsync` | `GET /admin/hubs` | Admin; thay lookup không actor trước HTTP | T09.02 / `BrowserAdminTests` |
| A022 | `ResetUserPasswordService.ResetAsync` | `PUT /admin/users/{userId}/password` | Admin; userId is target, not actor | T09.02 / `BrowserAdminTests` |
| A023 | `SetShipperCapacityService.SetAsync` | `PUT /admin/users/{userId}/capacity` | Admin; target shipper | T09.02 / `BrowserAdminTests` |
| A024 | `SetShipperWorkingAreasService.SetAsync` | `PUT /admin/users/{userId}/working-areas` | Admin; target shipper | T09.02 / `BrowserAdminTests` |
| A025 | `SetUserActiveStatusService.SetAsync` | `PUT /admin/users/{userId}/active` | Admin; userId is target, not actor | T09.02 / `BrowserAdminTests` |

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `ApplyUserFiltersAsync`, `CancelPendingUserAction`, `ClearCreateForm`, `ConfirmPendingUserActionAsync`, `CreateUserAsync`, `GetCurrentUserIdAsync`, `LoadAsync`, `NextUserPageAsync`, `OnInitializedAsync`, `PreviousUserPageAsync`, `RefreshAsync`, `RequestPasswordReset`, `RequestToggleUser`, `ResetUserPasswordAsync`, `SaveCapacityAsync`, `SaveWorkingAreasAsync`, `SelectAreaShipper`, `SetResetPassword`, `SetToggleReason`, `SyncSelectedAreaShipper`, `SyncSelectedCapacity`, `SyncSelectedWorkingAreaHubIds`, `ToggleHubSelection`, `ToggleUserAsync`.

## PartnerIntegrations

**Source:** [PartnerIntegrations.razor](../../src/MiniLogistics.Web/Components/Pages/Integrations/PartnerIntegrations.razor). **Routes:** `/partner/integrations`.

**React page:** `integrations/IntegrationClientsPage + WebhookSettingsPanel.tsx` (dự kiến, dưới `features/`).

**Query bind hiện tại:** `shopId`.

| ID | Action/use case hiện tại | Endpoint đích | Quyền/ràng buộc | Task / regression dự kiến |
| --- | --- | --- | --- | --- |
| A026 | `ApiClientManagementService.CreateApiClientAsync` | `POST /integrations/clients` | IntegrationScopeService + Shop ManageIntegrations | T10.01 / `BrowserIntegrationClientTests` |
| A027 | `ApiClientManagementService.RotateApiClientKeyAsync` | `POST /integrations/clients/{apiClientId}/rotate-key` | Integration management scope | T10.01 / `BrowserIntegrationClientTests` |
| A028 | `ApiClientManagementService.SetApiClientActiveStatusAsync` | `PUT /integrations/clients/{apiClientId}/active` | Integration management scope | T10.01 / `BrowserIntegrationClientTests` |
| A029 | `ApiClientManagementService.UpdateApiClientSecurityAsync` | `PUT /integrations/clients/{apiClientId}/security` | Integration management scope | T10.01 / `BrowserIntegrationClientTests` |
| A030 | `WebhookManagementService.GetDashboardAsync` | `GET /integrations/dashboard` | Integration management scope; projection | T10.01 / `BrowserIntegrationClientTests` |
| A031 | `WebhookManagementService.RetryOutboxMessageAsync` | `POST /integrations/outbox/{outboxMessageId}/retry` | Integration management scope | T10.02 / `BrowserIntegrationWebhookTests` |
| A032 | `WebhookManagementService.RetryWebhookDeliveryAsync` | `POST /integrations/deliveries/{deliveryId}/retry` | Integration management scope | T10.02 / `BrowserIntegrationWebhookTests` |
| A033 | `WebhookManagementService.TestWebhookAsync` | `POST /integrations/clients/{apiClientId}/webhook/test` | Integration management scope | T10.02 / `BrowserIntegrationWebhookTests` |
| A034 | `WebhookManagementService.UpsertWebhookEndpointAsync` | `PUT /integrations/clients/{apiClientId}/webhook` | Integration management scope | T10.02 / `BrowserIntegrationWebhookTests` |

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `CopyGeneratedKeyAsync`, `CreateApiClientAsync`, `GetCurrentUserIdAsync`, `LoadAsync`, `OnParametersSetAsync`, `OnShopChangedAsync`, `RefreshAsync`, `RetryDeliveryAsync`, `RetryOutboxAsync`, `RotateClientAsync`, `SaveSecurityAsync`, `SaveWebhookAsync`, `SelectClient`, `SyncWebhookForm`, `TestWebhookAsync`, `ToggleClientAsync`, `ToggleCreateScope`, `ToggleSecurityScope`.

## OperationsAssignments

**Source:** [OperationsAssignments.razor](../../src/MiniLogistics.Web/Components/Pages/Operations/OperationsAssignments.razor). **Routes:** `/operations/assignments`, `/operations/assignment`.

**React page:** `operations/OperationsPage.tsx` (đã triển khai T06.04, dưới `features/`), cùng hai alias dưới `/app`; xem validation log cho evidence.

**Query bind hiện tại:** không có SupplyParameterFromQuery.

| ID | Action/use case hiện tại | Endpoint đích | Quyền/ràng buộc | Task / regression dự kiến |
| --- | --- | --- | --- | --- |
| A035 | `AssignShipperToShipmentService.AssignAsync` | `POST /operations/shipments/{shipmentId}/assign` | AssignmentAssign | T06.02 / `BrowserOperationsAssignmentTests` |
| A036 | `AutoAssignShipmentService.AutoAssignAsync` | `POST /operations/shipments/{shipmentId}/auto-assign` | AssignmentRetryAuto; actor-aware entry required | T06.02 / `BrowserOperationsAssignmentTests` |
| A037 | `BulkRetryAutoAssignmentService.RetryAsync` | `POST /operations/assignments/bulk-retry` | AssignmentBulkRetryAuto | T06.02 / `BrowserOperationsAssignmentTests` |
| A038 | `CancelShipmentAssignmentService.CancelAsync` | `POST /operations/shipments/{shipmentId}/cancel-assignment` | AssignmentCancel | T06.02 / `BrowserOperationsAssignmentTests` |
| A039 | `OperationsReadService.ShippersAsync` → `GetActiveShippersService.GetAsync` | `GET /operations/shippers` | Admin/Operator: ShipmentView; actor guard + aggregate load, no email | T06.01 / `BrowserOperationsReadTests` |
| A040 | `OperationsReadService.SettlementAsync` → focused SQL projection | `GET /operations/cod/settlement-candidates` | Admin: CodSettle, tương đương current UI; thay đề xuất CodViewPending ở Phase 0 | T06.01 / `BrowserOperationsReadTests`, `OperationsSettlementReadRepositoryTests` |
| A041 | `OperationsReadService.ShipmentsAsync` → `GetOperationsShipmentsService.SearchAsync` | `GET /operations/shipments` | Admin/Operator: ShipmentView; AuditView để đọc actor identity/GPS | T06.01 / `BrowserOperationsReadTests` |
| A042 | `OperationsReadService.PendingAsync` → `GetPendingPickupShipmentsService.SearchAsync` | `GET /operations/pending-pickups` | Admin/Operator: ShipmentView; actor guard | T06.01 / `BrowserOperationsReadTests` |
| A043 | `MarkCodCollectedService.MarkCollectedAsync` | `POST /operations/shipments/{shipmentId}/cod/collect` | CodCollect; delivered/pending | T06.03 / `BrowserOperationsActionTests` |
| A044 | `MarkCodSettledService.MarkSettledAsync` | `POST /operations/shipments/{shipmentId}/cod/settle` | Admin/CodSettle; collected | T06.03 / `BrowserOperationsActionTests` |
| A045 | `ReassignShipmentService.ReassignAsync` | `POST /operations/shipments/{shipmentId}/reassign` | AssignmentReassign | T06.02 / `BrowserOperationsAssignmentTests` |
| A046 | `UpdateShipmentStatusService.UpdateAsync` | `POST /operations/shipments/{shipmentId}/status` | ShipmentStatusUpdate | T06.03 / `BrowserOperationsActionTests` |

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `ApplyFiltersAsync`, `AssignAsync`, `BulkRetryAutoAssignAsync`, `CancelAssignmentAsync`, `ClearPendingSelection`, `GetCurrentUserIdAsync`, `IsCurrentUserInRoleAsync`, `LoadAsync`, `MarkCodCollectedAsync`, `MarkCodSettledAsync`, `NextOperationsPageAsync`, `NextPendingPageAsync`, `OnInitializedAsync`, `PreviousOperationsPageAsync`, `PreviousPendingPageAsync`, `ReassignAsync`, `RefreshAsync`, `ResetFiltersAsync`, `RetryAutoAssignAsync`, `SelectReassignShipper`, `SelectShipper`, `SelectStatus`, `SelectVisiblePendingShipments`, `SetCancelAssignmentReason`, `SetReassignReason`, `SetStatusNote`, `SyncOperationState`, `SyncPendingAssignmentInsights`, `TogglePendingSelection`, `TrimSelectionsToCurrentShipments`, `UpdateStatusAsync`.

## Contact

**Source:** [Contact.razor](../../src/MiniLogistics.Web/Components/Pages/Public/Contact.razor). **Routes:** `/contact`.

**React page:** `public/ContactPage.tsx` (dự kiến, dưới `features/`).

**Query bind hiện tại:** không có SupplyParameterFromQuery.

Static content/contact links; no current submit endpoint. T11.01 / public-pages.spec.ts.


## Faq

**Source:** [Faq.razor](../../src/MiniLogistics.Web/Components/Pages/Public/Faq.razor). **Routes:** `/faq`.

**React page:** `public/FaqPage.tsx` (dự kiến, dưới `features/`).

**Query bind hiện tại:** không có SupplyParameterFromQuery.

Static FAQ/expand interactions; no Application query. T11.01 / public-pages.spec.ts.


## Home

**Source:** [Home.razor](../../src/MiniLogistics.Web/Components/Pages/Public/Home.razor). **Routes:** `/`.

**React page:** `public/HomePage.tsx` (dự kiến, dưới `features/`).

**Query bind hiện tại:** không có SupplyParameterFromQuery.

| ID | Action/use case hiện tại | Endpoint đích | Quyền/ràng buộc | Task / regression dự kiến |
| --- | --- | --- | --- | --- |
| A047 | `BannerService.GetActiveBannersAsync` | `GET P/banners` | Anonymous/active only | T09.04 / `BrowserBannerTests` đã có API; renderer T11.01 còn kế hoạch |

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `Dispose`, `NextBanner`, `OnInitializedAsync`, `PrevBanner`, `SetBanner`.

## Login

**Source:** [Login.razor](../../src/MiniLogistics.Web/Components/Pages/Public/Login.razor). **Routes:** `/login`.

**React page:** `auth/LoginPage.tsx` (dự kiến, dưới `features/`).

**Query bind hiện tại:** `error`.

POST /api/v1/app/auth/login; SignInManager + safe session; email/password/rememberMe; anonymous + CSRF; T02.02–03 / BrowserAuthenticationTests + auth.spec.ts.


## RegisterShop

**Source:** [RegisterShop.razor](../../src/MiniLogistics.Web/Components/Pages/Public/RegisterShop.razor). **Routes:** `/register-shop`.

**React page:** `auth/RegisterShopPage.tsx` (dự kiến, dưới `features/`).

**Query bind hiện tại:** `error`.

POST /api/v1/app/auth/register-shop; IRegisterShopService + Identity sign-in; RegisterShopCommand body, no actor; anonymous + CSRF; T02.02–03 / BrowserAuthenticationTests + auth.spec.ts.


**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `OnInitialized`.

## Tracking

**Source:** [Tracking.razor](../../src/MiniLogistics.Web/Components/Pages/Public/Tracking.razor). **Routes:** `/tracking`.

**React page:** `public/TrackingPage.tsx` (dự kiến, dưới `features/`).

**Query bind hiện tại:** `code`.

| ID | Action/use case hiện tại | Endpoint đích | Quyền/ràng buộc | Task / regression dự kiến |
| --- | --- | --- | --- | --- |
| A048 | `GetPublicTrackingService.GetAsync` | `GET P/tracking/{trackingCode}` | Anonymous/summary only + limiter | T11.02 / `BrowserPublicTrackingTests` |

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `HandleSearchAsync`, `OnParametersSetAsync`, `SearchAsync`.

## Error

**Source:** [Error.razor](../../src/MiniLogistics.Web/Components/Pages/Shared/Error.razor). **Routes:** `/Error`.

**React page:** `public/ErrorPage.tsx` (dự kiến, dưới `features/`).

**Query bind hiện tại:** không có SupplyParameterFromQuery.

Safe error page/correlation ID; do not show exception. T01.03 + T11.05 / root-cutover.spec.ts.


**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `OnInitialized`.

## NotFound

**Source:** [NotFound.razor](../../src/MiniLogistics.Web/Components/Pages/Shared/NotFound.razor). **Routes:** `/not-found`.

**React page:** `public/NotFoundPage.tsx` (dự kiến, dưới `features/`).

**Query bind hiện tại:** không có SupplyParameterFromQuery.

NotFound navigation state; APIs/assets must retain HTTP 404. T01.02 + T11.05 / BrowserRootRoutingTests.


## CreateShipment

**Source:** [CreateShipment.razor](../../src/MiniLogistics.Web/Components/Pages/Shipments/CreateShipment.razor). **Routes:** `/shipments/create`.

**React page:** `features/shipments/ShipmentFormPage.tsx` và `ShipmentForm.tsx` đã triển khai ở Phase 4. Route React `/app/shipments/create` giữ `shopId/shipmentId`; API quote và commands có prefix `/api/v1/app`. Tỉnh/phường dùng shared reference query. Update draft rồi submit là hai command tuần tự như Blazor; phí do server tính lại. Editing cần ViewFullPii + ViewCod để không ghi đè masked DTO; không nâng quyền đọc trong Application. Phase 5 thêm import/download trên list/detail, Blazor cũ giữ trong coexist.

**Query bind hiện tại:** `shopId`, `shipmentId`.

| ID | Action/use case hiện tại | Endpoint đích | Quyền/ràng buộc | Task / regression dự kiến |
| --- | --- | --- | --- | --- |
| A049 | `AdministrativeDivisionService.GetProvincesAsync` | `GET P/reference-data/provinces` | Anonymous safe reference | T02.02 / `BrowserReferenceDataTests` |
| A050 | `CreateDraftShipmentService.CreateAsync` | `POST /shops/{shopId}/shipments/drafts` | Shop/ManageShipments; active Shop | T04.02 / `BrowserShopShipmentCommandTests` |
| A051 | `CreateShipmentService.CreateAsync` | `POST /shops/{shopId}/shipments` | Shop/ManageShipments; active Shop | T04.02 / `BrowserShopShipmentCommandTests` |
| A052 | `GetShipmentDetailForCurrentShopService.GetAsync` | `GET /shops/{shopId}/shipments/{shipmentId}` | Shop/ViewShipments; masked PII/COD | T03.01 / `BrowserShopShipmentReadTests` |
| A053 | `GetShopContextService.GetAsync` | `GET /shop-context?shopId={shopId}` | Shop/ViewShipments | T02.04 / `BrowserShopContextTests` |
| A054 | `RouteClassificationService.Classify` | `POST /shops/{shopId}/shipping/quote` | Shop/ManageShipments | T04.01 / `BrowserShippingQuoteTests` |
| A055 | `ShippingFeeService.CalculateAsync` | `POST /shops/{shopId}/shipping/quote` | Shop/ManageShipments | T04.01 / `BrowserShippingQuoteTests` |
| A056 | `SubmitDraftShipmentService.SubmitAsync` | `POST /shops/{shopId}/shipments/{shipmentId}/submit` | Shop/ManageShipments; active Shop | T04.02 / `BrowserShopShipmentCommandTests` |
| A057 | `UpdateShipmentBeforePickupService.UpdateAsync` | `PUT /shops/{shopId}/shipments/{shipmentId}` | Shop/ManageShipments; Draft/PendingPickup | T04.02 / `BrowserShopShipmentCommandTests` |

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `CalculateFeeEstimateAsync`, `CreateShipmentAsync`, `GetCurrentUserIdAsync`, `HandleDeliveryProvinceChanged`, `HandlePickupProvinceChanged`, `HandleSaveDraftAsync`, `HandleSubmitAsync`, `LoadEditableShipmentAsync`, `OnParametersSetAsync`, `OnShopChanged`, `PopulateForm`, `RefreshEditingShipmentAsync`, `ResetFeeBreakdown`, `ResetFormAsync`, `ResetShipmentFields`, `SetEstimate`, `SubmitDraftFromFormAsync`, `UpdateBeforePickupAsync`.

## ShipmentDetail

**Source:** [ShipmentDetail.razor](../../src/MiniLogistics.Web/Components/Pages/Shipments/ShipmentDetail.razor). **Routes:** `/shipments/{Id:guid}`.

**React page:** `features/shipments/ShipmentDetailPage.tsx` read/history từ Phase 3; `ShipmentActions.tsx` thêm edit/submit/cancel ở Phase 4. Submit/cancel có xác nhận, active/ManageShipments/status gate và server kiểm tra lại; edit còn cần snapshot không bị che. Assignment/proof projection chưa có, không dựng UI giả.

**Query bind hiện tại:** `shopId`.

| ID | Action/use case hiện tại | Endpoint đích | Quyền/ràng buộc | Task / regression dự kiến |
| --- | --- | --- | --- | --- |
| A058 | `CancelShipmentForCurrentShopService.CancelAsync` | `POST /shops/{shopId}/shipments/{shipmentId}/cancel` | Shop/ManageShipments; allowed status | T04.02 / `BrowserShopShipmentCommandTests` |
| A059 | `GetShipmentDetailForCurrentShopService.GetAsync` | `GET /shops/{shopId}/shipments/{shipmentId}` | Shop/ViewShipments; masked PII/COD | T03.01 / `BrowserShopShipmentReadTests` |
| A060 | `GetShopContextService.GetAsync` | `GET /shop-context?shopId={shopId}` | Shop/ViewShipments | T02.04 / `BrowserShopContextTests` |
| A061 | `SubmitDraftShipmentService.SubmitAsync` | `POST /shops/{shopId}/shipments/{shipmentId}/submit` | Shop/ManageShipments; active Shop | T04.02 / `BrowserShopShipmentCommandTests` |

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `CancelCancelConfirmation`, `CancelShipmentAsync`, `ConfirmCancelShipmentAsync`, `GetCurrentUserIdAsync`, `LoadShipmentAsync`, `OnParametersSetAsync`, `OnShopChanged`, `RequestCancelShipment`, `SubmitDraftAsync`.

## Shipments

**Source:** [Shipments.razor](../../src/MiniLogistics.Web/Components/Pages/Shipments/Shipments.razor). **Routes:** `/shipments`.

**React page:** `features/shipments/ShipmentListPage.tsx` đã triển khai server paging/filter/sort và URL state ở Phase 3; Phase 4 thêm link tạo React. Phase 5 có `features/shipment-import/ShipmentImportPanel.tsx`, editable preview và polling2s/lookup Batch ID; chọn file rồi preview, confirm revalidate. CSV2MB/500 rows; numeric parse lỗi phải nhập lại; collapse giữ guard pending/unknown. Export filter theo URL list, bỏ paging; file routes giữ nguyên.

**Query bind hiện tại:** `shopId`, `status`, `fromUtc`, `toUtc`.

| ID | Action/use case hiện tại | Endpoint đích | Quyền/ràng buộc | Task / regression dự kiến |
| --- | --- | --- | --- | --- |
| A062 | `ConfirmShipmentImportService.ConfirmAsync` | `POST /shops/{shopId}/shipment-imports/confirm` | Shop/ManageShipments; active Shop | T05.01 / `BrowserShipmentImportTests` |
| A063 | `GetShipmentImportBatchService.GetAsync` | `GET /shops/{shopId}/shipment-imports/{batchId}` | Shop/use-case batch ownership | T05.01 / `BrowserShipmentImportTests` |
| A064 | `GetShipmentsForCurrentShopService.SearchAsync` | `GET /shops/{shopId}/shipments` | Shop/ViewShipments; PII/COD filter permissions | T03.01 / `BrowserShopShipmentReadTests` |
| A065 | `GetShopContextService.GetAsync` | `GET /shop-context?shopId={shopId}` | Shop/ViewShipments | T02.04 / `BrowserShopContextTests` |
| A066 | `PreviewShipmentImportService.PreviewAsync` | `POST /shops/{shopId}/shipment-imports/preview` | Shop/ManageShipments; active Shop | T05.01 / `BrowserShipmentImportTests` |

**Download local:** CSV template `shipment-import-template.csv` và lỗi preview `shipment-import-errors.csv` đã chuyển sang Blob download local ở Phase 5, có escaping/formula protection/revoke object URL. Batch lỗi sau confirm dùng file endpoint bên dưới. File API legacy vẫn phục vụ Blazor và React cookie downloads, không thay public URL.

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `ApplyShipmentFiltersAsync`, `ClearImportState`, `ClearShipmentFiltersAsync`, `ConfirmImportAsync`, `GetCurrentUserIdAsync`, `HandleImportFileChangedAsync`, `LoadShipmentItemsAsync`, `NextShipmentPageAsync`, `OnParametersSetAsync`, `OnShopChanged`, `PollImportBatchAsync`, `PreviousShipmentPageAsync`, `ReloadShipmentItemsForCurrentUserAsync`, `ResetShipmentResults`, `ToggleImportPanel`.

## ShipperShipments

**Source:** [ShipperShipments.razor](../../src/MiniLogistics.Web/Components/Pages/Shipper/ShipperShipments.razor). **Routes:** `/shipper/shipments`.

**React page:** `features/shipper/ShipperWorkspacePage.tsx` đã triển khai tại `/app/shipper/shipments`; navigation/login/returnUrl dùng React, Blazor cũ vẫn tồn tại. Stage/search/COD/page/businessDate theo URL; cards390px, confirm, availability, status/failure note và thu COD. A067–A073 đã có dưới prefix `/api/v1/app`.

**Query bind hiện tại:** không có SupplyParameterFromQuery.

| ID | Action/use case hiện tại | Endpoint đích | Quyền/ràng buộc | Task / regression dự kiến |
| --- | --- | --- | --- | --- |
| A067 | `ShipperSelfService.ProfileAsync` | `GET /shipper/me/profile` | Shipper/self projection + aggregate load; không enumerate active shippers | T07.01 / `BrowserShipperTests` |
| A068 | `GetAssignedShipmentsForShipperService.SearchAsync` | `GET /shipper/me/workspace` | Shipper/self assignment | T07.01 / `BrowserShipperTests` |
| A069 | `GetShipperCodDailySummaryService.GetAsync` | `GET /shipper/me/cod-summary` | Shipper/self | T07.01 / `BrowserShipperTests` |
| A070 | `GetShipperWorkingAreasService.GetAsync` | `GET /shipper/me/working-areas` | Shipper/self | T07.01 / `BrowserShipperTests` |
| A071 | `MarkCodCollectedService.MarkCollectedAsync` | `POST /shipper/me/shipments/{shipmentId}/cod/collect` | Shipper/own active assignment | T07.01 / `BrowserShipperTests` |
| A072 | `SetShipperAvailabilityService.SetAsync` | `PUT /shipper/me/availability` | Shipper/self | T07.01 / `BrowserShipperTests` |
| A073 | `UpdateShipmentStatusService.UpdateAsync` | `POST /shipper/me/shipments/{shipmentId}/status` | Shipper/own active assignment | T07.01 / `BrowserShipperTests` |

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `ApplyFiltersAsync`, `CancelShipperAction`, `ConfirmShipperActionAsync`, `GetCurrentUserIdAsync`, `LoadAsync`, `LoadCodSummaryAsync`, `LoadShipperProfileAsync`, `MarkCodCollectedAsync`, `NextPageAsync`, `OnInitializedAsync`, `PreviousPageAsync`, `RefreshAsync`, `RequestMarkCodCollected`, `RequestStatusUpdate`, `RequestToggleAvailability`, `ResetFiltersAsync`, `SelectFailureReason`, `SelectStatus`, `SetStageAsync`, `SyncPerShipmentState`, `ToggleAvailabilityAsync`, `UpdateStatusAsync`.

## Dashboard

**Source:** [Dashboard.razor](../../src/MiniLogistics.Web/Components/Pages/Shop/Dashboard.razor). **Routes:** `/dashboard`.

**React page:** `features/dashboard/ShopDashboardPage.tsx` đã triển khai KPI/status distribution/finance/latest và drill-down ở Phase 3.

**Query bind hiện tại:** `shopId`, `fromUtc`, `toUtc`.

| ID | Action/use case hiện tại | Endpoint đích | Quyền/ràng buộc | Task / regression dự kiến |
| --- | --- | --- | --- | --- |
| A074 | `GetShipmentsForCurrentShopService.SearchAsync` | `GET /shops/{shopId}/shipments` | Shop/ViewShipments; PII/COD filter permissions | T03.01 / `BrowserShopShipmentReadTests` |
| A075 | `GetShopContextService.GetAsync` | `GET /shop-context?shopId={shopId}` | Shop/ViewShipments | T02.04 / `BrowserShopContextTests` |
| A076 | `GetShopDashboardKpiService.GetAsync` | `GET /shops/{shopId}/dashboard` | Shop/ViewShipments; ViewCod projection | T03.03 / `BrowserShopShipmentReadTests + dashboard.spec.ts` |

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `ApplyDateRangeAsync`, `ApplyPresetAsync`, `BuildDashboardModel`, `ClearDateRangeAsync`, `GetCurrentUserIdAsync`, `OnParametersSetAsync`, `OnShopChanged`.

## ShopAuditLogs

**Source:** [ShopAuditLogs.razor](../../src/MiniLogistics.Web/Components/Pages/Shop/ShopAuditLogs.razor). **Routes:** `/shop/audit`.

**React page:** `features/shop-settings/ShopAuditPage.tsx` - implemented under `/app`; legacy route retained. HTTP/browser evidence: migration validation log.

**Query bind hiện tại:** không có SupplyParameterFromQuery.

| ID | Action/use case hiện tại | Endpoint đích | Quyền/ràng buộc | Task / regression dự kiến |
| --- | --- | --- | --- | --- |
| A077 | `AuditService.GetAsync` | `GET /shops/{shopId}/audit` | Shop/ViewAudit | T08.03 / `BrowserShopSettingsTests` |
| A078 | `ShopContextService.GetAsync` | `GET /shop-context?shopId={shopId}` | Shop/ViewShipments | T02.04 / `BrowserShopContextTests` |

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `GetUserIdAsync`, `LoadAsync`, `OnInitializedAsync`, `OnShopChanged`.

## ShopCodReport

**Source:** [ShopCodReport.razor](../../src/MiniLogistics.Web/Components/Pages/Shop/ShopCodReport.razor). **Routes:** `/shop/cod-report`.

**React page:** `features/shop-settings/ShopCodReportPage.tsx` - implemented under `/app`; legacy route retained. HTTP/browser evidence: migration validation log.

**Query bind hiện tại:** `shopId`, `fromUtc`, `toUtc`.

| ID | Action/use case hiện tại | Endpoint đích | Quyền/ràng buộc | Task / regression dự kiến |
| --- | --- | --- | --- | --- |
| A079 | `ShopReportingService.GetAsync` | `GET /shops/{shopId}/cod-report` | Shop/ViewCod | T08.03 / `BrowserShopSettingsTests` |
| A080 | `GetShopContextService.GetAsync` | `GET /shop-context?shopId={shopId}` | Shop/ViewShipments | T02.04 / `BrowserShopContextTests` |

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `ApplyDateRangeAsync`, `ClearDateRangeAsync`, `GetCurrentUserIdAsync`, `OnParametersSetAsync`, `OnShopChanged`.

## ShopNotifications

**Source:** [ShopNotifications.razor](../../src/MiniLogistics.Web/Components/Pages/Shop/ShopNotifications.razor). **Routes:** `/shop/notifications`.

**React page:** `features/shop-settings/ShopNotificationsPage.tsx` - implemented under `/app`; legacy route retained. HTTP/browser evidence: migration validation log.

**Query bind hiện tại:** không có SupplyParameterFromQuery.

| ID | Action/use case hiện tại | Endpoint đích | Quyền/ràng buộc | Task / regression dự kiến |
| --- | --- | --- | --- | --- |
| A081 | `NotificationService.GetAsync` | `GET /shops/{shopId}/notifications` | Shop/ViewShipments; user ownership | T08.03 / `BrowserShopSettingsTests` |
| A082 | `NotificationService.GetPreferencesAsync` | `GET /shops/{shopId}/notifications/preferences` | Shop/ManageNotifications | T08.03 / `BrowserShopSettingsTests` |
| A083 | `NotificationService.MarkReadAsync` | `POST /shops/{shopId}/notifications/{notificationId}/read` | Shop/ViewShipments; user ownership | T08.03 / `BrowserShopSettingsTests` |
| A084 | `NotificationService.UpdatePreferencesAsync` | `PUT /shops/{shopId}/notifications/preferences` | Shop/ManageNotifications | T08.03 / `BrowserShopSettingsTests` |
| A085 | `ShopContextService.GetAsync` | `GET /shop-context?shopId={shopId}` | Shop/ViewShipments | T02.04 / `BrowserShopContextTests` |

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `GetUserIdAsync`, `LoadAsync`, `MarkReadAsync`, `OnInitializedAsync`, `OnShopChanged`, `SavePreferencesAsync`, `ShowError`, `ToggleEvent`.

## ShopProfile

**Source:** [ShopProfile.razor](../../src/MiniLogistics.Web/Components/Pages/Shop/ShopProfile.razor). **Routes:** `/profile`.

**React page:** `features/shop-settings/ShopProfilePage.tsx` - implemented under `/app`; legacy route retained. HTTP/browser evidence: migration validation log.

**Query bind hiện tại:** `shopId`.

| ID | Action/use case hiện tại | Endpoint đích | Quyền/ràng buộc | Task / regression dự kiến |
| --- | --- | --- | --- | --- |
| A086 | `CreateAdditionalShopService.CreateAsync` | `POST /shops` | Shop/owner use case | T08.01 / `BrowserShopProfileTests` |
| A087 | `GetShopContextService.GetAsync` | `GET /shop-context?shopId={shopId}` | Shop/ViewShipments | T02.04 / `BrowserShopContextTests` |
| A088 | `GetShopProfileService.GetAsync` | `GET /shops/{shopId}/profile` | Shop/ViewShipments; PII masking | T08.01 / `BrowserShopProfileTests` |
| A089 | `UpdateShopProfileService.UpdateAsync` | `PUT /shops/{shopId}/profile` | Shop/ManageShopProfile | T08.01 / `BrowserShopProfileTests` |

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `CreateAdditionalShopAsync`, `GetCurrentUserIdAsync`, `LoadAsync`, `OnParametersSetAsync`, `OnShopChanged`, `ResetNewShopForm`, `SaveAsync`, `SyncForm`.

## ShopStaff

**Source:** [ShopStaff.razor](../../src/MiniLogistics.Web/Components/Pages/Shop/ShopStaff.razor). **Routes:** `/shop/staff`.

**React page:** `features/shop-settings/ShopStaffPage.tsx` - implemented under `/app`; legacy route retained. HTTP/browser evidence: migration validation log.

**Query bind hiện tại:** `shopId`.

| ID | Action/use case hiện tại | Endpoint đích | Quyền/ràng buộc | Task / regression dự kiến |
| --- | --- | --- | --- | --- |
| A090 | `GetShopContextService.GetAsync` | `GET /shop-context?shopId={shopId}` | Shop/ViewShipments | T02.04 / `BrowserShopContextTests` |
| A091 | `ShopStaffManagementService.CreateAsync` | `POST /shops/{shopId}/staff` | Shop owner only | T08.02 / `BrowserShopSettingsTests` |
| A092 | `ShopStaffManagementService.GetAsync` | `GET /shops/{shopId}/staff` | Shop owner only | T08.02 / `BrowserShopSettingsTests` |
| A093 | `ShopStaffManagementService.UpdateAsync` | `PUT /shops/{shopId}/staff/{staffUserId}` | Shop owner only; bind response.UserId, not MembershipId | T08.02 / `BrowserShopSettingsTests` |

**Handler parity (local/lifecycle + orchestrations; không mặc định tạo endpoint):** `CancelSaveStaff`, `ConfirmSaveStaffAsync`, `CreateStaffAsync`, `GetCurrentUserIdAsync`, `LoadStaffAsync`, `OnCreateRoleChanged`, `OnParametersSetAsync`, `OnShopChanged`, `RequestSaveStaff`, `SaveStaffAsync`, `ToggleCreatePermission`.

## Auth, layout và endpoints giữ lại

| Action | Contract/path | Quyền / test / task |
| --- | --- | --- |
| Session bootstrap | GET `/api/v1/app/auth/me` → Session; GET `/api/v1/app/auth/csrf` → requestToken | me authenticated; csrf anonymous/no-store; T02.02 / BrowserAuthenticationTests |
| Logout từ MainLayout/NavMenu | POST `/api/v1/app/auth/logout` → 204; legacy POST `/auth/logout` vẫn giữ redirect | authenticated + CSRF; T02.02–03 / auth.spec.ts |
| Auth form legacy | POST `/auth/login`, `/auth/register-shop`, `/auth/logout` | Giữ antiforgery/redirect behavior; AuthenticationAntiforgeryTests |
| Culture selector | GET `/culture/set?culture=vi-VN|en-US&returnUrl={local}` | Giữ endpoint hiện tại; local return URL; shell.spec.ts |
| CSV shipment export | GET `/shop/files/shipments/export.csv` + Shop/list filters | ExportData; thêm ViewFullPii/ViewCod nếu filter/sort nhạy cảm; T05.03 / BrowserFileDownloadTests |
| CSV COD export | GET `/shop/files/cod-report.csv?shopId&fromUtc&toUtc` | Quyền ExportData + ViewCod theo use case; T05.03 / BrowserFileDownloadTests |
| Label PDF | GET `/shop/files/shipments/{shipmentId}/label.pdf?shopId` | ViewShipments + ViewFullPii; non-Draft; T05.03 / BrowserFileDownloadTests |
| CSV batch errors | GET `/shop/files/import-batches/{batchId}/errors.csv?shopId` | Batch/Shop access trong IExportShipmentImportErrorsService; T05.03 / BrowserFileDownloadTests |
| Home carousel/nav/local filter/confirm | Local React interactions; no new API | Không sửa dữ liệu để giả thành công; feature component tests |
| Host/backend | `/health/live`, `/health/ready`, `/openapi/v1.json` non-production, `/api/v1/partner/*`, static/upload images | Giữ route ownership, HTTP status và partner contract; BrowserSpaHostingTests + PartnerApiContractTests |

## Service input/response bindings

Các signatures dưới đây được đọc từ interface hiện có; **không** biến signature thành body JSON nguyên xi. Actor fields (CreatedBy/Current/Owner/RequestedBy/ChangedBy/AssignedBy/ReassignedBy/CancelledBy/CollectedBy/SettledBy/SubmittedBy UserId, và UserId khi là người thực hiện) lấy principal. Shop/resource ID từ route/query đã validate, CancellationToken lấy RequestAborted; target userId vẫn là dữ liệu quản trị cần authorize. Với shipper/me, shipperUserId luôn là principal, không nhận target shipper từ query/body.

Với input `XCommand`/`XQuery`, request browser gồm các field không phải actor/path ID trong record X (cùng defaults), camelCase; output bỏ Result wrapper và map decimal/enum/permission như contract. Tên record, nullability và nested DTO lấy file nguồn của type, không đoán từ nhãn UI. Nếu use case không có actor, phải bổ sung authorized entry hoặc focused authorized use case trong task chỉ định trước khi expose.

| Service.method | Source | Current input signature | Current response payload |
| --- | --- | --- | --- |
| `IAdminSystemConfigurationService.CreateFeeRuleVersionAsync` | [IAdminSystemConfigurationService.cs](../../src/MiniLogistics.Application/AdminSystemConfiguration/IAdminSystemConfigurationService.cs) | `CreateFeeRuleVersionCommand command, CancellationToken cancellationToken = default` | `FeeRuleConfigResponse` |
| `IAdminSystemConfigurationService.GetAsync` | [IAdminSystemConfigurationService.cs](../../src/MiniLogistics.Application/AdminSystemConfiguration/IAdminSystemConfigurationService.cs) | `Guid requestedByUserId, CancellationToken cancellationToken = default` | `AdminSystemConfigurationResponse` |
| `IAdminSystemConfigurationService.UpsertRouteRegionAsync` | [IAdminSystemConfigurationService.cs](../../src/MiniLogistics.Application/AdminSystemConfiguration/IAdminSystemConfigurationService.cs) | `UpsertRouteRegionConfigCommand command, CancellationToken cancellationToken = default` | `RouteRegionConfigResponse` |
| `IApiClientManagementService.CreateApiClientAsync` | [IApiClientManagementService.cs](../../src/MiniLogistics.Application/PartnerApi/IApiClientManagementService.cs) | `CreatePartnerApiClientCommand command, CancellationToken cancellationToken = default` | `PartnerApiClientSecretResponse` |
| `IApiClientManagementService.RotateApiClientKeyAsync` | [IApiClientManagementService.cs](../../src/MiniLogistics.Application/PartnerApi/IApiClientManagementService.cs) | `RotatePartnerApiClientKeyCommand command, CancellationToken cancellationToken = default` | `PartnerApiClientSecretResponse` |
| `IApiClientManagementService.SetApiClientActiveStatusAsync` | [IApiClientManagementService.cs](../../src/MiniLogistics.Application/PartnerApi/IApiClientManagementService.cs) | `SetPartnerApiClientActiveStatusCommand command, CancellationToken cancellationToken = default` | `Result` |
| `IApiClientManagementService.UpdateApiClientSecurityAsync` | [IApiClientManagementService.cs](../../src/MiniLogistics.Application/PartnerApi/IApiClientManagementService.cs) | `UpdatePartnerApiClientSecurityCommand command, CancellationToken cancellationToken = default` | `Result` |
| `IAssignShipperToShipmentService.AssignAsync` | [IAssignShipperToShipmentService.cs](../../src/MiniLogistics.Application/Shipments/AssignShipperToShipment/IAssignShipperToShipmentService.cs) | `AssignShipperCommand command, CancellationToken cancellationToken = default` | `Result` |
| `IAutoAssignShipmentService.AutoAssignAsync` | [IAutoAssignShipmentService.cs](../../src/MiniLogistics.Application/Shipments/AutoAssignShipment/IAutoAssignShipmentService.cs) | `Guid shipmentId, CancellationToken cancellationToken = default, Guid? requestedByUserId = null` | `AutoAssignShipmentResult` |
| `IBannerService.CreateBannerAsync` | [IBannerService.cs](../../src/MiniLogistics.Application/Banners/IBannerService.cs) | `Guid actorUserId, string title, string imageUrl, string? linkUrl, int sortOrder, bool isActive, CancellationToken cancellationToken = default` | `BannerDto` |
| `IBannerService.DeleteBannerAsync` | [IBannerService.cs](../../src/MiniLogistics.Application/Banners/IBannerService.cs) | `Guid actorUserId, Guid id, CancellationToken cancellationToken = default` | `Result` |
| `IBannerService.GetActiveBannersAsync` | [IBannerService.cs](../../src/MiniLogistics.Application/Banners/IBannerService.cs) | `CancellationToken cancellationToken = default` | `IReadOnlyList<BannerDto>` |
| `IBannerService.GetAllBannersAsync` | [IBannerService.cs](../../src/MiniLogistics.Application/Banners/IBannerService.cs) | `Guid actorUserId, CancellationToken cancellationToken = default` | `IReadOnlyList<BannerDto>` |
| `IBannerService.ToggleBannerStatusAsync` | [IBannerService.cs](../../src/MiniLogistics.Application/Banners/IBannerService.cs) | `Guid actorUserId, Guid id, CancellationToken cancellationToken = default` | `Result` |
| `IBannerService.UpdateBannerAsync` | [IBannerService.cs](../../src/MiniLogistics.Application/Banners/IBannerService.cs) | `Guid actorUserId, Guid id, string title, string imageUrl, string? linkUrl, int sortOrder, CancellationToken cancellationToken = default` | `BannerDto` |
| `IUploadBannerImageService.UploadAsync` | [UploadBannerImageService.cs](../../src/MiniLogistics.Application/Banners/UploadBannerImageService.cs) | `Guid actorUserId, Stream content, string fileName, string contentType, CancellationToken cancellationToken = default` | `BannerImageResponse` |
| `IBulkRetryAutoAssignmentService.RetryAsync` | [IBulkRetryAutoAssignmentService.cs](../../src/MiniLogistics.Application/Shipments/BulkRetryAutoAssignment/IBulkRetryAutoAssignmentService.cs) | `BulkRetryAutoAssignmentCommand command, CancellationToken cancellationToken = default` | `BulkRetryAutoAssignmentResult` |
| `ICancelShipmentAssignmentService.CancelAsync` | [ICancelShipmentAssignmentService.cs](../../src/MiniLogistics.Application/Shipments/CancelShipmentAssignment/ICancelShipmentAssignmentService.cs) | `CancelShipmentAssignmentCommand command, CancellationToken cancellationToken = default` | `Result` |
| `ICancelShipmentForCurrentShopService.CancelAsync` | [ICancelShipmentForCurrentShopService.cs](../../src/MiniLogistics.Application/Shipments/CancelShipmentForCurrentShop/ICancelShipmentForCurrentShopService.cs) | `CancelShipmentCommand command, CancellationToken cancellationToken = default` | `Result` |
| `IConfirmShipmentImportService.ConfirmAsync` | [IShipmentImportServices.cs](../../src/MiniLogistics.Application/Shipments/ImportShipments/IShipmentImportServices.cs) | `ConfirmShipmentImportCommand command, CancellationToken cancellationToken = default` | `ShipmentImportConfirmResponse` |
| `ICreateAdditionalShopService.CreateAsync` | [ICreateAdditionalShopService.cs](../../src/MiniLogistics.Application/Shops/CreateAdditionalShop/ICreateAdditionalShopService.cs) | `CreateAdditionalShopCommand command, CancellationToken cancellationToken = default` | `CreateAdditionalShopResponse` |
| `ICreateDraftShipmentService.CreateAsync` | [ICreateDraftShipmentService.cs](../../src/MiniLogistics.Application/Shipments/DraftShipments/ICreateDraftShipmentService.cs) | `CreateDraftShipmentCommand command, CancellationToken cancellationToken = default` | `DraftShipmentResponse` |
| `ICreateHubService.CreateAsync` | [ICreateHubService.cs](../../src/MiniLogistics.Application/AdminHubs/CreateHub/ICreateHubService.cs) | `CreateHubCommand command, CancellationToken cancellationToken = default` | `AdminHubResponse` |
| `ICreateInternalUserService.CreateAsync` | [ICreateInternalUserService.cs](../../src/MiniLogistics.Application/AdminUsers/CreateInternalUser/ICreateInternalUserService.cs) | `CreateInternalUserCommand command, CancellationToken cancellationToken = default` | `CreateInternalUserResponse` |
| `ICreateShipmentService.CreateAsync` | [ICreateShipmentService.cs](../../src/MiniLogistics.Application/Shipments/CreateShipment/ICreateShipmentService.cs) | `CreateShipmentCommand command, CancellationToken cancellationToken = default` | `CreateShipmentResponse` |
| `IGetActiveShippersService.GetAsync` | [IGetActiveShippersService.cs](../../src/MiniLogistics.Application/Shippers/GetActiveShippers/IGetActiveShippersService.cs) | `CancellationToken cancellationToken = default` | `IReadOnlyList<GetActiveShipperResponse>` |
| `IGetAdminAuditLogsService.GetAsync` | [IGetAdminAuditLogsService.cs](../../src/MiniLogistics.Application/AdminAuditing/IGetAdminAuditLogsService.cs) | `AdminAuditLogQuery query, CancellationToken cancellationToken = default` | `IReadOnlyList<AdminAuditLogResponse>` |
| `IGetAdminCodReportService.GetAsync` | [IGetAdminCodReportService.cs](../../src/MiniLogistics.Application/AdminCod/IGetAdminCodReportService.cs) | `AdminCodReportQuery query, CancellationToken cancellationToken = default` | `AdminCodReportResponse` |
| `IGetAdminDashboardService.GetAsync` | [IGetAdminDashboardService.cs](../../src/MiniLogistics.Application/AdminDashboard/IGetAdminDashboardService.cs) | `AdminDashboardQuery query, CancellationToken cancellationToken = default` | `AdminDashboardResponse` |
| `IGetAdminHubsService.GetAsync` | [IGetAdminHubsService.cs](../../src/MiniLogistics.Application/AdminHubs/GetAdminHubs/IGetAdminHubsService.cs) | `AdminHubQuery query, CancellationToken cancellationToken = default` | `IReadOnlyList<AdminHubResponse>` |
| `IGetAdminShopsService.GetAsync` | [IGetAdminShopsService.cs](../../src/MiniLogistics.Application/Shops/GetAdminShops/IGetAdminShopsService.cs) | `Guid currentUserId, CancellationToken cancellationToken = default` | `IReadOnlyList<GetAdminShopResponse>` |
| `IGetAdminUsersService.SearchAsync` | [IGetAdminUsersService.cs](../../src/MiniLogistics.Application/AdminUsers/GetAdminUsers/IGetAdminUsersService.cs) | `GetAdminUsersQuery query, CancellationToken cancellationToken = default` | `PagedResponse<GetAdminUserResponse>` |
| `IGetAssignedShipmentsForShipperService.SearchAsync` | [IGetAssignedShipmentsForShipperService.cs](../../src/MiniLogistics.Application/Shipments/GetAssignedShipmentsForShipper/IGetAssignedShipmentsForShipperService.cs) | `Guid shipperUserId, GetAssignedShipmentsForShipperQuery query, CancellationToken cancellationToken = default` | `PagedResponse<GetAssignedShipmentForShipperResponse>` |
| `IGetCodSettlementCandidatesService.GetAsync` | [IGetCodSettlementCandidatesService.cs](../../src/MiniLogistics.Application/CashOnDelivery/GetCodSettlementCandidates/IGetCodSettlementCandidatesService.cs) | `CancellationToken cancellationToken = default` | `IReadOnlyList<GetCodSettlementCandidateResponse>` |
| `IGetLogisticsHubsService.GetAsync` | [IGetLogisticsHubsService.cs](../../src/MiniLogistics.Application/Shippers/GetLogisticsHubs/IGetLogisticsHubsService.cs) | `bool activeOnly = true, CancellationToken cancellationToken = default` | `IReadOnlyList<LogisticsHubResponse>` |
| `IGetOperationsShipmentsService.SearchAsync` | [IGetOperationsShipmentsService.cs](../../src/MiniLogistics.Application/Shipments/GetOperationsShipments/IGetOperationsShipmentsService.cs) | `GetOperationsShipmentsQuery query, CancellationToken cancellationToken = default` | `PagedResponse<GetOperationsShipmentResponse>` |
| `IGetPendingPickupShipmentsService.SearchAsync` | [IGetPendingPickupShipmentsService.cs](../../src/MiniLogistics.Application/Shipments/GetPendingPickupShipments/IGetPendingPickupShipmentsService.cs) | `GetPendingPickupShipmentsQuery query, CancellationToken cancellationToken = default` | `PagedResponse<GetPendingPickupShipmentResponse>` |
| `IGetPublicTrackingService.GetAsync` | [IGetPublicTrackingService.cs](../../src/MiniLogistics.Application/Shipments/GetPublicTracking/IGetPublicTrackingService.cs) | `string trackingCode, string? phoneLast4 = null, CancellationToken cancellationToken = default` | `PublicTrackingResponse` |
| `IGetShipmentDetailForCurrentShopService.GetAsync` | [IGetShipmentDetailForCurrentShopService.cs](../../src/MiniLogistics.Application/Shipments/GetShipmentDetailForCurrentShop/IGetShipmentDetailForCurrentShopService.cs) | `Guid ownerUserId, Guid shipmentId, Guid? shopId = null, CancellationToken cancellationToken = default` | `ShipmentDetailResponse` |
| `IGetShipmentImportBatchService.GetAsync` | [IShipmentImportServices.cs](../../src/MiniLogistics.Application/Shipments/ImportShipments/IShipmentImportServices.cs) | `Guid currentUserId, Guid? shopId, Guid batchId, CancellationToken cancellationToken = default` | `ShipmentImportConfirmResponse` |
| `IGetShipmentsForCurrentShopService.SearchAsync` | [IGetShipmentsForCurrentShopService.cs](../../src/MiniLogistics.Application/Shipments/GetShipmentsForCurrentShop/IGetShipmentsForCurrentShopService.cs) | `GetShipmentsForCurrentShopQuery query, CancellationToken cancellationToken = default` | `PagedResponse<ShipmentListItemResponse>` |
| `IGetShipperCodDailySummaryService.GetAsync` | [IGetShipperCodDailySummaryService.cs](../../src/MiniLogistics.Application/CashOnDelivery/GetShipperCodDailySummary/IGetShipperCodDailySummaryService.cs) | `Guid shipperUserId, DateOnly? businessDate = null, CancellationToken cancellationToken = default` | `ShipperCodDailySummaryResponse` |
| `IGetShipperWorkingAreasService.GetAsync` | [IGetShipperWorkingAreasService.cs](../../src/MiniLogistics.Application/Shippers/GetShipperWorkingAreas/IGetShipperWorkingAreasService.cs) | `Guid requestedByUserId, Guid shipperId, CancellationToken cancellationToken = default` | `IReadOnlyList<ShipperWorkingAreaResponse>` |
| `IGetShopAuditLogsService.GetAsync` | [IGetShopAuditLogsService.cs](../../src/MiniLogistics.Application/Shops/Audit/IGetShopAuditLogsService.cs) | `ShopAuditLogQuery query, CancellationToken cancellationToken = default` | `IReadOnlyList<ShopAuditLogResponse>` |
| `IGetShopCodReportService.GetAsync` | [IGetShopCodReportService.cs](../../src/MiniLogistics.Application/Shops/Reports/IGetShopCodReportService.cs) | `GetShopCodReportQuery query, CancellationToken cancellationToken = default` | `ShopCodReportResponse` |
| `IGetShopContextService.GetAsync` | [IGetShopContextService.cs](../../src/MiniLogistics.Application/Shops/GetShopContext/IGetShopContextService.cs) | `Guid currentUserId, Guid? selectedShopId = null, CancellationToken cancellationToken = default` | `GetShopContextResponse` |
| `IGetShopDashboardKpiService.GetAsync` | [IGetShopDashboardKpiService.cs](../../src/MiniLogistics.Application/Shops/Reports/IGetShopDashboardKpiService.cs) | `ShopDashboardKpiQuery query, CancellationToken cancellationToken = default` | `ShopDashboardKpiResponse` |
| `IGetShopProfileService.GetAsync` | [IGetShopProfileService.cs](../../src/MiniLogistics.Application/Shops/GetShopProfile/IGetShopProfileService.cs) | `Guid currentUserId, Guid? shopId = null, CancellationToken cancellationToken = default` | `GetShopProfileResponse` |
| `IMarkCodCollectedService.MarkCollectedAsync` | [IMarkCodCollectedService.cs](../../src/MiniLogistics.Application/CashOnDelivery/MarkCodCollected/IMarkCodCollectedService.cs) | `MarkCodCollectedCommand command, CancellationToken cancellationToken = default` | `Result` |
| `IMarkCodSettledService.MarkSettledAsync` | [IMarkCodSettledService.cs](../../src/MiniLogistics.Application/CashOnDelivery/MarkCodSettled/IMarkCodSettledService.cs) | `MarkCodSettledCommand command, CancellationToken cancellationToken = default` | `Result` |
| `IPreviewShipmentImportService.PreviewAsync` | [IShipmentImportServices.cs](../../src/MiniLogistics.Application/Shipments/ImportShipments/IShipmentImportServices.cs) | `PreviewShipmentImportCommand command, CancellationToken cancellationToken = default` | `ShipmentImportPreviewResponse` |
| `IReassignShipmentService.ReassignAsync` | [IReassignShipmentService.cs](../../src/MiniLogistics.Application/Shipments/ReassignShipment/IReassignShipmentService.cs) | `ReassignShipmentCommand command, CancellationToken cancellationToken = default` | `Result` |
| `IResetUserPasswordService.ResetAsync` | [IResetUserPasswordService.cs](../../src/MiniLogistics.Application/AdminUsers/ResetUserPassword/IResetUserPasswordService.cs) | `ResetUserPasswordCommand command, CancellationToken cancellationToken = default` | `Result` |
| `IRouteClassificationService.Classify` | [IRouteClassificationService.cs](../../src/MiniLogistics.Application/Routing/IRouteClassificationService.cs) | `string pickupProvince, string deliveryProvince` | `RouteClassificationResult` |
| `ISetHubActiveStatusService.SetAsync` | [ISetHubActiveStatusService.cs](../../src/MiniLogistics.Application/AdminHubs/SetHubActiveStatus/ISetHubActiveStatusService.cs) | `SetHubActiveStatusCommand command, CancellationToken cancellationToken = default` | `AdminHubResponse` |
| `ISetShipperAvailabilityService.SetAsync` | [ISetShipperAvailabilityService.cs](../../src/MiniLogistics.Application/Shippers/SetShipperAvailability/ISetShipperAvailabilityService.cs) | `SetShipperAvailabilityCommand command, CancellationToken cancellationToken = default` | `Result` |
| `ISetShipperCapacityService.SetAsync` | [ISetShipperCapacityService.cs](../../src/MiniLogistics.Application/AdminUsers/SetShipperCapacity/ISetShipperCapacityService.cs) | `SetShipperCapacityCommand command, CancellationToken cancellationToken = default` | `Result` |
| `ISetShipperWorkingAreasService.SetAsync` | [ISetShipperWorkingAreasService.cs](../../src/MiniLogistics.Application/Shippers/SetShipperWorkingAreas/ISetShipperWorkingAreasService.cs) | `SetShipperWorkingAreasCommand command, CancellationToken cancellationToken = default` | `IReadOnlyList<ShipperWorkingAreaResponse>` |
| `ISetShopActiveStatusService.SetAsync` | [ISetShopActiveStatusService.cs](../../src/MiniLogistics.Application/Shops/SetShopActiveStatus/ISetShopActiveStatusService.cs) | `SetShopActiveStatusCommand command, CancellationToken cancellationToken = default` | `Result` |
| `ISetUserActiveStatusService.SetAsync` | [ISetUserActiveStatusService.cs](../../src/MiniLogistics.Application/AdminUsers/SetUserActiveStatus/ISetUserActiveStatusService.cs) | `SetUserActiveStatusCommand command, CancellationToken cancellationToken = default` | `Result` |
| `IShippingFeeService.CalculateAsync` | [IShippingFeeService.cs](../../src/MiniLogistics.Application/Fees/IShippingFeeService.cs) | `RouteType routeType, Weight actualWeight, ParcelDimensions dimensions, Money declaredGoodsValue, CancellationToken cancellationToken = default` | `ShippingFeeQuote` |
| `IShopNotificationService.GetAsync` | [IShopNotificationService.cs](../../src/MiniLogistics.Application/Shops/Notifications/IShopNotificationService.cs) | `Guid currentUserId, Guid? shopId, int limit = 100, CancellationToken cancellationToken = default` | `IReadOnlyList<ShopNotificationResponse>` |
| `IShopNotificationService.GetPreferencesAsync` | [IShopNotificationService.cs](../../src/MiniLogistics.Application/Shops/Notifications/IShopNotificationService.cs) | `Guid currentUserId, Guid? shopId, CancellationToken cancellationToken = default` | `ShopNotificationPreferenceResponse` |
| `IShopNotificationService.MarkReadAsync` | [IShopNotificationService.cs](../../src/MiniLogistics.Application/Shops/Notifications/IShopNotificationService.cs) | `Guid currentUserId, Guid? shopId, Guid notificationId, CancellationToken cancellationToken = default` | `Result` |
| `IShopNotificationService.UpdatePreferencesAsync` | [IShopNotificationService.cs](../../src/MiniLogistics.Application/Shops/Notifications/IShopNotificationService.cs) | `Guid currentUserId, Guid? shopId, IReadOnlyCollection<string> enabledEventTypes, CancellationToken cancellationToken = default` | `ShopNotificationPreferenceResponse` |
| `IShopStaffManagementService.CreateAsync` | [IShopStaffManagementService.cs](../../src/MiniLogistics.Application/Shops/Staff/IShopStaffManagementService.cs) | `CreateShopStaffCommand command, CancellationToken cancellationToken = default` | `ShopStaffResponse` |
| `IShopStaffManagementService.GetAsync` | [IShopStaffManagementService.cs](../../src/MiniLogistics.Application/Shops/Staff/IShopStaffManagementService.cs) | `Guid currentUserId, Guid shopId, CancellationToken cancellationToken = default` | `IReadOnlyList<ShopStaffResponse>` |
| `IShopStaffManagementService.UpdateAsync` | [IShopStaffManagementService.cs](../../src/MiniLogistics.Application/Shops/Staff/IShopStaffManagementService.cs) | `UpdateShopStaffAccessCommand command, CancellationToken cancellationToken = default` | `ShopStaffResponse` |
| `ISubmitDraftShipmentService.SubmitAsync` | [ISubmitDraftShipmentService.cs](../../src/MiniLogistics.Application/Shipments/DraftShipments/ISubmitDraftShipmentService.cs) | `SubmitDraftShipmentCommand command, CancellationToken cancellationToken = default` | `DraftShipmentResponse` |
| `IUpdateHubService.UpdateAsync` | [IUpdateHubService.cs](../../src/MiniLogistics.Application/AdminHubs/UpdateHub/IUpdateHubService.cs) | `UpdateHubCommand command, CancellationToken cancellationToken = default` | `AdminHubResponse` |
| `IUpdateShipmentBeforePickupService.UpdateAsync` | [IUpdateShipmentBeforePickupService.cs](../../src/MiniLogistics.Application/Shipments/DraftShipments/IUpdateShipmentBeforePickupService.cs) | `UpdateShipmentBeforePickupCommand command, CancellationToken cancellationToken = default` | `DraftShipmentResponse` |
| `IUpdateShipmentStatusService.UpdateAsync` | [IUpdateShipmentStatusService.cs](../../src/MiniLogistics.Application/Shipments/UpdateShipmentStatus/IUpdateShipmentStatusService.cs) | `UpdateShipmentStatusCommand command, CancellationToken cancellationToken = default` | `Result` |
| `IUpdateShopProfileService.UpdateAsync` | [IUpdateShopProfileService.cs](../../src/MiniLogistics.Application/Shops/UpdateShopProfile/IUpdateShopProfileService.cs) | `UpdateShopProfileCommand command, CancellationToken cancellationToken = default` | `UpdateShopProfileResponse` |
| `IWebhookManagementService.GetDashboardAsync` | [IWebhookManagementService.cs](../../src/MiniLogistics.Application/PartnerApi/IWebhookManagementService.cs) | `Guid currentUserId, CancellationToken cancellationToken = default` | `PartnerIntegrationDashboardResponse` |
| `IWebhookManagementService.RetryOutboxMessageAsync` | [IWebhookManagementService.cs](../../src/MiniLogistics.Application/PartnerApi/IWebhookManagementService.cs) | `RetryPartnerOutboxMessageCommand command, CancellationToken cancellationToken = default` | `Result` |
| `IWebhookManagementService.RetryWebhookDeliveryAsync` | [IWebhookManagementService.cs](../../src/MiniLogistics.Application/PartnerApi/IWebhookManagementService.cs) | `RetryPartnerWebhookDeliveryCommand command, CancellationToken cancellationToken = default` | `Result` |
| `IWebhookManagementService.TestWebhookAsync` | [IWebhookManagementService.cs](../../src/MiniLogistics.Application/PartnerApi/IWebhookManagementService.cs) | `TestPartnerWebhookCommand command, CancellationToken cancellationToken = default` | `PartnerWebhookTestResponse` |
| `IWebhookManagementService.UpsertWebhookEndpointAsync` | [IWebhookManagementService.cs](../../src/MiniLogistics.Application/PartnerApi/IWebhookManagementService.cs) | `UpsertPartnerWebhookEndpointCommand command, CancellationToken cancellationToken = default` | `PartnerWebhookEndpointResponse` |
| `VietnamAdministrativeDivisionService.GetProvincesAsync` | [VietnamAdministrativeDivisionService.cs](../../src/MiniLogistics.Web/Services/VietnamAdministrativeDivisionService.cs) | `CancellationToken` | `IReadOnlyList<ProvinceOption>` |

## Khác biệt đã xác minh và giới hạn parity

- Proof-of-delivery có Domain/Application service và tests, nhưng không có lời gọi SubmitDeliveryProofService hoặc form proof trong routed Operations/Shipper hiện tại; ShipmentDetailResponse cũng không chứa proof/assignment DTO. Không đánh dấu feature proof UI là parity đã có. Giữ backend capability/tests; bổ sung UI/API proof là mở rộng riêng, không chặn migration tương đương UI hiện tại.
- Webhook portal hiện upsert cấu hình với secret nhập vào, test/retry và audit; IWebhookManagementService không có endpoint reveal/rotate secret đã lưu. Không tự thêm lifecycle reveal/rotate webhook secret chỉ vì plan mô tả chung secret lifecycle. API client key create/rotate vẫn là action hiện có.
- Các read Operations/pending/settlement candidates, active-shippers và hub lookup không nhận actor trong signature hiện tại. Operation page gate không đủ cho HTTP exposure: task adapter phải bổ sung authorization ở use case, giữ internal calls cần thiết; không mở API chỉ gọi thẳng phương thức chưa có guard.
- GetShopProfile đọc yêu cầu ViewShipments, không phải ManageShopProfile; chỉ mutation cần ManageShopProfile. Notification read/mark-read yêu cầu ViewShipments; preferences cần ManageNotifications. Staff management là owner-only, không có flag ManageStaff và không tự cấp Admin bypass.
- Toggle banner hiện là toggle command: browser giữ POST toggle riêng, không mô tả thành PUT set-state và không retry tự động. Các API active của user/Shop/client dùng desired state theo command hiện có.
- UI URL query chỉ bind các field nêu ở từng page. Search/filter/page khác hiện ở component state; React URL serialization có thể bổ sung các field đó nhưng phải giữ query cũ, defaults và permission. Unknown status URL được normalize như UI hiện tại trước khi gọi API; browser API raw invalid enum trả 400.

## Field schemas từ records hiện có

Các field dưới đây là nguồn C# được tham chiếu để bind request/response, **không phải** field browser đã phát hành. Quy tắc bỏ actor/path, decimal string và projection permission áp dụng trước khi sinh browser OpenAPI. Giữ defaults/nullability của record. Bảng phản ánh nested DTO để người triển khai không phải đoán shape từ tên service.

| Type | Source | Fields C# hiện tại (trước browser projection) |
| --- | --- | --- |
| `AdminAuditLogQuery` | [AdminAuditLogQuery.cs](../../src/MiniLogistics.Application/AdminAuditing/AdminAuditLogQuery.cs) | `Guid RequestedByUserId, Guid? ActorUserId = null, string? Action = null, string? TargetType = null, DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null, int Limit = 200` |
| `AdminAuditLogResponse` | [AdminAuditLogResponse.cs](../../src/MiniLogistics.Application/AdminAuditing/AdminAuditLogResponse.cs) | `Guid AuditLogId, Guid ActorUserId, string ActorRole, string Action, string TargetType, Guid TargetId, string? OldValueJson, string? NewValueJson, string? Reason, string? IpAddress, string? UserAgent, DateTimeOffset CreatedAtUtc` |
| `AdminCodDailySummary` | [AdminCodReportResponse.cs](../../src/MiniLogistics.Application/AdminCod/AdminCodReportResponse.cs) | `DateOnly Day, int ShipmentCount, decimal PendingAmount, decimal CollectedAmount, decimal SettledAmount` |
| `AdminCodGroupSummary` | [AdminCodReportResponse.cs](../../src/MiniLogistics.Application/AdminCod/AdminCodReportResponse.cs) | `string GroupKey, int ShipmentCount, decimal PendingAmount, decimal CollectedAmount, decimal SettledAmount, decimal CollectedRate` |
| `AdminCodReportQuery` | [AdminCodReportQuery.cs](../../src/MiniLogistics.Application/AdminCod/AdminCodReportQuery.cs) | `Guid RequestedByUserId, CodStatus? Status = null, Guid? ShipperId = null, string? Province = null, DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null, decimal? MinAmount = null, decimal? MaxAmount = null` |
| `AdminCodReportResponse` | [AdminCodReportResponse.cs](../../src/MiniLogistics.Application/AdminCod/AdminCodReportResponse.cs) | `AdminCodSummary Summary, IReadOnlyList<AdminCodTransactionResponse> Items, IReadOnlyList<AdminCodGroupSummary> ByShipper, IReadOnlyList<AdminCodGroupSummary> ByProvince, IReadOnlyList<AdminCodDailySummary> ByDay` |
| `AdminCodSummary` | [AdminCodReportResponse.cs](../../src/MiniLogistics.Application/AdminCod/AdminCodReportResponse.cs) | `int PendingCollectionCount, decimal PendingCollectionAmount, int CollectedAwaitingSettlementCount, decimal CollectedAwaitingSettlementAmount, int SettledCount, decimal SettledAmount, string Currency` |
| `AdminCodTransactionResponse` | [AdminCodReportResponse.cs](../../src/MiniLogistics.Application/AdminCod/AdminCodReportResponse.cs) | `Guid ShipmentId, string TrackingCode, string ReceiverName, string ReceiverPhone, string Province, Guid? ShipperId, string? ShipperName, decimal Amount, string Currency, CodStatus CodStatus, ShipmentStatus ShipmentStatus, DateTimeOffset CreatedAtUtc, DateTimeOffset? CollectedAtUtc, Guid? CollectedByUserId, DateTimeOffset? SettledAtUtc, Guid? SettledByUserId` |
| `AdminDashboardQuery` | [AdminDashboardQuery.cs](../../src/MiniLogistics.Application/AdminDashboard/AdminDashboardQuery.cs) | `Guid RequestedByUserId, DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null, string? Province = null` |
| `AdminDashboardResponse` | [AdminDashboardResponse.cs](../../src/MiniLogistics.Application/AdminDashboard/AdminDashboardResponse.cs) | `DateTimeOffset FromUtc, DateTimeOffset ToUtc, string? Province, ShipmentOverviewMetrics Shipments, ShipperOverviewMetrics Shippers, CodOverviewMetrics Cod, ShopOverviewMetrics Shops, WebhookOverviewMetrics Webhooks` |
| `AdminHubQuery` | [AdminHubQuery.cs](../../src/MiniLogistics.Application/AdminHubs/GetAdminHubs/AdminHubQuery.cs) | `Guid RequestedByUserId, string? SearchText = null, bool? IsActive = null, string? Province = null, bool? IsRegionalSortingHub = null` |
| `AdminHubResponse` | [AdminHubResponse.cs](../../src/MiniLogistics.Application/AdminHubs/AdminHubResponse.cs) | `Guid HubId, string Code, string Name, string Province, string? Ward, string? AddressLine, string Country, bool IsRegionalSortingHub, bool IsActive, int ActiveWorkingAreaCount, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc` |
| `AdminSystemConfigurationResponse` | [AdminSystemConfigurationResponses.cs](../../src/MiniLogistics.Application/AdminSystemConfiguration/AdminSystemConfigurationResponses.cs) | `IReadOnlyList<RouteRegionConfigResponse> RouteRegions, IReadOnlyList<FeeRuleConfigResponse> FeeRules` |
| `AssignShipperCommand` | [AssignShipperCommand.cs](../../src/MiniLogistics.Application/Shipments/AssignShipperToShipment/AssignShipperCommand.cs) | `Guid ShipmentId, Guid ShipperId, Guid AssignedByUserId, string? Note` |
| `AutoAssignShipmentResult` | [AutoAssignShipmentResult.cs](../../src/MiniLogistics.Application/Shipments/AutoAssignShipment/AutoAssignShipmentResult.cs) | `Guid ShipmentId, string TrackingCode, ShipmentStatus ShipmentStatus, AutoAssignShipmentStatus Status, Guid? ShipperId, string Reason` |
| `BannerDto` | [IBannerService.cs](../../src/MiniLogistics.Application/Banners/IBannerService.cs) | `Guid Id, string Title, string ImageUrl, string? LinkUrl, bool IsActive, int SortOrder, DateTimeOffset CreatedAtUtc` |
| `BulkRetryAutoAssignmentCommand` | [BulkRetryAutoAssignmentCommand.cs](../../src/MiniLogistics.Application/Shipments/BulkRetryAutoAssignment/BulkRetryAutoAssignmentCommand.cs) | `Guid RequestedByUserId, IReadOnlyList<Guid> ShipmentIds` |
| `BulkRetryAutoAssignmentItem` | [BulkRetryAutoAssignmentResult.cs](../../src/MiniLogistics.Application/Shipments/BulkRetryAutoAssignment/BulkRetryAutoAssignmentResult.cs) | `Guid ShipmentId, string TrackingCode, string Status, string Result, string Reason` |
| `BulkRetryAutoAssignmentResult` | [BulkRetryAutoAssignmentResult.cs](../../src/MiniLogistics.Application/Shipments/BulkRetryAutoAssignment/BulkRetryAutoAssignmentResult.cs) | `int RequestedCount, int RetriedCount, int AssignedCount, int SkippedCount, IReadOnlyList<BulkRetryAutoAssignmentItem> Items` |
| `CancelShipmentAssignmentCommand` | [CancelShipmentAssignmentCommand.cs](../../src/MiniLogistics.Application/Shipments/CancelShipmentAssignment/CancelShipmentAssignmentCommand.cs) | `Guid ShipmentId, Guid CancelledByUserId, string Reason` |
| `CancelShipmentCommand` | [CancelShipmentCommand.cs](../../src/MiniLogistics.Application/Shipments/CancelShipmentForCurrentShop/CancelShipmentCommand.cs) | `Guid OwnerUserId, Guid ShipmentId, string Reason, Guid? ShopId = null` |
| `CodOverviewMetrics` | [AdminDashboardResponse.cs](../../src/MiniLogistics.Application/AdminDashboard/AdminDashboardResponse.cs) | `int PendingCollectionCount, decimal PendingCollectionAmount, int CollectedAwaitingSettlementCount, decimal CollectedAwaitingSettlementAmount, int SettledCount, decimal SettledAmount, string Currency` |
| `ConfirmShipmentImportCommand` | [ShipmentImportCommands.cs](../../src/MiniLogistics.Application/Shipments/ImportShipments/ShipmentImportCommands.cs) | `Guid CurrentUserId, Guid? ShopId, IReadOnlyList<ShipmentImportRowDraft> Rows` |
| `CreateAdditionalShopCommand` | [CreateAdditionalShopCommand.cs](../../src/MiniLogistics.Application/Shops/CreateAdditionalShop/CreateAdditionalShopCommand.cs) | `Guid CurrentUserId, string Name, string PhoneNumber, string AddressLine, string Ward, string Province, string Country = "Vietnam"` |
| `CreateAdditionalShopResponse` | [CreateAdditionalShopResponse.cs](../../src/MiniLogistics.Application/Shops/CreateAdditionalShop/CreateAdditionalShopResponse.cs) | `Guid ShopId, string Name, bool IsActive` |
| `CreateDraftShipmentCommand` | [DraftShipmentCommands.cs](../../src/MiniLogistics.Application/Shipments/DraftShipments/DraftShipmentCommands.cs) | `Guid UserId, string SenderName, string SenderPhone, string ReceiverName, string ReceiverPhone, ShipmentAddressDto PickupAddress, ShipmentAddressDto DeliveryAddress, decimal WeightKg, decimal LengthCm, decimal WidthCm, decimal HeightCm, decimal GoodsValueAmount, decimal CodAmount, string Currency = "VND", string? Note = null, Guid? ShopId = null` |
| `CreateFeeRuleVersionCommand` | [AdminSystemConfigurationCommands.cs](../../src/MiniLogistics.Application/AdminSystemConfiguration/AdminSystemConfigurationCommands.cs) | `Guid RequestedByUserId, RouteType RouteType, decimal BaseWeightKg, decimal BaseFeeAmount, decimal ExtraWeightStepKg, decimal ExtraStepFeeAmount, decimal? MinimumWeightKg, decimal? MaximumWeightKg, decimal InsuranceFreeThreshold, decimal InsuranceMaximumValue, decimal InsuranceRate, decimal ReturnFeeRate, string? Reason = null` |
| `CreateHubCommand` | [CreateHubCommand.cs](../../src/MiniLogistics.Application/AdminHubs/CreateHub/CreateHubCommand.cs) | `Guid RequestedByUserId, string Code, string Name, string Province, string? Ward, string? AddressLine, string Country, bool IsRegionalSortingHub` |
| `CreateInternalUserCommand` | [CreateInternalUserCommand.cs](../../src/MiniLogistics.Application/AdminUsers/CreateInternalUser/CreateInternalUserCommand.cs) | `Guid RequestedByUserId, string FullName, string Email, string PhoneNumber, string Password, string Role` |
| `CreateInternalUserResponse` | [CreateInternalUserResponse.cs](../../src/MiniLogistics.Application/AdminUsers/CreateInternalUser/CreateInternalUserResponse.cs) | `Guid UserId, string FullName, string Email, string Role` |
| `CreatePartnerApiClientCommand` | [PartnerIntegrationCommands.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationCommands.cs) | `Guid CurrentUserId, Guid ShopId, string Name, PartnerApiScope Scopes = PartnerApiScope.All, IReadOnlyList<string>? AllowedIpAddresses = null, DateTimeOffset? ExpiresAtUtc = null` |
| `CreateShipmentCommand` | [CreateShipmentCommand.cs](../../src/MiniLogistics.Application/Shipments/CreateShipment/CreateShipmentCommand.cs) | `Guid CreatedByUserId, string SenderName, string SenderPhone, string ReceiverName, string ReceiverPhone, ShipmentAddressDto PickupAddress, ShipmentAddressDto DeliveryAddress, decimal WeightKg, decimal LengthCm, decimal WidthCm, decimal HeightCm, decimal GoodsValueAmount, decimal CodAmount, string Currency = "VND", string? Note = null, Guid? ShopId = null` |
| `CreateShipmentResponse` | [CreateShipmentResponse.cs](../../src/MiniLogistics.Application/Shipments/CreateShipment/CreateShipmentResponse.cs) | `Guid ShipmentId, string TrackingCode, decimal ActualWeightKg, decimal VolumetricWeightKg, decimal ChargeableWeightKg, decimal BaseFeeAmount, decimal ExtraWeightFeeAmount, decimal InsuranceFeeAmount, decimal ReturnFeeAmount, decimal ShippingFeeAmount, string Currency, ShipmentStatus Status` |
| `CreateShopStaffCommand` | [ShopStaffCommands.cs](../../src/MiniLogistics.Application/Shops/Staff/ShopStaffCommands.cs) | `Guid CurrentUserId, Guid ShopId, string FullName, string Email, string PhoneNumber, string Password, ShopStaffRole Role, ShopPermission Permissions` |
| `DraftShipmentResponse` | [DraftShipmentResponse.cs](../../src/MiniLogistics.Application/Shipments/DraftShipments/DraftShipmentResponse.cs) | `Guid ShipmentId, string TrackingCode, decimal WeightKg, decimal VolumetricWeightKg, decimal ChargeableWeightKg, decimal BaseFeeAmount, decimal ExtraWeightFeeAmount, decimal InsuranceFeeAmount, decimal ReturnFeeAmount, decimal ShippingFeeAmount, string Currency, ShipmentStatus Status, decimal? PreviousShippingFeeAmount = null` |
| `FeeRuleConfigResponse` | [AdminSystemConfigurationResponses.cs](../../src/MiniLogistics.Application/AdminSystemConfiguration/AdminSystemConfigurationResponses.cs) | `Guid FeeRuleId, RouteType RouteType, int Version, decimal BaseWeightKg, decimal BaseFeeAmount, decimal ExtraWeightStepKg, decimal ExtraStepFeeAmount, decimal? MinimumWeightKg, decimal? MaximumWeightKg, decimal InsuranceFreeThreshold, decimal InsuranceMaximumValue, decimal InsuranceRate, decimal ReturnFeeRate, bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc` |
| `GetActiveShipperResponse` | [GetActiveShipperResponse.cs](../../src/MiniLogistics.Application/Shippers/GetActiveShippers/GetActiveShipperResponse.cs) | `Guid UserId, string FullName, string Email, string? PhoneNumber, bool IsAvailableForAssignment, int MaxActiveShipments, IReadOnlyList<ShipperWorkingAreaResponse> WorkingAreas` |
| `GetAdminShopResponse` | [GetAdminShopResponse.cs](../../src/MiniLogistics.Application/Shops/GetAdminShops/GetAdminShopResponse.cs) | `Guid ShopId, Guid OwnerUserId, string OwnerFullName, string OwnerEmail, string? OwnerPhoneNumber, string Name, string PhoneNumber, string AddressLine, string Ward, string Province, string Country, bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc` |
| `GetAdminUserResponse` | [GetAdminUserResponse.cs](../../src/MiniLogistics.Application/AdminUsers/GetAdminUsers/GetAdminUserResponse.cs) | `Guid UserId, string FullName, string Email, string? PhoneNumber, bool IsActive, bool IsAvailableForAssignment, int MaxActiveShipments, IReadOnlyList<string> Roles, DateTimeOffset CreatedAtUtc, IReadOnlyList<ShipperWorkingAreaResponse> WorkingAreas` |
| `GetAdminUsersQuery` | [GetAdminUsersQuery.cs](../../src/MiniLogistics.Application/AdminUsers/GetAdminUsers/GetAdminUsersQuery.cs) | `Guid RequestedByUserId, string? SearchText = null, string? Role = null, bool? IsActive = null, int PageNumber = 1, int PageSize = 25` |
| `GetAssignedShipmentForShipperResponse` | [GetAssignedShipmentForShipperResponse.cs](../../src/MiniLogistics.Application/Shipments/GetAssignedShipmentsForShipper/GetAssignedShipmentForShipperResponse.cs) | `Guid ShipmentId, string TrackingCode, string SenderName, string SenderPhone, string ReceiverName, string ReceiverPhone, ShipmentAddressResponse PickupAddress, ShipmentAddressResponse DeliveryAddress, decimal CodAmount, CodStatus CodStatus, decimal ShippingFeeAmount, string Currency, ShipmentStatus Status, DateTimeOffset CreatedAtUtc, IReadOnlyList<ShipmentStatusHistoryResponse> TrackingHistory` |
| `GetAssignedShipmentsForShipperQuery` | [GetAssignedShipmentsForShipperQuery.cs](../../src/MiniLogistics.Application/Shipments/GetAssignedShipmentsForShipper/GetAssignedShipmentsForShipperQuery.cs) | `string? SearchText = null, ShipperWorkspaceStage? Stage = null, CodStatus? CodStatus = null, int PageNumber = 1, int PageSize = 20` |
| `GetCodSettlementCandidateResponse` | [GetCodSettlementCandidateResponse.cs](../../src/MiniLogistics.Application/CashOnDelivery/GetCodSettlementCandidates/GetCodSettlementCandidateResponse.cs) | `Guid ShipmentId, string TrackingCode, string ReceiverName, string ReceiverPhone, decimal CodAmount, string Currency, CodStatus CodStatus, DateTimeOffset? CollectedAtUtc, Guid? CollectedByUserId, string? CollectedByName, string? CollectedByEmail` |
| `GetOperationsShipmentResponse` | [GetOperationsShipmentResponse.cs](../../src/MiniLogistics.Application/Shipments/GetOperationsShipments/GetOperationsShipmentResponse.cs) | `Guid ShipmentId, string TrackingCode, string ReceiverName, string ReceiverPhone, ShipmentAddressResponse PickupAddress, ShipmentAddressResponse DeliveryAddress, decimal CodAmount, CodStatus CodStatus, decimal ShippingFeeAmount, string Currency, ShipmentStatus Status, DateTimeOffset CreatedAtUtc, Guid? ActiveShipperId, string? ActiveShipperName, string? ActiveShipperPhone, IReadOnlyList<ShipmentStatusHistoryResponse> TrackingHistory, bool IsSlaOverdue = false` |
| `GetOperationsShipmentsQuery` | [GetOperationsShipmentsQuery.cs](../../src/MiniLogistics.Application/Shipments/GetOperationsShipments/GetOperationsShipmentsQuery.cs) | `string? SearchText = null, ShipmentStatus? Status = null, CodStatus? CodStatus = null, Guid? ShipperId = null, string? Province = null, DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null, decimal? MinCodAmount = null, decimal? MaxCodAmount = null, bool SlaOnly = false, int PageNumber = 1, int PageSize = 25` |
| `GetPendingPickupShipmentResponse` | [GetPendingPickupShipmentResponse.cs](../../src/MiniLogistics.Application/Shipments/GetPendingPickupShipments/GetPendingPickupShipmentResponse.cs) | `Guid ShipmentId, string TrackingCode, string ReceiverName, string PickupProvince, string DeliveryProvince, decimal CodAmount, decimal ShippingFeeAmount, string Currency, DateTimeOffset CreatedAtUtc, bool IsSlaOverdue = false` |
| `GetPendingPickupShipmentsQuery` | [GetPendingPickupShipmentsQuery.cs](../../src/MiniLogistics.Application/Shipments/GetPendingPickupShipments/GetPendingPickupShipmentsQuery.cs) | `string? SearchText = null, string? Province = null, DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null, decimal? MinCodAmount = null, decimal? MaxCodAmount = null, bool SlaOnly = false, int PageNumber = 1, int PageSize = 25` |
| `GetShipmentsForCurrentShopQuery` | [GetShipmentsForCurrentShopQuery.cs](../../src/MiniLogistics.Application/Shipments/GetShipmentsForCurrentShop/GetShipmentsForCurrentShopQuery.cs) | `Guid OwnerUserId, Guid? ShopId = null, ShipmentStatus? StatusFilter = null, string? TrackingCodeSearch = null, string? ReceiverNameSearch = null, string? ReceiverPhoneSearch = null, DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null, decimal? MinCodAmount = null, decimal? MaxCodAmount = null, ShopShipmentSortBy SortBy = ShopShipmentSortBy.CreatedAt, SortDirection SortDirection = SortDirection.Descending, int PageNumber = 1, int PageSize = 25` |
| `GetShopCodReportQuery` | [GetShopCodReportQuery.cs](../../src/MiniLogistics.Application/Shops/Reports/GetShopCodReportQuery.cs) | `Guid OwnerUserId, Guid? ShopId = null, DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null` |
| `GetShopContextResponse` | [GetShopContextResponse.cs](../../src/MiniLogistics.Application/Shops/GetShopContext/GetShopContextResponse.cs) | `Guid SelectedShopId, IReadOnlyList<ShopContextItemResponse> Shops` |
| `GetShopProfileResponse` | [GetShopProfileResponse.cs](../../src/MiniLogistics.Application/Shops/GetShopProfile/GetShopProfileResponse.cs) | `Guid ShopId, string Name, string PhoneNumber, string AddressLine, string Ward, string Province, string Country, bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc` |
| `GpsCoordinateDto` | [GpsCoordinateDto.cs](../../src/MiniLogistics.Application/Shipments/GpsCoordinateDto.cs) | `decimal Latitude, decimal Longitude, decimal? AccuracyMeters = null, DateTimeOffset? CapturedAtUtc = null` |
| `LogisticsHubResponse` | [LogisticsHubResponse.cs](../../src/MiniLogistics.Application/Shippers/LogisticsHubResponse.cs) | `Guid HubId, string Code, string Name, string Province, string? Ward, string? AddressLine, string Country, bool IsRegionalSortingHub, bool IsActive` |
| `MarkCodCollectedCommand` | [MarkCodCollectedCommand.cs](../../src/MiniLogistics.Application/CashOnDelivery/MarkCodCollected/MarkCodCollectedCommand.cs) | `Guid ShipmentId, Guid CollectedByUserId, decimal? CollectedAmount = null, string? Note = null` |
| `MarkCodSettledCommand` | [MarkCodSettledCommand.cs](../../src/MiniLogistics.Application/CashOnDelivery/MarkCodSettled/MarkCodSettledCommand.cs) | `Guid ShipmentId, Guid SettledByUserId, string? Note = null` |
| `PartnerApiClientResponse` | [PartnerIntegrationResponses.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationResponses.cs) | `Guid ApiClientId, Guid ShopId, string ShopName, string Name, string ApiKeyPrefix, bool IsActive, DateTimeOffset? LastUsedAtUtc, DateTimeOffset CreatedAtUtc, PartnerWebhookEndpointResponse? WebhookEndpoint, IReadOnlyList<PartnerWebhookDeliveryResponse> RecentDeliveries, PartnerWebhookMetricsResponse WebhookMetrics, IReadOnlyList<PartnerApiCredentialAuditResponse> RecentCredentialAudits, PartnerApiScope Scopes = PartnerApiScope.All, IReadOnlyList<string>? AllowedIpAddresses = null, DateTimeOffset? ExpiresAtUtc = null, PartnerApiUsageMetricsResponse? Usage = null` |
| `PartnerApiClientSecretResponse` | [PartnerIntegrationResponses.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationResponses.cs) | `Guid ApiClientId, string ApiKey, string ApiKeyPrefix` |
| `PartnerApiCredentialAuditResponse` | [PartnerIntegrationResponses.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationResponses.cs) | `Guid AuditId, Guid ActorUserId, string Action, bool IsSuccess, string? ErrorCode, DateTimeOffset OccurredAtUtc` |
| `PartnerApiFailedRequestResponse` | [PartnerIntegrationResponses.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationResponses.cs) | `string Method, string Path, int StatusCode, string? ErrorCode, string? ErrorMessage, DateTimeOffset CreatedAtUtc` |
| `PartnerApiUsageBucketResponse` | [PartnerIntegrationResponses.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationResponses.cs) | `DateTimeOffset BucketUtc, int RequestCount, int SuccessCount, int ErrorCount` |
| `PartnerApiUsageMetricsResponse` | [PartnerIntegrationResponses.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationResponses.cs) | `int TotalRequests, int SuccessfulRequests, int FailedRequests, IReadOnlyList<PartnerApiUsageBucketResponse> Daily, IReadOnlyList<PartnerApiUsageBucketResponse> Hourly, IReadOnlyList<PartnerApiFailedRequestResponse> LatestFailedRequests` |
| `PartnerIntegrationDashboardResponse` | [PartnerIntegrationResponses.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationResponses.cs) | `IReadOnlyList<PartnerIntegrationShopResponse> Shops, IReadOnlyList<PartnerApiClientResponse> ApiClients, bool GranularPermissionEnabled = false, IReadOnlyList<PartnerOutboxFailureResponse>? OutboxFailures = null` |
| `PartnerIntegrationShopResponse` | [PartnerIntegrationResponses.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationResponses.cs) | `Guid ShopId, string ShopName, bool IsActive` |
| `PartnerOutboxFailureResponse` | [PartnerIntegrationResponses.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationResponses.cs) | `Guid OutboxMessageId, Guid ApiClientId, string EventType, Guid AggregateId, string Status, int RetryCount, DateTimeOffset? NextAttemptAtUtc, string Error, DateTimeOffset CreatedAtUtc` |
| `PartnerWebhookDeliveryResponse` | [PartnerIntegrationResponses.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationResponses.cs) | `Guid WebhookDeliveryId, string EventType, WebhookDeliveryStatus Status, int RetryCount, DateTimeOffset? NextAttemptAtUtc, DateTimeOffset? LastAttemptAtUtc, int? LastResponseStatusCode, long? LastDurationMs, string? LastError, DateTimeOffset CreatedAtUtc` |
| `PartnerWebhookEndpointResponse` | [PartnerIntegrationResponses.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationResponses.cs) | `Guid WebhookEndpointId, string Url, bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc` |
| `PartnerWebhookMetricsResponse` | [PartnerIntegrationResponses.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationResponses.cs) | `int TotalDeliveries, int SucceededDeliveries, int FailedDeliveries, int PendingRetryDeliveries, decimal SuccessRate, decimal? AverageLatencyMs` |
| `PartnerWebhookTestResponse` | [PartnerIntegrationResponses.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationResponses.cs) | `Guid WebhookDeliveryId, string EventType` |
| `PreviewShipmentImportCommand` | [ShipmentImportCommands.cs](../../src/MiniLogistics.Application/Shipments/ImportShipments/ShipmentImportCommands.cs) | `Guid CurrentUserId, Guid? ShopId, string CsvContent` |
| `PublicTrackingAddressResponse` | [PublicTrackingResponse.cs](../../src/MiniLogistics.Application/Shipments/GetPublicTracking/PublicTrackingResponse.cs) | `string Street, string Ward, string Province, string Country, string FullAddress` |
| `PublicTrackingResponse` | [PublicTrackingResponse.cs](../../src/MiniLogistics.Application/Shipments/GetPublicTracking/PublicTrackingResponse.cs) | `string TrackingCode, PublicTrackingAccessLevel AccessLevel, ShipmentStatus CurrentStatus, string PickupProvince, string DeliveryProvince, DateTimeOffset CreatedAtUtc, DateTimeOffset LastUpdatedAtUtc, string? SenderName, string? SenderPhone, string? ReceiverName, string? ReceiverPhone, PublicTrackingAddressResponse? PickupAddress, PublicTrackingAddressResponse? DeliveryAddress, IReadOnlyList<PublicTrackingTimelineItemResponse> Timeline` |
| `PublicTrackingTimelineItemResponse` | [PublicTrackingResponse.cs](../../src/MiniLogistics.Application/Shipments/GetPublicTracking/PublicTrackingResponse.cs) | `ShipmentStatus Status, string MessageCode, string Message, string Locale, DateTimeOffset ChangedAtUtc` |
| `ReassignShipmentCommand` | [ReassignShipmentCommand.cs](../../src/MiniLogistics.Application/Shipments/ReassignShipment/ReassignShipmentCommand.cs) | `Guid ShipmentId, Guid NewShipperId, Guid ReassignedByUserId, string Reason` |
| `RegisterShopCommand` | [RegisterShopCommand.cs](../../src/MiniLogistics.Application/Shops/RegisterShop/RegisterShopCommand.cs) | `string FullName, string Email, string Password, string ShopName, string PhoneNumber, string AddressLine, string Ward, string Province, string Country = "Vietnam"` |
| `ResetUserPasswordCommand` | [ResetUserPasswordCommand.cs](../../src/MiniLogistics.Application/AdminUsers/ResetUserPassword/ResetUserPasswordCommand.cs) | `Guid RequestedByUserId, Guid TargetUserId, string NewPassword, string? Reason = null` |
| `RetryPartnerOutboxMessageCommand` | [PartnerIntegrationCommands.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationCommands.cs) | `Guid CurrentUserId, Guid OutboxMessageId` |
| `RetryPartnerWebhookDeliveryCommand` | [PartnerIntegrationCommands.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationCommands.cs) | `Guid CurrentUserId, Guid WebhookDeliveryId` |
| `RotatePartnerApiClientKeyCommand` | [PartnerIntegrationCommands.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationCommands.cs) | `Guid CurrentUserId, Guid ApiClientId` |
| `RouteClassificationResult` | [IRouteClassificationService.cs](../../src/MiniLogistics.Application/Routing/IRouteClassificationService.cs) | `RouteType RouteType, string PickupRegion, string DeliveryRegion, Guid? PickupRouteRegionConfigId = null, int? PickupRouteRegionConfigVersion = null, Guid? DeliveryRouteRegionConfigId = null, int? DeliveryRouteRegionConfigVersion = null` |
| `RouteRegionConfigResponse` | [AdminSystemConfigurationResponses.cs](../../src/MiniLogistics.Application/AdminSystemConfiguration/AdminSystemConfigurationResponses.cs) | `Guid ConfigId, string Province, string Region, int Version, bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc` |
| `SetHubActiveStatusCommand` | [SetHubActiveStatusCommand.cs](../../src/MiniLogistics.Application/AdminHubs/SetHubActiveStatus/SetHubActiveStatusCommand.cs) | `Guid RequestedByUserId, Guid HubId, bool IsActive, string? Reason = null` |
| `SetPartnerApiClientActiveStatusCommand` | [PartnerIntegrationCommands.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationCommands.cs) | `Guid CurrentUserId, Guid ApiClientId, bool IsActive` |
| `SetShipperAvailabilityCommand` | [SetShipperAvailabilityCommand.cs](../../src/MiniLogistics.Application/Shippers/SetShipperAvailability/SetShipperAvailabilityCommand.cs) | `Guid ShipperUserId, bool IsAvailableForAssignment` |
| `SetShipperCapacityCommand` | [SetShipperCapacityCommand.cs](../../src/MiniLogistics.Application/AdminUsers/SetShipperCapacity/SetShipperCapacityCommand.cs) | `Guid RequestedByUserId, Guid ShipperId, bool IsAvailableForAssignment, int MaxActiveShipments` |
| `SetShipperWorkingAreaItem` | [SetShipperWorkingAreasCommand.cs](../../src/MiniLogistics.Application/Shippers/SetShipperWorkingAreas/SetShipperWorkingAreasCommand.cs) | `Guid HubId, string? Ward = null, string? ZoneCode = null` |
| `SetShipperWorkingAreasCommand` | [SetShipperWorkingAreasCommand.cs](../../src/MiniLogistics.Application/Shippers/SetShipperWorkingAreas/SetShipperWorkingAreasCommand.cs) | `Guid RequestedByUserId, Guid ShipperId, IReadOnlyList<SetShipperWorkingAreaItem> Areas` |
| `SetShopActiveStatusCommand` | [SetShopActiveStatusCommand.cs](../../src/MiniLogistics.Application/Shops/SetShopActiveStatus/SetShopActiveStatusCommand.cs) | `Guid RequestedByUserId, Guid ShopId, bool IsActive` |
| `SetUserActiveStatusCommand` | [SetUserActiveStatusCommand.cs](../../src/MiniLogistics.Application/AdminUsers/SetUserActiveStatus/SetUserActiveStatusCommand.cs) | `Guid RequestedByUserId, Guid TargetUserId, bool IsActive, string? Reason = null` |
| `ShipmentAddressDto` | [ShipmentAddressDto.cs](../../src/MiniLogistics.Application/Shipments/CreateShipment/ShipmentAddressDto.cs) | `string Street, string Ward, string Province, string Country = "Vietnam"` |
| `ShipmentAddressResponse` | [ShipmentAddressResponse.cs](../../src/MiniLogistics.Application/Shipments/ShipmentAddressResponse.cs) | `string Street, string Ward, string Province, string Country, string FullAddress` |
| `ShipmentDetailResponse` | [ShipmentDetailResponse.cs](../../src/MiniLogistics.Application/Shipments/GetShipmentDetailForCurrentShop/ShipmentDetailResponse.cs) | `Guid ShipmentId, string TrackingCode, string SenderName, string SenderPhone, string ReceiverName, string ReceiverPhone, ShipmentAddressResponse PickupAddress, ShipmentAddressResponse DeliveryAddress, decimal WeightKg, decimal ParcelLengthCm, decimal ParcelWidthCm, decimal ParcelHeightCm, decimal VolumetricWeightKg, decimal ChargeableWeightKg, decimal GoodsValueAmount, decimal CodAmount, decimal BaseFeeAmount, decimal ExtraWeightFeeAmount, decimal InsuranceFeeAmount, decimal ReturnFeeAmount, decimal ShippingFeeAmount, string Currency, RouteType RouteType, string? Note, ShipmentStatus Status, DateTimeOffset CreatedAtUtc, IReadOnlyList<ShipmentStatusHistoryResponse> TrackingHistory` |
| `ShipmentImportConfirmResponse` | [ShipmentImportResponses.cs](../../src/MiniLogistics.Application/Shipments/ImportShipments/ShipmentImportResponses.cs) | `int TotalRows, int CreatedRows, int FailedRows, IReadOnlyList<ShipmentImportConfirmRowResponse> Rows, Guid? BatchId = null, ShipmentImportBatchStatus? Status = null` |
| `ShipmentImportConfirmRowResponse` | [ShipmentImportResponses.cs](../../src/MiniLogistics.Application/Shipments/ImportShipments/ShipmentImportResponses.cs) | `int RowNumber, string? ClientOrderCode, bool IsCreated, Guid? ShipmentId, string? TrackingCode, IReadOnlyList<string> Errors, ShipmentImportRowStatus Status = ShipmentImportRowStatus.Pending` |
| `ShipmentImportPreviewResponse` | [ShipmentImportResponses.cs](../../src/MiniLogistics.Application/Shipments/ImportShipments/ShipmentImportResponses.cs) | `Guid ShopId, int TotalRows, int ValidRows, int InvalidRows, IReadOnlyList<ShipmentImportPreviewRowResponse> Rows` |
| `ShipmentImportPreviewRowResponse` | [ShipmentImportResponses.cs](../../src/MiniLogistics.Application/Shipments/ImportShipments/ShipmentImportResponses.cs) | `ShipmentImportRowDraft Draft, bool IsValid, IReadOnlyList<string> Errors, RouteType? RouteType, decimal? ChargeableWeightKg, decimal? ShippingFeeAmount, string Currency` |
| `ShipmentImportRowDraft` | [ShipmentImportCommands.cs](../../src/MiniLogistics.Application/Shipments/ImportShipments/ShipmentImportCommands.cs) | `int RowNumber, string? ClientOrderCode, string ReceiverName, string ReceiverPhone, string DeliveryStreet, string DeliveryWard, string DeliveryProvince, string DeliveryCountry, decimal WeightKg, decimal LengthCm, decimal WidthCm, decimal HeightCm, decimal GoodsValueAmount, decimal CodAmount, string? Note` |
| `ShipmentListItemResponse` | [ShipmentListItemResponse.cs](../../src/MiniLogistics.Application/Shipments/GetShipmentsForCurrentShop/ShipmentListItemResponse.cs) | `Guid ShipmentId, string TrackingCode, string ReceiverName, RouteType RouteType, decimal WeightKg, decimal ChargeableWeightKg, decimal CodAmount, decimal ShippingFeeAmount, string Currency, ShipmentStatus Status, DateTimeOffset CreatedAtUtc` |
| `ShipmentOverviewMetrics` | [AdminDashboardResponse.cs](../../src/MiniLogistics.Application/AdminDashboard/AdminDashboardResponse.cs) | `int Created, int PendingPickupUnassigned, int DeliveryFailed, int NonDraftTotal, decimal DeliveryFailedRate` |
| `ShipmentStatusHistoryResponse` | [ShipmentStatusHistoryResponse.cs](../../src/MiniLogistics.Application/Shipments/ShipmentStatusHistoryResponse.cs) | `ShipmentStatus Status, string Note, DateTimeOffset ChangedAtUtc, Guid ChangedByUserId, string ChangedByDisplayName, string? ChangedByEmail, bool ChangedByUserFound, FailureReasonCode? FailureReasonCode = null, decimal? Latitude = null, decimal? Longitude = null, decimal? GpsAccuracyMeters = null, DateTimeOffset? GpsCapturedAtUtc = null` |
| `ShipperCodDailySummaryResponse` | [ShipperCodDailySummaryResponse.cs](../../src/MiniLogistics.Application/CashOnDelivery/GetShipperCodDailySummary/ShipperCodDailySummaryResponse.cs) | `Guid ShipperUserId, DateOnly BusinessDate, decimal PendingCollectionAmount, int PendingCollectionCount, decimal CollectedTodayAmount, int CollectedTodayCount, string Currency` |
| `ShipperOverviewMetrics` | [AdminDashboardResponse.cs](../../src/MiniLogistics.Application/AdminDashboard/AdminDashboardResponse.cs) | `int Active, int AvailableForAssignment, int OverCapacity` |
| `ShipperWorkingAreaResponse` | [ShipperWorkingAreaResponse.cs](../../src/MiniLogistics.Application/Shippers/ShipperWorkingAreaResponse.cs) | `Guid WorkingAreaId, Guid ShipperId, Guid HubId, string HubCode, string HubName, string Province, string? Ward, string? ZoneCode, bool IsActive` |
| `ShopAuditLogQuery` | [ShopAuditLogQuery.cs](../../src/MiniLogistics.Application/Shops/Audit/ShopAuditLogQuery.cs) | `Guid CurrentUserId, Guid? ShopId = null, string? Action = null, DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null, int Limit = 200` |
| `ShopAuditLogResponse` | [ShopAuditLogResponse.cs](../../src/MiniLogistics.Application/Shops/Audit/ShopAuditLogResponse.cs) | `Guid AuditLogId, string ActorRole, string Action, string TargetType, Guid TargetId, string? OldValueJson, string? NewValueJson, string? Reason, DateTimeOffset CreatedAtUtc` |
| `ShopCodReportResponse` | [ShopCodReportResponse.cs](../../src/MiniLogistics.Application/Shops/Reports/ShopCodReportResponse.cs) | `Guid ShopId, decimal PendingCollectionAmount, decimal CollectedAmount, decimal SettledAmount, decimal DiscrepancyAmount, string Currency, IReadOnlyList<ShopCodReportRowResponse> Rows` |
| `ShopCodReportRowResponse` | [ShopCodReportResponse.cs](../../src/MiniLogistics.Application/Shops/Reports/ShopCodReportResponse.cs) | `Guid ShipmentId, string TrackingCode, ShipmentStatus ShipmentStatus, CodStatus CodStatus, decimal DeclaredAmount, decimal? CollectedAmount, decimal DiscrepancyAmount, DateTimeOffset? CollectedAtUtc, DateTimeOffset CreatedAtUtc` |
| `ShopContextItemResponse` | [GetShopContextResponse.cs](../../src/MiniLogistics.Application/Shops/GetShopContext/GetShopContextResponse.cs) | `Guid ShopId, string Name, string PhoneNumber, string AddressLine, string Ward, string Province, string Country, bool IsActive, bool IsOwner, ShopStaffRole? StaffRole, ShopPermission Permissions` |
| `ShopDashboardKpiQuery` | [ShopDashboardKpiQuery.cs](../../src/MiniLogistics.Application/Shops/Reports/ShopDashboardKpiQuery.cs) | `Guid OwnerUserId, Guid? ShopId = null, DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null` |
| `ShopDashboardKpiResponse` | [ShopDashboardKpiResponse.cs](../../src/MiniLogistics.Application/Shops/Reports/ShopDashboardKpiResponse.cs) | `Guid ShopId, int TotalShipments, int DeliveredShipments, int ReturnedShipments, int DeliveryFailedShipments, decimal DeliveredRate, decimal ReturnedRate, decimal DeliveryFailedRate, decimal TotalShippingFee, decimal PendingCodAmount, decimal CollectedCodAmount, decimal SettledCodAmount, string Currency, IReadOnlyDictionary<ShipmentStatus, int> CountByStatus` |
| `ShopNotificationPreferenceResponse` | [ShopNotificationResponses.cs](../../src/MiniLogistics.Application/Shops/Notifications/ShopNotificationResponses.cs) | `Guid ShopId, IReadOnlyList<string> EnabledEventTypes` |
| `ShopNotificationResponse` | [ShopNotificationResponses.cs](../../src/MiniLogistics.Application/Shops/Notifications/ShopNotificationResponses.cs) | `Guid NotificationId, Guid ShopId, string EventType, string Title, string Message, Guid? ShipmentId, bool IsRead, DateTimeOffset CreatedAtUtc` |
| `ShopOverviewMetrics` | [AdminDashboardResponse.cs](../../src/MiniLogistics.Application/AdminDashboard/AdminDashboardResponse.cs) | `int Active, int Inactive` |
| `ShopStaffResponse` | [ShopStaffResponses.cs](../../src/MiniLogistics.Application/Shops/Staff/ShopStaffResponses.cs) | `Guid MembershipId, Guid UserId, string FullName, string Email, string? PhoneNumber, ShopStaffRole Role, ShopPermission Permissions, bool IsActive, DateTimeOffset CreatedAtUtc` |
| `SubmitDraftShipmentCommand` | [DraftShipmentCommands.cs](../../src/MiniLogistics.Application/Shipments/DraftShipments/DraftShipmentCommands.cs) | `Guid UserId, Guid ShipmentId, Guid? ShopId = null` |
| `TestPartnerWebhookCommand` | [PartnerIntegrationCommands.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationCommands.cs) | `Guid CurrentUserId, Guid ApiClientId` |
| `UpdateHubCommand` | [UpdateHubCommand.cs](../../src/MiniLogistics.Application/AdminHubs/UpdateHub/UpdateHubCommand.cs) | `Guid RequestedByUserId, Guid HubId, string Code, string Name, string Province, string? Ward, string? AddressLine, string Country, bool IsRegionalSortingHub` |
| `UpdatePartnerApiClientSecurityCommand` | [PartnerIntegrationCommands.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationCommands.cs) | `Guid CurrentUserId, Guid ApiClientId, PartnerApiScope Scopes, IReadOnlyList<string> AllowedIpAddresses, DateTimeOffset? ExpiresAtUtc` |
| `UpdateShipmentBeforePickupCommand` | [DraftShipmentCommands.cs](../../src/MiniLogistics.Application/Shipments/DraftShipments/DraftShipmentCommands.cs) | `Guid UserId, Guid ShipmentId, string SenderName, string SenderPhone, string ReceiverName, string ReceiverPhone, ShipmentAddressDto PickupAddress, ShipmentAddressDto DeliveryAddress, decimal WeightKg, decimal LengthCm, decimal WidthCm, decimal HeightCm, decimal GoodsValueAmount, decimal CodAmount, string Currency = "VND", string? Note = null, Guid? ShopId = null` |
| `UpdateShipmentStatusCommand` | [UpdateShipmentStatusCommand.cs](../../src/MiniLogistics.Application/Shipments/UpdateShipmentStatus/UpdateShipmentStatusCommand.cs) | `Guid ShipmentId, Guid ChangedByUserId, ShipmentStatus NewStatus, string? Note, FailureReasonCode? FailureReasonCode = null, GpsCoordinateDto? GpsCoordinate = null` |
| `UpdateShopProfileCommand` | [UpdateShopProfileCommand.cs](../../src/MiniLogistics.Application/Shops/UpdateShopProfile/UpdateShopProfileCommand.cs) | `Guid CurrentUserId, string Name, string PhoneNumber, string AddressLine, string Ward, string Province, string Country = "Vietnam", Guid? ShopId = null` |
| `UpdateShopProfileResponse` | [UpdateShopProfileResponse.cs](../../src/MiniLogistics.Application/Shops/UpdateShopProfile/UpdateShopProfileResponse.cs) | `Guid ShopId, string Name, string PhoneNumber, string AddressLine, string Ward, string Province, string Country, bool IsActive, DateTimeOffset? UpdatedAtUtc` |
| `UpdateShopStaffAccessCommand` | [ShopStaffCommands.cs](../../src/MiniLogistics.Application/Shops/Staff/ShopStaffCommands.cs) | `Guid CurrentUserId, Guid ShopId, Guid StaffUserId, ShopStaffRole Role, ShopPermission Permissions, bool IsActive` |
| `UpsertPartnerWebhookEndpointCommand` | [PartnerIntegrationCommands.cs](../../src/MiniLogistics.Application/PartnerApi/PartnerIntegrationCommands.cs) | `Guid CurrentUserId, Guid ApiClientId, string Url, string SigningSecret` |
| `UpsertRouteRegionConfigCommand` | [AdminSystemConfigurationCommands.cs](../../src/MiniLogistics.Application/AdminSystemConfiguration/AdminSystemConfigurationCommands.cs) | `Guid RequestedByUserId, string Province, string Region, string? Reason = null` |
| `WebhookOverviewMetrics` | [AdminDashboardResponse.cs](../../src/MiniLogistics.Application/AdminDashboard/AdminDashboardResponse.cs) | `int Failed, int RetryPending` |
