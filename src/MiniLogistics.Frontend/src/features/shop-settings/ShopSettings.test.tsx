import { screen, within, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import type { ReactNode } from 'react';
import { renderWithProviders } from '../../test/renderWithProviders';
import { ShopContextState, type ShopContext } from '../shop-context/ShopContext';
import { resetCsrf } from '../../shared/api/httpClient';
import { ShopProfilePage } from './ShopProfilePage';
import { ShopStaffPage } from './ShopStaffPage';
import { ShopCodReportPage } from './ShopCodReportPage';
import { ShopNotificationsPage } from './ShopNotificationsPage';
import { ShopAuditPage } from './ShopAuditPage';
const id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const session = { userId: 'settings-user', displayName: 'Shop', roles: ['Shop'], operationPermissions: [], locale: 'vi-VN', serverTimeZoneId: 'Asia/Bangkok' };
const profile = { shopId: id, name: 'Test Shop', phoneNumber: '0901234567', addressLine: 'Street', ward: 'Ward', province: 'Province', country: 'Vietnam', isActive: true, createdAtUtc: '2026-10-25T00:00:00Z', updatedAtUtc: null };
const staff = { membershipId: 'membership-id', userId: 'staff-user-id', fullName: 'Staff A', email: 'staff@example.test', phoneNumber: null, role: 'Viewer', permissions: ['ViewShipments'], isActive: true, createdAtUtc: '2026-10-25T00:00:00Z' };
const full = ['ViewShipments', 'ManageShopProfile', 'ViewFullPii', 'ViewCod', 'ViewAudit', 'ManageNotifications', 'ExportData'];
function wrap(ui: ReactNode, permissions = full, isOwner = false) {
  const context: ShopContext = { selectedShopId: id, shops: [{ ...profile, isOwner, staffRole: 'Custom', permissions }] };
  return <ShopContextState.Provider value={{ context, loading: false, error: null, reload: vi.fn(), selectShop: vi.fn() }}>{ui}</ShopContextState.Provider>;
}
function setup(write: (path: string, options: RequestInit) => Promise<Response> = async () => new Response(null, { status: 204 })) {
  const fetch = vi.fn(async (path: string, options?: RequestInit): Promise<Response> => {
    if (path.includes('/auth/csrf')) return Response.json({ requestToken: 'test' });
    if (options?.method === 'POST' || options?.method === 'PUT') return write(path, options);
    if (path.includes('/reference-data/')) return Response.json({ provinces: [{ name: 'Province', wards: [{ name: 'Ward' }] }] });
    if (path.endsWith('/profile')) return Response.json(profile);
    if (path.endsWith('/staff')) return Response.json([staff]);
    if (path.includes('/notifications/preferences')) return Response.json({ shopId: id, enabledEventTypes: ['cod.collected'] });
    if (path.includes('/notifications')) return Response.json([{ notificationId: 'notice-1', shopId: id, title: 'New delivery', message: 'Delivered', eventType: 'shipment.delivered', isRead: false, createdAtUtc: '2026-10-25T00:00:00Z' }]);
    if (path.includes('/cod-report')) return Response.json({ shopId: id, pendingCollectionAmount: '9007199254740993.01', collectedAmount: '0', settledAmount: '0', discrepancyAmount: '0', currency: 'VND', rows: [] });
    return Response.json([]);
  }); vi.stubGlobal('fetch', fetch); return fetch;
}
afterEach(() => { vi.unstubAllGlobals(); resetCsrf(); });
it('keeps masked profiles read-only even when profile management is granted', async () => {
  setup(); renderWithProviders(wrap(<ShopProfilePage />, ['ViewShipments', 'ManageShopProfile']), { session });
  expect(await screen.findByText('Hồ sơ chỉ đọc theo quyền hiện tại')).toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Lưu hồ sơ' })).not.toBeInTheDocument();
  expect(screen.queryByRole('textbox', { name: 'Điện thoại' })).not.toBeInTheDocument();
});
it('confirms profile writes and blocks unknown replay until a successful refresh', async () => {
  const fetch = setup(async () => { throw new TypeError('offline'); }); const { user } = renderWithProviders(wrap(<ShopProfilePage />), { session });
  const name = await screen.findByRole('textbox', { name: 'Tên Shop' }); await user.clear(name); await user.type(name, 'Updated Shop');
  await user.click(screen.getByRole('button', { name: 'Lưu hồ sơ' }));
  const dialog = await screen.findByRole('dialog'); expect(fetch.mock.calls.some(([, options]) => options?.method === 'PUT')).toBe(false);
  await user.dblClick(within(dialog).getByRole('button', { name: 'Xác nhận' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Lưu hồ sơ' })).toBeDisabled());
  const writes = fetch.mock.calls.filter(([, options]) => options?.method === 'PUT'); expect(writes).toHaveLength(1);
  expect(JSON.parse(String(writes[0][1]?.body)).name).toBe('Updated Shop');
  await user.click(screen.getByRole('button', { name: 'Làm mới' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Lưu hồ sơ' })).toBeEnabled());
});
it('maps staff edits to UserId and asks before applying permissions', async () => {
  const fetch = setup(async () => Response.json(staff)); const { user } = renderWithProviders(wrap(<ShopStaffPage />, full, true), { session });
  await user.click(await screen.findByRole('button', { name: 'Sửa Staff A' }));
  await user.click(screen.getByRole('switch', { name: 'Nhân viên hoạt động' }));
  await user.click(screen.getByRole('button', { name: 'Lưu quyền' }));
  await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Xác nhận' }));
  await waitFor(() => expect(fetch.mock.calls.some(([path, options]) => path.endsWith('/staff/staff-user-id') && options?.method === 'PUT')).toBe(true));
  expect(fetch.mock.calls.some(([path]) => path.includes('/staff/membership-id'))).toBe(false);
});
it('validates staff password complexity and clears it before the confirmation request', async () => {
  const fetch = setup(async () => Response.json(staff)); const { user } = renderWithProviders(wrap(<ShopStaffPage />, full, true), { session });
  await user.type(await screen.findByRole('textbox', { name: 'Họ tên', exact: true }), 'New Staff');
  await user.type(screen.getByRole('textbox', { name: 'Email nhân viên' }), 'new@example.test');
  await user.type(screen.getByRole('textbox', { name: 'Điện thoại nhân viên' }), '0901234567');
  const password = screen.getByLabelText(/Mật khẩu khởi tạo/); await user.type(password, 'weak-pass1');
  await user.click(screen.getByRole('button', { name: 'Tạo nhân viên', exact: true }));
  expect(await screen.findByText('Mật khẩu cần chữ hoa, chữ thường, số và ký tự đặc biệt')).toBeInTheDocument();
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  await user.clear(password); await user.type(password, 'GoodPass!1'); await user.click(screen.getByRole('button', { name: 'Tạo nhân viên', exact: true }));
  const dialog = await screen.findByRole('dialog'); expect(password).toHaveValue(''); expect(dialog).not.toHaveTextContent('GoodPass!1');
  await user.click(within(dialog).getByRole('button', { name: 'Xác nhận' }));
  await waitFor(() => expect(fetch.mock.calls.some(([, options]) => options?.method === 'POST')).toBe(true));
  expect(JSON.parse(String(fetch.mock.calls.find(([, options]) => options?.method === 'POST')![1]?.body))).toMatchObject({ password: 'GoodPass!1', role: 'Viewer', permissions: ['ViewShipments'] });
});
it('denies staff administration before reading staff data for a non-owner', async () => {
  const fetch = setup(); renderWithProviders(wrap(<ShopStaffPage />), { session });
  expect(await screen.findByText('Không có quyền truy cập Shop')).toBeInTheDocument(); expect(fetch).not.toHaveBeenCalled();
});
it('renders exact report values and separates ViewCod from ExportData', async () => {
  setup(); renderWithProviders(wrap(<ShopCodReportPage />, ['ViewShipments', 'ViewCod']), { session });
  expect(await screen.findByText('9.007.199.254.740.993,01 VND')).toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Xuất COD CSV' })).not.toBeInTheDocument();
});
it('does not load preferences for notification readers without management permission', async () => {
  const fetch = setup(); const { user } = renderWithProviders(wrap(<ShopNotificationsPage />, ['ViewShipments']), { session });
  await user.click(await screen.findByRole('button', { name: 'Đã đọc New delivery' }));
  await waitFor(() => expect(fetch.mock.calls.some(([path, options]) => path.endsWith('/notifications/notice-1/read') && options?.method === 'POST')).toBe(true));
  expect(fetch.mock.calls.some(([path]) => path.includes('preferences'))).toBe(false);
});
it('keeps invalid audit URLs from issuing reads', async () => {
  const fetch = setup(); renderWithProviders(wrap(<ShopAuditPage />), { session, path: '/shop/audit?limit=999' });
  expect(await screen.findByText('Bộ lọc không hợp lệ')).toBeInTheDocument(); expect(fetch).not.toHaveBeenCalled();
});
it('shows report errors without fabricating financial zeroes', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response(null, { status: 429 })));
  renderWithProviders(wrap(<ShopCodReportPage />), { session });
  expect(await screen.findByText('Không tải được báo cáo COD')).toBeInTheDocument(); expect(screen.queryByText('0 VND')).not.toBeInTheDocument();
});
it('keeps unsaved notification preferences while marking a notification as read', async () => {
  setup(); const { user } = renderWithProviders(wrap(<ShopNotificationsPage />), { session });
  const collected = await screen.findByRole('checkbox', { name: 'cod.collected' }); expect(collected).toBeChecked();
  await user.click(collected); await user.click(await screen.findByRole('button', { name: 'Đã đọc New delivery' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Đã đọc New delivery' })).toBeEnabled());
  expect(screen.getByRole('checkbox', { name: 'cod.collected' })).not.toBeChecked();
});
