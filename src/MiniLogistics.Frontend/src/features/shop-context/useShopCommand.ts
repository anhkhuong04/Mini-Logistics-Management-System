import { useLayoutEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { ApiError } from '../../shared/api/httpClient';
import { useSession } from '../../shared/auth/SessionContext';
import { useShopContext } from './ShopContext';
export function useShopCommand() {
  const { session } = useSession();
  const { context } = useShopContext();
  const cache = useQueryClient();
  const identity = `${session?.userId}/${context?.selectedShopId}`;
  const current = useRef(identity);
  const mounted = useRef(true);
  const busy = useRef(false);
  const [pending, setPending] = useState(false);
  const [failure, setFailure] = useState<Error | null>(null);
  useLayoutEffect(() => {
    current.current = identity;
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, [identity]);
  const unknown =
    failure !== null &&
    (!(failure instanceof ApiError) ||
      failure.status === 0 ||
      failure.status >= 500 ||
      failure.code === 'Browser.ProtocolError');
  const conflict = failure instanceof ApiError && failure.status === 409;
  const invalidate = () =>
    cache.invalidateQueries({ queryKey: ['user', session?.userId, 'shop', context?.selectedShopId] });
  async function run<T>(operation: () => Promise<T>): Promise<{ value: T } | null> {
    if (busy.current || unknown || conflict) return null;
    const requestIdentity = identity;
    const user = session?.userId,
      shop = context?.selectedShopId;
    busy.current = true;
    setPending(true);
    setFailure(null);
    try {
      const value = await operation();
      await cache.invalidateQueries({ queryKey: ['user', user, 'shop', shop] });
      if (!mounted.current || current.current !== requestIdentity) return null;
      return { value };
    } catch (error) {
      if (mounted.current && current.current === requestIdentity)
        setFailure(error instanceof Error ? error : new Error('Unknown outcome'));
      return null;
    } finally {
      busy.current = false;
      if (mounted.current && current.current === requestIdentity) setPending(false);
    }
  }
  return {
    run,
    invalidate,
    pending,
    failure,
    unknown,
    conflict,
    blocked: unknown || conflict,
    reset: () => setFailure(null),
  };
}
