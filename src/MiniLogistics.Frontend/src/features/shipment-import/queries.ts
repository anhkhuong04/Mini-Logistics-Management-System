import { useCallback, useEffect, useState } from 'react';
import { useSession } from '../../shared/auth/SessionContext';
import { useShopContext } from '../shop-context';
import { getImportBatch } from './api';
export const isImportTerminal = (status?: string | null) =>
  Boolean(status && ['Completed', 'CompletedWithErrors', 'Failed'].includes(status));
export function useBatchPolling<T extends { status?: string | null }>(
  key: string | null,
  read: (key: string, signal: AbortSignal) => Promise<T>,
) {
  const [snapshot, setSnapshot] = useState<{ key: string | null; data: T | null; error: Error | null }>({
    key: null,
    data: null,
    error: null,
  });
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    if (!key) return;
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout>;
    let active = true;
    async function poll() {
      try {
        const value = await read(key!, controller.signal);
        if (!active) return;
        setSnapshot({ key, data: value, error: null });
        if (!isImportTerminal(value.status))
          timer = setTimeout(() => {
            void poll();
          }, 2000);
      } catch (failure) {
        if (active)
          setSnapshot({
            key,
            data: null,
            error: failure instanceof Error ? failure : new Error('Batch read failed'),
          });
      }
    }
    void poll();
    return () => {
      active = false;
      clearTimeout(timer);
      controller.abort();
    };
  }, [key, read, revision]);
  const current = snapshot.key === key ? snapshot : { data: null, error: null };
  return {
    ...current,
    retry: () => {
      setSnapshot({ key, data: null, error: null });
      setRevision((value) => value + 1);
    },
  };
}
export function useImportBatch(id: string | null) {
  const { session } = useSession();
  const { context } = useShopContext();
  const shopId = context?.selectedShopId;
  const read = useCallback(
    (_: string, signal: AbortSignal) => getImportBatch(shopId!, id!, signal),
    [id, shopId],
  );
  return useBatchPolling(session && shopId && id ? `${session.userId}/${shopId}/${id}` : null, read);
}
