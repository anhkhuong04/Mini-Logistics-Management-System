import { isResourceId } from '../../shared/lib/identifiers';
import { useQuery } from '@tanstack/react-query';
import { useSession } from '../../shared/auth/SessionContext';
import { useShopContext } from '../shop-context';
import { queryKeys } from '../shop-context';
import { getShipment, getShipments } from './api';
import type { ShipmentFilters } from '../../shared/model/shipmentFilters';
export function useShipments(filters: ShipmentFilters | null) {
  const { session } = useSession();
  const { context } = useShopContext();
  const shop = context?.selectedShopId;
  return useQuery({
    queryKey: [...queryKeys.shop(session?.userId ?? '', shop ?? '', filters ?? {}), 'shipments'],
    enabled: Boolean(session && shop && filters),
    queryFn: ({ signal }) => getShipments(shop!, filters!, signal),
  });
}
export function useShipment(id: string) {
  const { session } = useSession();
  const { context } = useShopContext();
  const shop = context?.selectedShopId;
  return useQuery({
    queryKey: [...queryKeys.shop(session?.userId ?? '', shop ?? ''), 'shipment', id],
    enabled: Boolean(session && shop && isResourceId(id)),
    queryFn: ({ signal }) => getShipment(shop!, id, signal),
  });
}
