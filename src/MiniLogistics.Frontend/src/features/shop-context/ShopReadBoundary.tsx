import type { ReactNode } from 'react';
import { Alert, Result } from 'antd';
import { ApiError } from '../../shared/api/httpClient';
import { AsyncContent } from '../../shared/ui/AsyncContent';
import { useShopContext } from './ShopContext';
import { useShipmentCopy } from '../../shared/i18n/shipmentCopy';
export function ShopReadBoundary({ children }: { children: ReactNode }) {
  const { context, loading, error, reload } = useShopContext(); const copy = useShipmentCopy();
  if (error instanceof ApiError && error.status === 403) return <Result status="403" title={copy.denied} />;
  if (loading || error || !context) return <AsyncContent loading={loading} error={Boolean(error)} onRetry={reload} empty={!loading && !error}>{null}</AsyncContent>;
  const selected = context.shops.find(shop => shop.shopId === context.selectedShopId);
  return <>{selected?.isActive === false && <Alert type="warning" showIcon title={copy.inactive} />}{children}</>;
}
