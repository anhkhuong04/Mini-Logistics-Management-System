import type { components } from '../../../shared/api/generated/browser-api';
import { requestJson } from '../../../shared/api/httpClient';
export type ShippingQuoteRequest = components['schemas']['BrowserShippingQuoteRequest'];
export type ShippingQuote = components['schemas']['ShopShippingQuoteResponse'];
export const getShippingQuote = (shopId: string, body: ShippingQuoteRequest, signal: AbortSignal) =>
  requestJson<ShippingQuote>(`/api/v1/app/shops/${encodeURIComponent(shopId)}/shipping/quote`, { method: 'POST', body, signal });
