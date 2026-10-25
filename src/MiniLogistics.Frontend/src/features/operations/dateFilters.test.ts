import { expect, it } from 'vitest';
import { operationsDateRange, operationsDateLabel } from './dateFilters';
it('preserves the legacy server local calendar day as UTC instants with exact end ticks', () => {
  expect(operationsDateRange('2026-10-01', '2026-10-01', 'Asia/Bangkok')).toEqual({ fromUtc: '2026-09-30T17:00:00Z', toUtc: '2026-10-01T16:59:59.9999999Z' });
  expect(operationsDateLabel('2026-09-30T17:00:00Z', 'Asia/Bangkok')).toBe('2026-10-01');
});
it('uses the offset at each DST boundary instead of the browser or current offset', () => {
  expect(operationsDateRange('2026-03-08', '2026-03-08', 'America/New_York')).toEqual({ fromUtc: '2026-03-08T05:00:00Z', toUtc: '2026-03-09T03:59:59.9999999Z' });
  expect(() => operationsDateRange('2026-02-30', '', 'Asia/Bangkok')).toThrow();
  expect(() => operationsDateRange('2026-10-02', '2026-10-01', 'Asia/Bangkok')).toThrow();
});
