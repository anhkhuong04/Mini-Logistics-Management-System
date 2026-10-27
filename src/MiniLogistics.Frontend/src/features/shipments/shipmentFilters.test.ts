import { describe, expect, it } from 'vitest';
import { readShipmentFilters } from '../../shared/model/shipmentFilters';
import { shipmentListUrl } from './shipmentFilters';
import { utcDateRange } from '../../shared/lib/dateRange';
describe('shipment URL contracts', () => {
  it('preserves legacy status and exact UTC ticks without local-time conversion', () => {
    const filters = readShipmentFilters(
      new URLSearchParams(
        'status=7&fromUtc=2026-10-01T00:00:00Z&toUtc=2026-10-01T23:59:59.9999999Z&pageNumber=2',
      ),
    );
    expect(filters.status).toBe('Delivered');
    expect(filters.pageNumber).toBe(2);
    const url = shipmentListUrl('shop-a', filters);
    expect(new URLSearchParams(url.split('?')[1]).get('toUtc')).toBe('2026-10-01T23:59:59.9999999Z');
    expect(new URLSearchParams(url.split('?')[1]).get('shopId')).toBe('shop-a');
  });
  it('rejects invalid enum date range paging and lossy decimal inputs', () => {
    for (const query of [
      'status=999',
      'pageSize=101',
      'pageNumber=0',
      'pageNumber=2147483647&pageSize=100',
      'pageNumber=2147483648&pageSize=1',
      'fromUtc=2026-02-30T00:00:00Z',
      'fromUtc=2026-10-02T00:00:00Z&toUtc=2026-10-01T00:00:00Z',
      'minCodAmount=1e2',
      'minCodAmount=3.01&maxCodAmount=3.00',
    ])
      expect(() => readShipmentFilters(new URLSearchParams(query)), query).toThrow();
  });
  it('makes UTC day boundaries inclusive, accepts same day and open-ended filters', () => {
    expect(utcDateRange('2026-10-01', '2026-10-01')).toEqual({
      fromUtc: '2026-10-01T00:00:00Z',
      toUtc: '2026-10-01T23:59:59.9999999Z',
    });
    expect(utcDateRange('', '2026-10-01').fromUtc).toBeUndefined();
    expect(() => utcDateRange('2026-02-30', '')).toThrow();
  });
});
