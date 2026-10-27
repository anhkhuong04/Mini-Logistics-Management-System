import { expect, it } from 'vitest';
import { serverDateRange, serverDateLabel } from './serverDateRange';
it('preserves the legacy server local calendar day as UTC instants with exact end ticks', () => {
  expect(serverDateRange('2026-10-01', '2026-10-01', 'Asia/Bangkok')).toEqual({
    fromUtc: '2026-09-30T17:00:00Z',
    toUtc: '2026-10-01T16:59:59.9999999Z',
  });
  expect(serverDateLabel('2026-09-30T17:00:00Z', 'Asia/Bangkok')).toBe('2026-10-01');
});
it('uses the offset at each DST boundary instead of the browser or current offset', () => {
  expect(serverDateRange('2026-03-08', '2026-03-08', 'America/New_York')).toEqual({
    fromUtc: '2026-03-08T05:00:00Z',
    toUtc: '2026-03-09T03:59:59.9999999Z',
  });
  expect(() => serverDateRange('2026-02-30', '', 'Asia/Bangkok')).toThrow();
  expect(() => serverDateRange('2026-10-02', '2026-10-01', 'Asia/Bangkok')).toThrow();
});
it('keeps leap-day and repeated DST day boundaries exact', () => {
  expect(serverDateRange('2028-02-29', '2028-02-29', 'UTC')).toEqual({
    fromUtc: '2028-02-29T00:00:00Z',
    toUtc: '2028-02-29T23:59:59.9999999Z',
  });
  expect(serverDateRange('2026-11-01', '2026-11-01', 'America/New_York')).toEqual({
    fromUtc: '2026-11-01T04:00:00Z',
    toUtc: '2026-11-02T04:59:59.9999999Z',
  });
});
