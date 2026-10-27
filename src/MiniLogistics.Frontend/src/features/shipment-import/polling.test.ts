import { afterEach, expect, it, vi } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { useBatchPolling } from './queries';
afterEach(() => {
  vi.useRealTimers();
});
it('polls every two seconds and stops at terminal state', async () => {
  vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
  const read = vi
    .fn()
    .mockResolvedValueOnce({ status: 'Processing' })
    .mockResolvedValueOnce({ status: 'Completed' });
  const { result } = renderHook(() => useBatchPolling('batch', read));
  await act(async () => {
    await Promise.resolve();
  });
  expect(read).toHaveBeenCalledTimes(1);
  await act(async () => {
    await vi.advanceTimersByTimeAsync(1999);
  });
  expect(read).toHaveBeenCalledTimes(1);
  await act(async () => {
    await vi.advanceTimersByTimeAsync(1);
  });
  expect(result.current.data?.status).toBe('Completed');
  await act(async () => {
    await vi.advanceTimersByTimeAsync(6000);
  });
  expect(read).toHaveBeenCalledTimes(2);
});
it('aborts old Shop requests, ignores late results and waits for manual retry after failure', async () => {
  let resolve!: (value: { status: string }) => void;
  let signal!: AbortSignal;
  const read = vi.fn((_: string, requestSignal: AbortSignal) => {
    signal = requestSignal;
    return new Promise<{ status: string }>((done) => {
      resolve = done;
    });
  });
  const { result, rerender, unmount } = renderHook(({ id }) => useBatchPolling(id, read), {
    initialProps: { id: 'old' },
  });
  await waitFor(() => expect(read).toHaveBeenCalledTimes(1));
  const oldSignal = signal;
  const oldResolve = resolve;
  rerender({ id: 'new' });
  expect(oldSignal.aborted).toBe(true);
  await act(async () => {
    oldResolve({ status: 'Completed' });
  });
  expect(result.current.data).toBeNull();
  unmount();
  expect(signal.aborted).toBe(true);
});
it('stops on read failure and only restarts when the user retries', async () => {
  vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
  const read = vi
    .fn()
    .mockRejectedValueOnce(new Error('Unavailable'))
    .mockResolvedValueOnce({ status: 'CompletedWithErrors' });
  const { result } = renderHook(() => useBatchPolling('batch', read));
  await act(async () => {
    await Promise.resolve();
  });
  expect(result.current.error).not.toBeNull();
  await act(async () => {
    await vi.advanceTimersByTimeAsync(6000);
  });
  expect(read).toHaveBeenCalledTimes(1);
  await act(async () => {
    result.current.retry();
  });
  expect(read).toHaveBeenCalledTimes(2);
  expect(result.current.data?.status).toBe('CompletedWithErrors');
});
