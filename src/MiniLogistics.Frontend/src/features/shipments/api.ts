import type { components } from '../../shared/api/generated/browser-api';
import { requestJson } from '../../shared/api/httpClient';
import { shipmentSearch, type ShipmentFilters } from '../../shared/model/shipmentFilters';
export type ShipmentItem = components['schemas']['ShipmentListItemResponse'];
export type ShipmentDetail = components['schemas']['ShipmentDetailResponse'];
export type ShipmentPage = {
  items: ShipmentItem[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};
export const getShipments = (shop: string, filters: ShipmentFilters, signal: AbortSignal) =>
  requestJson<ShipmentPage>(
    `/api/v1/app/shops/${encodeURIComponent(shop)}/shipments?${shipmentSearch(filters)}`,
    { signal },
  );
export const getShipment = (shop: string, id: string, signal: AbortSignal) =>
  requestJson<ShipmentDetail>(
    `/api/v1/app/shops/${encodeURIComponent(shop)}/shipments/${encodeURIComponent(id)}`,
    { signal },
  );
