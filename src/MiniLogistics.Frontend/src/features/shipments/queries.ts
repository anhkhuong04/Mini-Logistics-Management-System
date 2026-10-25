import { useQuery } from '@tanstack/react-query';
import { useSession } from '../../shared/auth/SessionContext';
import { useShopContext } from '../shop-context/ShopContext';
import { queryKeys } from '../shop-context/queryKeys';
import { getShipment, getShipments } from './api';
import type { ShipmentFilters } from './shipmentFilters';
export function useShipments(filters: ShipmentFilters | null) {
  const { session } = useSession(); const { context } = useShopContext();
  const shop = context?.selectedShopId;
  return useQuery({ queryKey: [...queryKeys.shop(session?.userId ?? '', shop ?? '', filters ?? {}), 'shipments'],
    enabled: Boolean(session && shop && filters),
    queryFn: ({ signal }) => getShipments(shop!, filters!, signal) });
}
export const isShipmentId = (id: string) => /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(id) && !/^0+-0+-0+-0+-0+$/.test(id);
export function useShipment(id: string) {
  const { session } = useSession(); const { context } = useShopContext(); const shop = context?.selectedShopId;
  return useQuery({ queryKey: [...queryKeys.shop(session?.userId ?? '', shop ?? ''), 'shipment', id],
    enabled: Boolean(session && shop && isShipmentId(id)), queryFn: ({ signal }) => getShipment(shop!, id, signal) });
}
