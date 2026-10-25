import { useQuery } from '@tanstack/react-query';
import { useSession } from '../../shared/auth/SessionContext';
import { useShopContext } from '../shop-context/ShopContext';
import { queryKeys } from '../shop-context/queryKeys';
import { getDashboard } from './api';
import type { ShipmentFilters } from '../shipments/shipmentFilters';
export function useDashboard(range: Pick<ShipmentFilters, 'fromUtc' | 'toUtc'> | null) {
  const { session } = useSession(); const { context } = useShopContext(); const shop = context?.selectedShopId;
  return useQuery({ queryKey: [...queryKeys.shop(session?.userId ?? '', shop ?? '', range ?? {}), 'dashboard'],
    enabled: Boolean(session && shop && range), queryFn: ({ signal }) => getDashboard(shop!, range!, signal) });
}
