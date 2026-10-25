import { afterEach, expect, it, vi } from 'vitest';
import { waitForQuote } from './useShippingQuote';
afterEach(() => vi.useRealTimers());
it('debounces preview work and aborts a stale input before a request can start', async () => {
  vi.useFakeTimers(); const first = new AbortController(); const second = new AbortController();
  const stale = waitForQuote(first.signal).catch(error => error.name);
  await vi.advanceTimersByTimeAsync(200); first.abort();
  expect(await stale).toBe('AbortError');
  const ready = vi.fn(); const current = waitForQuote(second.signal).then(ready);
  await vi.advanceTimersByTimeAsync(349); expect(ready).not.toHaveBeenCalled();
  await vi.advanceTimersByTimeAsync(1); await current; expect(ready).toHaveBeenCalledOnce();
});
