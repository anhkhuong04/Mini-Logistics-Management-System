import type { components } from '../../shared/api/generated/browser-api';
import { requestJson } from '../../shared/api/httpClient';
import { shipperSearch, type ShipperFilters } from './models';
export type ShipperShipment = components['schemas']['GetAssignedShipmentForShipperResponse'];
export type ShipperProfile = components['schemas']['ShipperSelfProfileResponse'];
export type WorkingArea = components['schemas']['ShipperWorkingAreaResponse'];
export type CodSummary = components['schemas']['ShipperCodDailySummaryResponse'];
export type StatusBody = components['schemas']['BrowserOperationsStatusRequest'];
export type CollectBody = components['schemas']['BrowserCodCollectRequest'];
export type ShipmentPage = { items: ShipperShipment[]; pageNumber: number; pageSize: number; totalCount: number; totalPages: number };
export const shipperPrefix = '/api/v1/app/shipper/me';
export const readWorkspace = (filters: Omit<ShipperFilters, 'businessDate'>, signal: AbortSignal) => requestJson<ShipmentPage>(`${shipperPrefix}/workspace?${shipperSearch(filters)}`, { signal });
export const readProfile = (signal: AbortSignal) => requestJson<ShipperProfile>(`${shipperPrefix}/profile`, { signal });
export const readAreas = (signal: AbortSignal) => requestJson<WorkingArea[]>(`${shipperPrefix}/working-areas`, { signal });
export const readSummary = (businessDate: string | undefined, signal: AbortSignal) => requestJson<CodSummary>(`${shipperPrefix}/cod-summary${businessDate ? `?businessDate=${encodeURIComponent(businessDate)}` : ''}`, { signal });
export type ShipperAction = { kind: 'status'; key: string; code: string; body: StatusBody } | { kind: 'collect'; key: string; code: string; body: CollectBody; currency: string } | { kind: 'availability'; key: '@availability'; available: boolean };
export function sendAction(action: ShipperAction, signal: AbortSignal) {
  const path = action.kind === 'availability' ? 'availability' : `shipments/${encodeURIComponent(action.key)}/${action.kind === 'status' ? 'status' : 'cod/collect'}`;
  const body = action.kind === 'availability' ? { isAvailableForAssignment: action.available } : action.body;
  return requestJson<void>(`${shipperPrefix}/${path}`, { method: action.kind === 'availability' ? 'PUT' : 'POST', body, signal });
}
