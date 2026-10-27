import { ApiError, requestDownload } from './httpClient';
import { shipmentSearch, type ShipmentFilters } from '../model/shipmentFilters';
export function exportShipmentsUrl(shopId: string, filters: ShipmentFilters) {
  const { pageNumber: _page, pageSize: _size, ...criteria } = filters;
  void _page;
  void _size;
  const query = shipmentSearch(criteria);
  query.set('shopId', shopId);
  return `/shop/files/shipments/export.csv?${query}`;
}
export function codExportUrl(shopId: string, range: { fromUtc?: string; toUtc?: string }) {
  const query = new URLSearchParams({ shopId });
  if (range.fromUtc) query.set('fromUtc', range.fromUtc);
  if (range.toUtc) query.set('toUtc', range.toUtc);
  return `/shop/files/cod-report.csv?${query}`;
}
export const labelUrl = (shop: string, id: string) =>
  `/shop/files/shipments/${encodeURIComponent(id)}/label.pdf?${new URLSearchParams({ shopId: shop })}`;
export const importErrorsUrl = (shop: string, id: string) =>
  `/shop/files/import-batches/${encodeURIComponent(id)}/errors.csv?${new URLSearchParams({ shopId: shop })}`;
export function saveBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  try {
    link.href = url;
    link.download = fileName;
    document.body.append(link);
    link.click();
  } finally {
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 30_000);
  }
}
export async function downloadFile(
  path: string,
  expectedMime: 'text/csv' | 'application/pdf',
  signal: AbortSignal,
) {
  const result = await requestDownload(path, signal);
  signal.throwIfAborted();
  if (result.blob.type.split(';')[0].toLowerCase() !== expectedMime)
    throw new ApiError(200, 'Browser.ProtocolError', 'Unexpected file type');
  saveBlob(result.blob, result.fileName);
}
