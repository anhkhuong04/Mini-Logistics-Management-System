import { requestJson } from '../../shared/api/httpClient';
import type { ShopContext } from './ShopContext';
export const getShopContext = (shopId: string | null, signal: AbortSignal) =>
  requestJson<ShopContext>(`/api/v1/app/shop-context${shopId ? `?${new URLSearchParams({ shopId })}` : ''}`, { signal });
