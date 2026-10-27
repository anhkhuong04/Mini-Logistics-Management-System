import { expect, it } from 'vitest';
import { readShipperFilters, nextShipperStatuses, canCollectCod } from './models';
it('keeps enum filters and bounded paging in the URL and rejects malformed input', () => {
  expect(
    readShipperFilters(
      new URLSearchParams('stage=CodPending&pageNumber=2&pageSize=10&businessDate=2026-10-25'),
    ),
  ).toMatchObject({ stage: 'CodPending', pageNumber: 2, pageSize: 10, businessDate: '2026-10-25' });
  for (const query of [
    'stage=1',
    'stage=Pickup&stage=Transit',
    'pageSize=101',
    'pageNumber=2147483647',
    'businessDate=2026-02-30',
  ])
    expect(() => readShipperFilters(new URLSearchParams(query))).toThrow();
});
it('keeps delivered COD pending actionable while terminal statuses have no status command', () => {
  expect(nextShipperStatuses('Delivered')).toEqual([]);
  expect(nextShipperStatuses('Returned')).toEqual([]);
  expect(nextShipperStatuses('DeliveryFailed')).toEqual(['Delivering', 'Returned']);
  expect(canCollectCod('Delivered', 'PendingCollection')).toBe(true);
  expect(canCollectCod('Delivering', 'PendingCollection')).toBe(false);
  expect(canCollectCod('Delivered', 'Collected')).toBe(false);
});
