import { describe, expect, it } from 'vitest';
import { groupStatuses } from './statusDistribution';
import { shipmentStatuses } from '../shipments/shipmentFilters';
describe('real shipment distribution', () => {
  it('counts every known state once, omits zeros, and never draws an empty distribution', () => {
    const all = Object.fromEntries(shipmentStatuses.map(status => [status, 1]));
    const groups = groupStatuses(all);
    expect(groups.reduce((sum, group) => sum + group.count, 0)).toBe(shipmentStatuses.length);
    expect(groups.find(group => group.key === 'processing')?.count).toBe(5);
    expect(groupStatuses({ Delivered: 3, PendingPickup: 1, DeliveryFailed: 0 }).map(group => group.percent).sort()).toEqual([25, 75]);
    expect(groupStatuses({})).toEqual([]);
  });
});
