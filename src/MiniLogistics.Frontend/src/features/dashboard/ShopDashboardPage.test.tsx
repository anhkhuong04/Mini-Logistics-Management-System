import { screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../test/renderWithProviders';
import { ShopContextState } from '../shop-context/ShopContext';
import { ShopDashboardPage } from './ShopDashboardPage';
const shopId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const session = { userId: 'user', displayName: 'User', roles: ['Shop'], operationPermissions: [], locale: 'vi-VN', serverTimeZoneId: 'UTC' };
const kpi = { shopId, totalShipments: 4, deliveredShipments: 3, returnedShipments: 0, deliveryFailedShipments: 0,
  deliveredRate: '75', returnedRate: '0', deliveryFailedRate: '0', totalShippingFee: '100000', pendingCodAmount: '30000', collectedCodAmount: '50000', settledCodAmount: '0', currency: 'VND', countByStatus: { Delivered: 3, PendingPickup: 1 } };
function renderDashboard({ permissions = ['ViewShipments', 'ViewCod', 'ManageShipments'], active = true } = {}) {
  return renderWithProviders(<ShopContextState.Provider value={{ context: { selectedShopId: shopId, shops: [{ shopId, name: 'Shop A', permissions,
    isOwner: false, isActive: active, phoneNumber: '***', addressLine: '***', ward: '', province: '', country: '', staffRole: 'Viewer' }] },
    loading: false, error: null, selectShop: vi.fn(), reload: vi.fn() }}><ShopDashboardPage /></ShopContextState.Provider>,
  { session, path: `/dashboard?shopId=${shopId}&fromUtc=2026-10-01T00:00:00Z&toUtc=2026-10-07T23:59:59.9999999Z` });
}
afterEach(() => vi.unstubAllGlobals());
describe('dashboard parity', () => {
  it('does not render missing KPIs as zero or as an empty-order prompt', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json({ code: 'Application.ValidationFailed' }, { status: 400 })));
    renderDashboard();
    expect(await screen.findByText('Không thể tải tổng quan')).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Tóm tắt đơn hàng' })).not.toBeInTheDocument();
    expect(screen.queryByText('Chưa có đơn hàng')).not.toBeInTheDocument();
  });
  it('keeps KPI values when latest shipments fail and preserves drill-down range', async () => {
    vi.stubGlobal('fetch', vi.fn((path: string) => Promise.resolve(path.includes('/dashboard') ? Response.json(kpi) : Response.json({ code: 'Application.ValidationFailed' }, { status: 400 }))));
    renderDashboard();
    expect(await screen.findByText('Không thể tải đơn hàng mới nhất')).toBeInTheDocument();
    const link = screen.getByRole('link', { name: /Đã giao/ });
    const query = new URLSearchParams(link.getAttribute('href')!.split('?')[1]);
    expect(query.get('status')).toBe('Delivered'); expect(query.get('shopId')).toBe(shopId);
    expect(query.get('toUtc')).toBe('2026-10-07T23:59:59.9999999Z');
    expect(screen.queryByText('Chưa có đơn hàng')).not.toBeInTheDocument();
  });
  it('hides COD for restricted staff, inactive mutations, and a redundant single-Shop selector', async () => {
    vi.stubGlobal('fetch', vi.fn((path: string) => Promise.resolve(Response.json(path.includes('/dashboard') ? kpi : { items: [], totalCount: 0, pageNumber: 1, pageSize: 5, totalPages: 0 }))));
    renderDashboard({ permissions: ['ViewShipments'], active: false });
    await screen.findByRole('region', { name: 'Tóm tắt đơn hàng' });
    expect(screen.queryByText('COD chờ thu')).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Tạo đơn hàng' })).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Shop hiện tại' })).not.toBeInTheDocument();
    expect(screen.getAllByText(/tạm ngưng/).length).toBeGreaterThan(0);
  });
  it('hides creation for an inactive Shop even when ManageShipments is granted', async () => {
    vi.stubGlobal('fetch', vi.fn((path: string) => Promise.resolve(Response.json(path.includes('/dashboard') ? kpi : { items: [], totalCount: 0, pageNumber: 1, pageSize: 5, totalPages: 0 }))));
    renderDashboard({ active: false });
    await screen.findByRole('region', { name: 'Tóm tắt đơn hàng' });
    expect(screen.getByText('COD chờ thu')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Tạo đơn hàng' })).not.toBeInTheDocument();
    expect(screen.getAllByText(/tạm ngưng/).length).toBeGreaterThan(0);
  });
});
