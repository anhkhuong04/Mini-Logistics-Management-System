import type { components } from '../../shared/api/generated/browser-api';
import { requestForm, requestJson } from '../../shared/api/httpClient';
export type ImportDraft = components['schemas']['ShipmentImportRowDraft'];
export type ImportPreview = components['schemas']['ShipmentImportPreviewResponse'];
export type ImportResult = components['schemas']['ShipmentImportConfirmResponse'];
const path = (shop: string) => `/api/v1/app/shops/${encodeURIComponent(shop)}/shipment-imports`;
export const previewImport = (shop: string, file: File, signal: AbortSignal) => { const data = new FormData(); data.set('file', file); return requestForm<ImportPreview>(path(shop) + '/preview', data, { signal }); };
export const confirmImport = (shop: string, rows: ImportDraft[]) => requestJson<ImportResult>(path(shop) + '/confirm', { method: 'POST', body: { rows } });
export const getImportBatch = (shop: string, id: string, signal: AbortSignal) => requestJson<ImportResult>(path(shop) + '/' + encodeURIComponent(id), { signal });
export const importHeaders = 'clientOrderCode,receiverName,receiverPhone,deliveryStreet,deliveryWard,deliveryProvince,deliveryCountry,weightKg,lengthCm,widthCm,heightCm,goodsValueAmount,codAmount,note';
export function previewErrorsCsv(rows: ImportPreview['rows']) {
  const cell = (value: string) => `"${(/^[\s]*[=+@-]/.test(value) ? "'" + value : value).replaceAll('"', '""')}"`;
  return '\uFEFFrowNumber,clientOrderCode,errors\r\n' + rows.filter(row => row.errors.length > 0)
    .map(row => [String(row.draft.rowNumber), row.draft.clientOrderCode ?? '', row.errors.join('; ')].map(cell).join(',')).join('\r\n');
}
