import { useQuery } from '@tanstack/react-query';
import { useSession } from '../../../shared/auth/SessionContext';
import { useShopContext } from '../../shop-context';
import { queryKeys } from '../../shop-context';
import { getShippingQuote, type ShippingQuoteRequest } from './api';

export function waitForQuote(signal: AbortSignal, milliseconds = 350): Promise<void> {
  return new Promise((resolve, reject) => {
    const abort = () => {
      clearTimeout(timer);
      reject(new DOMException('Aborted', 'AbortError'));
    };
    const timer = setTimeout(() => {
      signal.removeEventListener('abort', abort);
      resolve();
    }, milliseconds);
    if (signal.aborted) abort();
    else signal.addEventListener('abort', abort, { once: true });
  });
}
export function useShippingQuote(body: ShippingQuoteRequest | null) {
  const { session } = useSession();
  const { context } = useShopContext();
  const shop = context?.shops.find((item) => item.shopId === context.selectedShopId);
  return useQuery({
    queryKey: [...queryKeys.shop(session?.userId ?? '', shop?.shopId ?? '', body ?? {}), 'shipping-quote'],
    enabled: Boolean(body && session && shop?.isActive && shop.permissions.includes('ManageShipments')),
    retry: false,
    queryFn: async ({ signal }) => {
      await waitForQuote(signal);
      return getShippingQuote(shop!.shopId, body!, signal);
    },
  });
}
