import { describe, expect, it } from 'vitest';
import { navigationFor } from './navigation';
import type { Session } from '../../shared/auth/SessionContext';
import type { ShopContext } from '../../features/shop-context/ShopContext';
describe('navigation permissions', () => {
  const session: Session = { userId: 'staff', displayName: 'Staff', roles: ['Shop'], operationPermissions: [], locale: 'vi-VN', serverTimeZoneId: 'UTC' };
  const context: ShopContext = { selectedShopId: 'A', shops: [{ shopId: 'A', name: 'A', isOwner: false, isActive: true,
    phoneNumber: 'masked', addressLine: 'masked', ward: '', province: '', country: '', staffRole: 'Viewer', permissions: ['ViewShipments'] }] };
  it('does not infer Shop permissions from a role or missing context', () => {
    expect(navigationFor(session, null)).toHaveLength(1);
    const links = navigationFor(session, context);
    expect(links.find(item => item.key === 'shipments')).toMatchObject({ document: false, to: '/shipments?shopId=A' });
    expect(links.some(item => ['cod', 'staff', 'admin'].includes(item.key))).toBe(false);
    expect(navigationFor(session, context, 'en-US').find(item => item.key === 'shipments')?.label).toBe('Shipments');
  });
});
