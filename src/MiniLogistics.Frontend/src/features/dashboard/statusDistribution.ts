import type { ShipmentStatus } from '../shipments/shipmentFilters';
const groups: Array<{ key: string; status: ShipmentStatus; statuses: ShipmentStatus[]; color: string }> = [
  { key: 'pending', status: 'PendingPickup', statuses: ['PendingPickup'], color: '#a15c07' },
  { key: 'processing', status: 'InTransit', statuses: ['Assigned', 'PickingUp', 'PickedUp', 'InTransit', 'Delivering'], color: '#0077c8' },
  { key: 'delivered', status: 'Delivered', statuses: ['Delivered'], color: '#087a55' },
  { key: 'failed', status: 'DeliveryFailed', statuses: ['DeliveryFailed'], color: '#b42318' },
  { key: 'returned', status: 'Returned', statuses: ['Returned'], color: '#a15c07' },
  { key: 'cancelled', status: 'Cancelled', statuses: ['Cancelled'], color: '#546b7b' },
  { key: 'draft', status: 'Draft', statuses: ['Draft'], color: '#546b7b' },
];
export function groupStatuses(counts: Record<string, number>) {
  const rows = groups.map(group => ({ ...group, count: group.statuses.reduce((sum, status) => sum + (counts[status] ?? 0), 0) }));
  const total = rows.reduce((sum, row) => sum + row.count, 0);
  return rows.filter(row => row.count > 0).map(row => ({ ...row, percent: row.count / total * 100 }));
}
