import { describe, expect, it } from 'vitest';
import { normalizeProvince, shipperOptions, nextStatuses, parseOperationsFilters } from './models';
describe('operations parity', () => {
  it('normalizes administrative prefixes and ranks matching available capacity before overrides', () => {
    expect(normalizeProvince('Thành phố Hồ Chí Minh')).toBe(normalizeProvince('Hồ Chí Minh'));
    const make = (userId: string, province: string, load: number, available = true) => ({ userId, fullName: userId,
      phoneNumber: null, maxActiveShipments: 3, activeShipmentCount: load, isAvailableForAssignment: available,
      workingAreas: [{ province, isActive: true }] });
    const result = shipperOptions('Thành phố Hồ Chí Minh', [make('override', 'Hà Nội', 0), make('full', 'Hồ Chí Minh', 3), make('ready', 'Hồ Chí Minh', 1)]);
    expect(result.map(item => item.userId)).toEqual(['ready', 'full', 'override']);
    expect(result[1].atCapacity).toBe(true); expect(result[2].matchesArea).toBe(false);
  });
  it('ports valid status actions and rejects malformed URL filters', () => {
    expect(nextStatuses('Delivered')).toEqual([]);
    expect(nextStatuses('DeliveryFailed')).toEqual(['Delivering', 'Returned']);
    expect(() => parseOperationsFilters(new URLSearchParams('pageSize=1000'))).toThrow();
    expect(() => parseOperationsFilters(new URLSearchParams('minCodAmount=1e3'))).toThrow();
    expect(() => parseOperationsFilters(new URLSearchParams('status=Assigned&status=Delivered'))).toThrow();
    expect(parseOperationsFilters(new URLSearchParams('view=operations&status=Assigned&pageNumber=2')).pageNumber).toBe(2);
  });
});
