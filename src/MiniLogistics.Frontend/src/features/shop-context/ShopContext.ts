import { createContext, useContext } from 'react';
import type { components } from '../../shared/api/generated/browser-api';
export type ShopContext = components['schemas']['BrowserShopContext'];
export const ShopContextState = createContext<{
  context: ShopContext | null;
  loading: boolean;
  error: Error | null;
  selectShop(id: string): void;
  reload(): void;
} | null>(null);
export function useShopContext() {
  const value = useContext(ShopContextState);
  if (!value) throw new Error('ShopContextProvider is required');
  return value;
}
