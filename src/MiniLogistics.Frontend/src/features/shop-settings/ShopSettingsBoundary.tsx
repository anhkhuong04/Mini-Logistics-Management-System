import type { ReactNode } from 'react';
import { Result } from 'antd';
import { useSession } from '../../shared/auth/SessionContext';
import { useShopContext, type ShopContext } from '../shop-context/ShopContext';
import { ShopReadBoundary } from '../shop-context/ShopReadBoundary';
import { ShopContextSelector } from '../shop-context/ShopContextSelector';
import './shop-settings.css';
export type SelectedShop = ShopContext['shops'][number];
export function ShopSettingsBoundary({ permission = 'ViewShipments', ownerOnly = false, children }: {
  permission?: string; ownerOnly?: boolean; children: (shop: SelectedShop, user: string) => ReactNode;
}) {
  const { context } = useShopContext(); const { session, permissionVersion } = useSession();
  const shop = context?.shops.find(item => item.shopId === context.selectedShopId);
  return <ShopReadBoundary><ShopContextSelector showInactive />{shop && session &&
    (!shop.permissions.includes(permission) || ownerOnly && !shop.isOwner ? <Result status="403" title="Không có quyền truy cập Shop" />
      : <div className="shop-settings" key={`${session.userId}:${permissionVersion}:${shop.shopId}:${shop.permissions.join(',')}:${shop.isActive}:${shop.isOwner}`}>{children(shop, session.userId)}</div>)}
  </ShopReadBoundary>;
}
