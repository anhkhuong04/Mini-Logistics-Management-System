import { screen, within, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../test/renderWithProviders';
import { resetCsrf } from '../../shared/api/httpClient';
import { RequireSession } from '../../shared/auth/RequireSession';
import { AdminBannersPage } from './AdminBannersPage';
import { AdminShopsPage } from './AdminShopsPage';
import { AdminUsersPage } from './AdminUsersPage';
import { safeReturnUrl } from '../auth/redirects';
const session = { userId: 'admin', displayName: 'Admin', roles: ['Admin'], operationPermissions: [], locale: 'vi-VN', serverTimeZoneId: 'Asia/Bangkok' };
afterEach(() => { vi.unstubAllGlobals(); resetCsrf(); });
it('keeps only migrated administration destinations under the SPA', () => {
  for (const path of ['/admin', '/admin/dashboard', '/admin/users', '/admin/shops', '/admin/hubs', '/admin/system-config', '/admin/cod', '/admin/audit-logs', '/admin/banners']) expect(safeReturnUrl(path)).toBe(`/app${path}`);
  expect(safeReturnUrl('/admin/invented')).toBeNull();
});
it('denies non-Admin before querying administrative data', async () => {
  const fetch = vi.fn(); vi.stubGlobal('fetch', fetch);
  renderWithProviders(<RequireSession roles={['Admin']}><AdminShopsPage /></RequireSession>, { session: { ...session, roles: ['Operator'] } });
  expect(screen.queryByRole('heading', { name: 'Quản trị Shop' })).not.toBeInTheDocument(); expect(fetch).not.toHaveBeenCalled();
});
it.each([0, 409])('confirms a Shop toggle and blocks replay after failure %s until refreshed', async status => {
  const fetch = vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/csrf')) return Response.json({ requestToken: 'test' });
    if (options?.method === 'PUT') {
      if (status === 409) return Response.json({ code: 'Application.Conflict', title: 'State changed' }, { status });
      throw new TypeError('offline');
    }
    return Response.json([{ shopId: 'shop', name: 'Test Shop', ownerFullName: 'Owner', ownerEmail: 'owner@example.test', phoneNumber: '0901234567', addressLine: 'Street', ward: 'Ward', province: 'Province', isActive: true }]);
  }); vi.stubGlobal('fetch', fetch);
  const { user } = renderWithProviders(<AdminShopsPage />, { session });
  await user.click(await screen.findByRole('button', { name: 'Đổi hoạt động Test Shop' }));
  expect(fetch.mock.calls.some(([, o]) => o?.method === 'PUT')).toBe(false);
  await user.type(within(await screen.findByRole('dialog')).getByRole('textbox', { name: 'Lý do thay đổi' }), 'Temporary pause');
  await user.dblClick(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Xác nhận' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Đổi hoạt động Test Shop' })).toBeDisabled());
  expect(fetch.mock.calls.filter(([, o]) => o?.method === 'PUT')).toHaveLength(1);
  expect(JSON.parse(String(fetch.mock.calls.find(([, o]) => o?.method === 'PUT')![1]?.body)).reason).toBe('Temporary pause');
  await user.click(screen.getByRole('button', { name: 'Làm mới' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Đổi hoạt động Test Shop' })).toBeEnabled());
});
it('clears the current banner image after failed upload and never saves metadata', async () => {
  const fetch = vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/csrf')) return Response.json({ requestToken: 'test' });
    if (options?.method === 'POST') return Response.json({ code: 'Application.ValidationFailed', title: 'Invalid image' }, { status: 400 });
    return Response.json([{ id: 'banner', title: 'Existing', imageUrl: '/images/banners/existing.png', linkUrl: null, sortOrder: 0, isActive: true }]);
  }); vi.stubGlobal('fetch', fetch);
  const { user, container } = renderWithProviders(<AdminBannersPage />, { session });
  await user.click(await screen.findByRole('button', { name: 'Sửa Existing' }));
  expect(screen.getByRole('textbox', { name: 'Đường dẫn ảnh' })).toHaveValue('/images/banners/existing.png');
  await user.upload(container.querySelector('input[type=file]')!, new File(['invalid'], 'bad.png', { type: 'image/png' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Upload thất bại');
  expect(screen.getByRole('textbox', { name: 'Đường dẫn ảnh' })).toHaveValue('');
  expect(screen.getByRole('button', { name: 'Lưu banner' })).toBeDisabled();
  expect(fetch.mock.calls.filter(([p, o]) => p.endsWith('/banners') && o?.method === 'POST')).toHaveLength(0);
});
it('clears internal user password before confirmation without putting it in the dialog', async () => {
  vi.stubGlobal('fetch', vi.fn(async (path: string) => Response.json(path.includes('/users') ? { items: [], totalCount: 0, pageNumber: 1, pageSize: 25 } : [])));
  const { user } = renderWithProviders(<AdminUsersPage />, { session });
  await user.type(screen.getByRole('textbox', { name: 'Họ tên' }), 'New Driver');
  await user.type(screen.getByRole('textbox', { name: 'Email' }), 'new@example.test');
  await user.type(screen.getByRole('textbox', { name: 'Điện thoại' }), '0901234567');
  const password = screen.getByLabelText(/Mật khẩu khởi tạo/); await user.type(password, 'GoodPass!1');
  await user.click(screen.getByRole('button', { name: 'Tạo người dùng' }));
  expect(await screen.findByRole('dialog')).not.toHaveTextContent('GoodPass!1'); expect(password).toHaveValue('');
});
