import { useEffect, type ReactNode } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useLocation, useSearchParams } from 'react-router';
import { useSession } from '../../shared/auth/SessionContext';
import { ApiError } from '../../shared/api/httpClient';
import { queryKeys } from './queryKeys';
import { getShopContext } from './api';
import { ShopContextState } from './ShopContext';

export function ShopContextProvider({ children }: { children: ReactNode }) {
  const { session, permissionVersion, isLoading, error: sessionError } = useSession();
  const [search, setSearch] = useSearchParams();
  const integrationRoute = useLocation().pathname === '/partner/integrations';
  const queryClient = useQueryClient();
  const shopId = search.get('shopId');
  const valid = !shopId || /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(shopId);
  const enabled =
    !integrationRoute && !isLoading && !sessionError && Boolean(session?.roles.includes('Shop')) && valid;
  const query = useQuery({
    queryKey: queryKeys.context(session?.userId ?? '', shopId, permissionVersion),
    enabled,
    staleTime: 0,
    gcTime: 0,
    retry: false,
    queryFn: ({ signal }) => getShopContext(shopId, signal),
  });
  const context =
    enabled && !query.error && query.data && (!shopId || query.data.selectedShopId === shopId)
      ? query.data
      : null;
  useEffect(() => {
    // Integration management has its own permission-filtered Shop selector.
    if (!integrationRoute && !shopId && context)
      setSearch(
        (current) => {
          current.set('shopId', context.selectedShopId);
          return current;
        },
        { replace: true },
      );
  }, [context, shopId, setSearch, integrationRoute]);
  function selectShop(id: string) {
    if (session) {
      void queryClient.cancelQueries({ queryKey: queryKeys.user(session.userId) });
      queryClient.removeQueries({ queryKey: queryKeys.user(session.userId) });
    }
    setSearch((current) => {
      current.set('shopId', id);
      return current;
    });
  }
  return (
    <ShopContextState.Provider
      value={{
        context,
        loading: enabled && query.isPending,
        error: !valid ? new ApiError(400, 'Application.ValidationFailed', 'Invalid Shop ID') : query.error,
        selectShop,
        reload: () => {
          if (enabled) void query.refetch();
        },
      }}
    >
      {children}
    </ShopContextState.Provider>
  );
}
