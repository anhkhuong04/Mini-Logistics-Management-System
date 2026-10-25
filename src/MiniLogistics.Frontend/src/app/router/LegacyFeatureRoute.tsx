import { useEffect } from 'react';
import { useLocation } from 'react-router';
import { Result } from 'antd';
import { useShopContext } from '../../features/shop-context/ShopContext';
import { AsyncContent } from '../../shared/ui/AsyncContent';
import { useAuthCopy } from '../../shared/i18n/authCopy';
export function LegacyFeatureRoute({ shopPermission, ownerOnly }: { shopPermission?: string; ownerOnly?: boolean }) {
  const { context, loading, error, reload } = useShopContext();
  const location = useLocation();
  const copy = useAuthCopy();
  const selected = context?.shops.find(shop => shop.shopId === context.selectedShopId);
  const allowed = !shopPermission && !ownerOnly || Boolean(selected && (!shopPermission || selected.permissions.includes(shopPermission)) && (!ownerOnly || selected.isOwner));
  useEffect(() => {
    if (allowed && !loading && !error) window.location.replace(`${location.pathname}${location.search}${location.hash}`);
  }, [allowed, error, loading, location]);
  if (loading || error) return <AsyncContent loading={loading} error={Boolean(error)} onRetry={reload}>{null}</AsyncContent>;
  if (!allowed) return <Result status="403" title={copy.forbidden} />;
  return <AsyncContent loading>{null}</AsyncContent>;
}
