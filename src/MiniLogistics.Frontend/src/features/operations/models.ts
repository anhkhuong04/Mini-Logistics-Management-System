import { readShipmentFilters, type ShipmentStatus } from '../../shared/model/shipmentFilters';
export type OperationsFilters = {
  view: 'pending' | 'operations' | 'cod';
  searchText?: string;
  province?: string;
  status?: ShipmentStatus;
  codStatus?: string;
  shipperId?: string;
  fromUtc?: string;
  toUtc?: string;
  minCodAmount?: string;
  maxCodAmount?: string;
  slaOnly: boolean;
  pageNumber: number;
  pageSize: number;
};
export function parseOperationsFilters(search: URLSearchParams): OperationsFilters {
  const base = readShipmentFilters(search);
  const get = (key: string) => {
    if (search.getAll(key).length > 1) throw new Error('Duplicate filter');
    return search.get(key)?.trim() || undefined;
  };
  const view = get('view') ?? 'pending';
  const codStatus = get('codStatus');
  const shipperId = get('shipperId');
  const slaOnly = get('slaOnly');
  const searchText = get('searchText');
  const province = get('province');
  if (
    !['pending', 'operations', 'cod'].includes(view) ||
    (codStatus && !['NotRequired', 'PendingCollection', 'Collected', 'Settled'].includes(codStatus)) ||
    (slaOnly && !['true', 'false'].includes(slaOnly)) ||
    [searchText, province].some((item) => item && item.length > 100) ||
    (shipperId && !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(shipperId))
  )
    throw new Error('Invalid filter');
  return {
    view: view as OperationsFilters['view'],
    searchText,
    province,
    status: base.status,
    codStatus,
    shipperId,
    fromUtc: base.fromUtc,
    toUtc: base.toUtc,
    minCodAmount: base.minCodAmount,
    maxCodAmount: base.maxCodAmount,
    slaOnly: slaOnly === 'true',
    pageNumber: base.pageNumber,
    pageSize: base.pageSize,
  };
}
export const normalizeProvince = (value: string) =>
  value
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .toLowerCase()
    .replaceAll('đ', 'd')
    .trim()
    .replace(/thanh pho |tinh /g, '')
    .split(/\s+/)
    .join(' ');
type ShipperChoice = {
  userId: string;
  fullName: string;
  maxActiveShipments: number | string;
  activeShipmentCount: number | string;
  isAvailableForAssignment: boolean;
  workingAreas: { province: string; isActive: boolean }[];
};
export function shipperOptions<T extends ShipperChoice>(province: string, shippers: T[]) {
  return shippers
    .map((shipper) => ({
      ...shipper,
      matchesArea: shipper.workingAreas.some(
        (area) => area.isActive && normalizeProvince(area.province) === normalizeProvince(province),
      ),
      atCapacity: Number(shipper.activeShipmentCount) >= Number(shipper.maxActiveShipments),
    }))
    .sort(
      (a, b) =>
        Number(b.matchesArea) - Number(a.matchesArea) ||
        Number(b.isAvailableForAssignment && !b.atCapacity) -
          Number(a.isAvailableForAssignment && !a.atCapacity) ||
        Number(b.isAvailableForAssignment) - Number(a.isAvailableForAssignment) ||
        Number(a.atCapacity) - Number(b.atCapacity) ||
        Number(a.activeShipmentCount) - Number(b.activeShipmentCount) ||
        a.fullName.localeCompare(b.fullName) ||
        a.userId.localeCompare(b.userId),
    );
}
export function nextStatuses(status: string): ShipmentStatus[] {
  const next: Record<string, ShipmentStatus[]> = {
    Assigned: ['PickingUp', 'Cancelled'],
    PickingUp: ['PickedUp', 'Cancelled'],
    PickedUp: ['InTransit', 'Returned'],
    InTransit: ['Delivering', 'Returned'],
    Delivering: ['Delivered', 'DeliveryFailed', 'Returned'],
    DeliveryFailed: ['Delivering', 'Returned'],
  };
  return next[status] ?? [];
}
