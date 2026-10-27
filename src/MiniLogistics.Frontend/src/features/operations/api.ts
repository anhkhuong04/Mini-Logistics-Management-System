import type { components } from '../../shared/api/generated/browser-api';
import { requestJson } from '../../shared/api/httpClient';
import type { OperationsFilters } from './models';
export type OperationsShipment = components['schemas']['GetOperationsShipmentResponse'];
export type PendingShipment = components['schemas']['GetPendingPickupShipmentResponse'];
export type OperationsShipper = components['schemas']['OperationsShipperResponse'];
export type CodCandidate = components['schemas']['OperationsCodCandidateResponse'];
export type BulkResult = components['schemas']['BulkRetryAutoAssignmentResult'];
export type AutoResult = components['schemas']['AutoAssignShipmentResult'];
export type OperationsPageData<T> = {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};
export const operationsPrefix = '/api/v1/app/operations';
export function operationsSearch(filters: OperationsFilters) {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(filters))
    if (key !== 'view' && value !== undefined && value !== '') query.set(key, String(value));
  return query.toString();
}
export function readOperations<T>(resource: string, filters: OperationsFilters, signal: AbortSignal) {
  return requestJson<OperationsPageData<T>>(`${operationsPrefix}/${resource}?${operationsSearch(filters)}`, {
    signal,
  });
}
export const readShippers = (signal: AbortSignal) =>
  requestJson<OperationsShipper[]>(`${operationsPrefix}/shippers`, { signal });
export const command = <T = void>(resource: string, body: unknown, signal: AbortSignal) =>
  requestJson<T>(`${operationsPrefix}/${resource}`, { method: 'POST', body, signal });
