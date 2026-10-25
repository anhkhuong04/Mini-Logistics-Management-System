import type { ShipmentStatus } from '../shipments/shipmentFilters';
export const stages = ['Pickup', 'Transit', 'Delivery', 'CodPending'] as const;
export const codStatuses = ['NotRequired', 'PendingCollection', 'Collected', 'Settled'] as const;
export type ShipperFilters = { stage?: typeof stages[number]; codStatus?: typeof codStatuses[number]; searchText?: string; businessDate?: string; pageNumber: number; pageSize: number };
export function readShipperFilters(search: URLSearchParams): ShipperFilters {
  const get = (key: string) => { if (search.getAll(key).length > 1) throw new Error('Duplicate filter'); return search.get(key)?.trim() || undefined; };
  const stage = get('stage'); const codStatus = get('codStatus'); const searchText = get('searchText'); const businessDate = get('businessDate');
  const page = get('pageNumber') ?? '1'; const size = get('pageSize') ?? '20';
  if (stage && !stages.includes(stage as typeof stages[number]) || codStatus && !codStatuses.includes(codStatus as typeof codStatuses[number]) || searchText && searchText.length > 100
    || !/^\d+$/.test(page) || !/^\d+$/.test(size) || Number(page) < 1 || Number(size) < 1 || Number(size) > 100 || !Number.isSafeInteger(Number(page)) || (Number(page) - 1) * Number(size) > 2147483647) throw new Error('Invalid filter');
  if (businessDate && (!/^\d{4}-\d{2}-\d{2}$/.test(businessDate) || businessDate.startsWith('0000') || Number.isNaN(Date.parse(businessDate)) || new Date(`${businessDate}T00:00:00Z`).toISOString().slice(0, 10) !== businessDate)) throw new Error('Invalid date');
  return { stage: stage as ShipperFilters['stage'], codStatus: codStatus as ShipperFilters['codStatus'], searchText, businessDate, pageNumber: Number(page), pageSize: Number(size) };
}
export function shipperSearch(filters: Partial<ShipperFilters>) {
  const query = new URLSearchParams(); for (const [key, value] of Object.entries(filters)) if (value != null && value !== '') query.set(key, String(value)); return query;
}
export function nextShipperStatuses(status: string): ShipmentStatus[] {
  const next: Record<string, ShipmentStatus[]> = { Assigned: ['PickingUp', 'Cancelled'], PickingUp: ['PickedUp', 'Cancelled'], PickedUp: ['InTransit', 'Returned'],
    InTransit: ['Delivering', 'Returned'], Delivering: ['Delivered', 'DeliveryFailed', 'Returned'], DeliveryFailed: ['Delivering', 'Returned'] };
  return next[status] ?? [];
}
export const canCollectCod = (status: string, codStatus: string) => status === 'Delivered' && codStatus === 'PendingCollection';
export const failureReasons = { ReceiverUnavailable: 'Không có người nhận', WrongAddress: 'Sai địa chỉ', ReceiverRejected: 'Người nhận từ chối',
  CannotContactReceiver: 'Không liên hệ được người nhận', DamagedParcel: 'Hàng bị hỏng', PaymentIssue: 'Vấn đề thanh toán', WeatherOrAccessIssue: 'Thời tiết hoặc đường đi', Other: 'Lý do khác' } as const;
