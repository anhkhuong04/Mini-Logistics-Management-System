import { screen } from '@testing-library/react';
import { expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../test/renderWithProviders';
import { ShopContextState } from '../shop-context/ShopContext';
import { ShipmentActions } from './ShipmentActions';
import type { ShipmentDetail } from './api';
const shopId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const shipment = { shipmentId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', status: 'Draft' } as ShipmentDetail;
const session = { userId: 'user', displayName: 'User', roles: ['Shop'], operationPermissions: [], locale: 'vi-VN', serverTimeZoneId: 'UTC' };
function renderActions(permissions: string[], isActive = true) {
  return renderWithProviders(<ShopContextState.Provider value={{ context: { selectedShopId: shopId, shops: [{ shopId, name: 'Shop A', permissions,
    isOwner: false, isActive, phoneNumber: '***', addressLine: '***', ward: '', province: '', country: '', staffRole: 'Custom' }] },
    loading: false, error: null, selectShop: vi.fn(), reload: vi.fn() }}><ShipmentActions shipment={shipment} reload={vi.fn().mockResolvedValue(undefined)} /></ShopContextState.Provider>, { session });
}
it('does not offer masked-data editing to ManageShipments-only staff but retains submit/cancel', () => {
  renderActions(['ViewShipments', 'ManageShipments']);
  expect(screen.queryByRole('link', { name: 'Sửa vận đơn' })).not.toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Gửi đơn' })).toBeInTheDocument(); expect(screen.getByRole('button', { name: 'Hủy đơn' })).toBeInTheDocument();
});
it('does not offer mutations on an inactive Shop even with all relevant permissions', () => {
  renderActions(['ViewShipments', 'ManageShipments', 'ViewFullPii', 'ViewCod'], false);
  expect(screen.queryByRole('button')).not.toBeInTheDocument(); expect(screen.queryByRole('link')).not.toBeInTheDocument();
});
