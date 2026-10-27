import { routePaths } from '../../shared/routing/routePaths';
import { shipmentSearch, type ShipmentFilters } from '../../shared/model/shipmentFilters';
export function shipmentListUrl(shopId: string, filters: Partial<ShipmentFilters> = {}) {
  const query = shipmentSearch(filters);
  query.set('shopId', shopId);
  return `${routePaths.shipments}?${query}`;
}
