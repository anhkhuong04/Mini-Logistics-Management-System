import { requestJson } from '../../shared/api/httpClient';
import type { ShipmentWrite, ShipmentWriteResult } from './shipmentFormMapping';
const path = (shop: string, id?: string) => `/api/v1/app/shops/${encodeURIComponent(shop)}/shipments${id ? `/${encodeURIComponent(id)}` : ''}`;
export const createShipment = (shop: string, body: ShipmentWrite, draft: boolean) => requestJson<ShipmentWriteResult>(path(shop) + (draft ? '/drafts' : ''), { method: 'POST', body });
export const updateShipment = (shop: string, id: string, body: ShipmentWrite) => requestJson<ShipmentWriteResult>(path(shop, id), { method: 'PUT', body });
export const submitShipment = (shop: string, id: string) => requestJson<ShipmentWriteResult>(path(shop, id) + '/submit', { method: 'POST' });
export const cancelShipment = (shop: string, id: string, reason: string) => requestJson<void>(path(shop, id) + '/cancel', { method: 'POST', body: { reason } });
