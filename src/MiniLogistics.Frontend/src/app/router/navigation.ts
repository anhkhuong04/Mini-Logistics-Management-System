import type { Session } from '../../shared/auth/SessionContext';
import type { ShopContext } from '../../features/shop-context/ShopContext';
import type { NavigationItem } from '../layouts/AppLayout';
import { createElement } from 'react';
import { HomeOutlined, DashboardOutlined, InboxOutlined, TruckOutlined, SettingOutlined, BellOutlined, UserOutlined, TeamOutlined, AuditOutlined, ApiOutlined, WalletOutlined } from '@ant-design/icons';
const navigationIcons = { home: HomeOutlined, operations: TruckOutlined, shipper: TruckOutlined, admin: SettingOutlined, integrations: ApiOutlined,
  dashboard: DashboardOutlined, shipments: InboxOutlined, notifications: BellOutlined, profile: UserOutlined, cod: WalletOutlined,
  audit: AuditOutlined, 'shop-integrations': ApiOutlined, staff: TeamOutlined };
export function navigationFor(session: Session | null, context: ShopContext | null, locale = 'vi-VN'): NavigationItem[] {
  const links: NavigationItem[] = [{ key: 'home', label: 'Trang chính', to: '/' }];
  const add = (key: string, label: string, to: string) => links.push({ key, label, to, document: !['integrations', 'shop-integrations', 'admin', 'dashboard', 'shipments', 'operations', 'shipper', 'profile', 'staff', 'cod', 'audit', 'notifications'].includes(key) });
  if (session?.operationPermissions.includes('operations.shipment.view')) add('operations', 'Điều phối', '/operations/assignments');
  if (session?.roles.includes('Shipper')) add('shipper', 'Công việc giao hàng', '/shipper/shipments');
  if (session?.roles.includes('Admin')) add('admin', 'Quản trị', '/admin');
  if (session?.roles.some(role => ['Admin', 'IntegrationAdmin'].includes(role))) add('integrations', 'Tích hợp', '/partner/integrations');
  const shop = context?.shops.find(item => item.shopId === context.selectedShopId);
  if (shop) {
    const query = `?shopId=${encodeURIComponent(shop.shopId)}`;
    if (shop.permissions.includes('ViewShipments')) {
      add('dashboard', 'Tổng quan Shop', `/dashboard${query}`);
      add('shipments', 'Vận đơn', `/shipments${query}`);
      add('notifications', 'Thông báo', `/shop/notifications${query}`);
      add('profile', 'Hồ sơ Shop', `/profile${query}`);
    }
    if (shop.permissions.includes('ViewCod')) add('cod', 'Báo cáo COD', `/shop/cod-report${query}`);
    if (shop.permissions.includes('ViewAudit')) add('audit', 'Nhật ký Shop', `/shop/audit${query}`);
    if (shop.permissions.includes('ManageIntegrations')) add('shop-integrations', 'Tích hợp Shop', `/partner/integrations${query}`);
    if (shop.isOwner) add('staff', 'Nhân viên Shop', `/shop/staff${query}`);
  }
  const english: Record<string, string> = { home: 'Home', operations: 'Operations', shipper: 'Delivery workspace', admin: 'Administration',
    integrations: 'Integrations', dashboard: 'Shop overview', shipments: 'Shipments', notifications: 'Notifications', profile: 'Shop profile',
    cod: 'COD report', audit: 'Shop audit', 'shop-integrations': 'Shop integrations', staff: 'Shop staff' };
  return links.map(item => ({ ...item, label: locale === 'en-US' ? english[item.key] ?? item.label : item.label,
    icon: createElement(navigationIcons[item.key as keyof typeof navigationIcons], { 'aria-hidden': true }) }));
}
