import { routePaths } from '../../app/router/routePaths';
export const shipmentStatuses = ['Draft', 'PendingPickup', 'Assigned', 'PickingUp', 'PickedUp', 'InTransit', 'Delivering', 'Delivered', 'DeliveryFailed', 'Returned', 'Cancelled'] as const;
export type ShipmentStatus = typeof shipmentStatuses[number];
export const sortFields = ['CreatedAt', 'TrackingCode', 'ReceiverName', 'CodAmount', 'ShippingFee'] as const;
export type ShipmentFilters = { status?: ShipmentStatus; trackingCode?: string; receiverName?: string; receiverPhone?: string;
  fromUtc?: string; toUtc?: string; minCodAmount?: string; maxCodAmount?: string;
  sortBy: typeof sortFields[number]; sortDirection: 'Ascending' | 'Descending'; pageNumber: number; pageSize: number };

function timestamp(value?: string) {
  if (!value) return undefined;
  // Legacy Razor query timestamps without a suffix were explicitly interpreted as UTC.
  const normalized = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?$/.test(value) ? `${value}Z` : value;
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$/.test(normalized)
    || Number.isNaN(Date.parse(normalized))) throw new Error('Invalid UTC timestamp');
  const day = normalized.slice(0, 10);
  if (new Date(`${day}T00:00:00Z`).toISOString().slice(0, 10) !== day) throw new Error('Invalid date');
  return normalized;
}
export function utcDateRange(from: string, to: string) {
  const fromUtc = from ? timestamp(`${from}T00:00:00Z`) : undefined;
  const toUtc = to ? timestamp(`${to}T23:59:59.9999999Z`) : undefined;
  if (fromUtc && toUtc && fromUtc > toUtc) throw new Error('Invalid date range');
  return { fromUtc, toUtc };
}
function decimal(value?: string) {
  if (value && (!/^\d+(?:\.\d+)?$/.test(value) || value.length > 60)) throw new Error('Invalid COD amount');
  return value;
}
function greaterDecimal(a: string, b: string) {
  const [ai, af = ''] = a.split('.'); const [bi, bf = ''] = b.split('.'); const scale = Math.max(af.length, bf.length);
  return BigInt(ai + af.padEnd(scale, '0')) > BigInt(bi + bf.padEnd(scale, '0'));
}
export function readShipmentFilters(search: URLSearchParams): ShipmentFilters {
  const get = (key: string) => { if (search.getAll(key).length > 1) throw new Error('Duplicate filter'); return search.get(key)?.trim() || undefined; };
  const rawStatus = get('status');
  const status = rawStatus && /^\d+$/.test(rawStatus) ? shipmentStatuses[Number(rawStatus)] : rawStatus;
  if (rawStatus && !shipmentStatuses.includes(status as ShipmentStatus)) throw new Error('Invalid status');
  const sortBy = get('sortBy') ?? 'CreatedAt'; const sortDirection = get('sortDirection') ?? 'Descending';
  if (!sortFields.includes(sortBy as ShipmentFilters['sortBy']) || !['Ascending', 'Descending'].includes(sortDirection)) throw new Error('Invalid sort');
  const page = get('pageNumber') ?? '1'; const size = get('pageSize') ?? '25';
  if (!/^\d+$/.test(page) || !/^\d+$/.test(size) || !Number.isSafeInteger(Number(page)) || Number(page) < 1 || Number(size) < 1 || Number(size) > 100) throw new Error('Invalid page');
  if (Number(page) > 2147483647 || (Number(page) - 1) * Number(size) > 2147483647) throw new Error('Invalid page');
  const fromUtc = timestamp(get('fromUtc')); const toUtc = timestamp(get('toUtc'));
  if (fromUtc && toUtc && Date.parse(fromUtc) > Date.parse(toUtc)) throw new Error('Invalid date range');
  const minCodAmount = decimal(get('minCodAmount')); const maxCodAmount = decimal(get('maxCodAmount'));
  if (minCodAmount && maxCodAmount && greaterDecimal(minCodAmount, maxCodAmount)) throw new Error('Invalid COD range');
  const trackingCode = get('trackingCode'), receiverName = get('receiverName'), receiverPhone = get('receiverPhone');
  if ([trackingCode, receiverName, receiverPhone].some(value => value && value.length > 100)) throw new Error('Filter too long');
  return { status: status as ShipmentStatus | undefined, sortBy: sortBy as ShipmentFilters['sortBy'], sortDirection: sortDirection as ShipmentFilters['sortDirection'],
    pageNumber: Number(page), pageSize: Number(size), fromUtc, toUtc, minCodAmount, maxCodAmount, trackingCode, receiverName, receiverPhone };
}
export function shipmentSearch(filters: Partial<ShipmentFilters>) {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(filters)) if (value !== undefined && value !== '') query.set(key, String(value));
  return query;
}
export function shipmentListUrl(shopId: string, filters: Partial<ShipmentFilters> = {}) {
  const query = shipmentSearch(filters); query.set('shopId', shopId);
  return `${routePaths.shipments}?${query}`;
}
