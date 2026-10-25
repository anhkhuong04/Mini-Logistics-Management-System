import type { components } from '../../shared/api/generated/browser-api';
import { requestJson } from '../../shared/api/httpClient';
import { shipmentSearch, type ShipmentFilters } from '../shipments/shipmentFilters';
export type DashboardKpi = components['schemas']['ShopDashboardKpiResponse'];
export const getDashboard = (shop: string, range: Pick<ShipmentFilters, 'fromUtc' | 'toUtc'>, signal: AbortSignal) =>
  requestJson<DashboardKpi>(`/api/v1/app/shops/${encodeURIComponent(shop)}/dashboard?${shipmentSearch(range)}`, { signal });
