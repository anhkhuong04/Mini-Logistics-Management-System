import { describe, expect, it } from 'vitest';
import { decimalString, formatMoney } from './money';
import { formatDateTime } from './dateTime';
import { createQueryClient } from '../api/queryClient';
import { ApiError } from '../api/httpClient';
describe('transport precision and retry policy', () => {
  it('formats large money and fractional zeros without number coercion', () => {
    expect(formatMoney(decimalString('9007199254740993.01'), 'VND', 'en-US')).toBe('9,007,199,254,740,993.01 VND');
    expect(formatMoney(decimalString('0.10'), 'VND', 'en-US')).toBe('0.10 VND');
    expect(() => decimalString('1e2')).toThrow();
  });
  it('uses the supplied timezone including historic DST instead of browser local time', () => {
    expect(formatDateTime('2026-01-01T12:00:00Z', 'America/New_York', 'en-US')).toContain('7:00');
    expect(formatDateTime('2026-07-01T12:00:00Z', 'America/New_York', 'en-US')).toContain('8:00');
    expect(() => formatDateTime('2026-01-01', 'UTC', 'en-US')).toThrow();
  });
  it('disables mutation retries and bounds read retries to transport and server failures', () => {
    const defaults = createQueryClient().getDefaultOptions();
    expect(defaults.mutations?.retry).toBe(false);
    const retry = defaults.queries?.retry as (count: number, error: Error) => boolean;
    for (const status of [400, 401, 403, 429]) expect(retry(0, new ApiError(status, 'test', 'safe'))).toBe(false);
    expect(retry(0, new ApiError(503, 'test', 'safe'))).toBe(true);
    expect(retry(2, new TypeError('transport'))).toBe(false);
  });
});
