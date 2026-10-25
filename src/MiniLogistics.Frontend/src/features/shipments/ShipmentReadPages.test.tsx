import { screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { Route, Routes } from 'react-router';
import { renderWithProviders } from '../../test/renderWithProviders';
import { ShopContextState } from '../shop-context/ShopContext';
import { ShipmentListPage } from './ShipmentListPage';
import { ShipmentDetailPage } from './ShipmentDetailPage';
const shopId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
const session = { userId: 'user', displayName: 'User', roles: ['Shop'], operationPermissions: [], locale: 'vi-VN', serverTimeZoneId: 'UTC' };
function shopUi(children: React.ReactNode, permissions = ['ViewShipments']) {
  return <ShopContextState.Provider value={{ context: { selectedShopId: shopId, shops: [{ shopId, name: 'Shop A', permissions,
    isOwner: false, isActive: true, phoneNumber: '***', addressLine: '***', ward: '', province: '', country: '', staffRole: 'Viewer' }] },
    loading: false, error: null, selectShop: vi.fn(), reload: vi.fn() }}>{children}</ShopContextState.Provider>;
}
afterEach(() => vi.unstubAllGlobals());
describe('shipment reading', () => {
  it('sends URL filters to the paged API and hides COD from staff without permission', async () => {
    const fetch = vi.fn().mockResolvedValue(Response.json({ items: [{ shipmentId: id, trackingCode: 'ML-TEST', receiverName: 'N***', status: 'PendingPickup',
      routeType: 'IntraProvince', weightKg: '1', chargeableWeightKg: '1', codAmount: '0', shippingFeeAmount: '25000', currency: 'VND', createdAtUtc: '2026-10-01T00:00:00Z' }], pageNumber: 2, pageSize: 25, totalCount: 26, totalPages: 2 }));
    vi.stubGlobal('fetch', fetch);
    renderWithProviders(shopUi(<ShipmentListPage />), { session, path: `/shipments?shopId=${shopId}&status=PendingPickup&pageNumber=2` });
    expect(await screen.findByRole('link', { name: 'ML-TEST' })).toHaveAttribute('href', `/shipments/${id}?shopId=${shopId}`);
    const url = new URL(fetch.mock.calls[0][0], 'https://localhost');
    expect(url.searchParams.get('pageNumber')).toBe('2'); expect(url.searchParams.get('status')).toBe('PendingPickup');
    expect(screen.queryByRole('columnheader', { name: 'COD' })).not.toBeInTheDocument();
  });
  it('shows invalid URL feedback without fetching and distinguishes denied/not-found', async () => {
    const fetch = vi.fn(); vi.stubGlobal('fetch', fetch);
    renderWithProviders(shopUi(<ShipmentListPage />), { session, path: '/shipments?pageSize=100000' });
    expect(screen.getByRole('alert')).toBeInTheDocument(); expect(fetch).not.toHaveBeenCalled();
  });
  it.each(['invalid-id', '00000000-0000-0000-0000-000000000000', '------------------------------------'])('rejects invalid detail ID %s without an endless loader or request', async invalidId => {
    const fetch = vi.fn(); vi.stubGlobal('fetch', fetch);
    renderWithProviders(shopUi(<Routes><Route path="/shipments/:id" element={<ShipmentDetailPage />} /></Routes>), { session, path: `/shipments/${invalidId}?shopId=${shopId}` });
    expect(screen.getByText('Không tìm thấy vận đơn')).toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });
  it('renders a scoped not-found response without fabricated detail sections', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json({ code: 'Application.NotFound', title: 'Missing' }, { status: 404 })));
    renderWithProviders(shopUi(<Routes><Route path="/shipments/:id" element={<ShipmentDetailPage />} /></Routes>), { session, path: `/shipments/${id}?shopId=${shopId}` });
    expect(await screen.findByText('Không tìm thấy vận đơn')).toBeInTheDocument();
    expect(screen.queryByText('Bằng chứng giao hàng')).not.toBeInTheDocument();
  });
});
