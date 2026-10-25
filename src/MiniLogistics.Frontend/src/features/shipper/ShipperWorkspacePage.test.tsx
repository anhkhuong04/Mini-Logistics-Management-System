import { screen, waitFor, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../test/renderWithProviders';
import { ShipperWorkspacePage } from './ShipperWorkspacePage';
const session = { userId: 'shipper-user', displayName: 'Shipper', roles: ['Shipper'], operationPermissions: [], locale: 'vi-VN', serverTimeZoneId: 'Asia/Bangkok' };
const item = { shipmentId: 'id-1', trackingCode: 'ML-SHIPPER', senderName: 'Sender', senderPhone: '0901234567', receiverName: 'Receiver', receiverPhone: '0907654321',
  pickupAddress: { fullAddress: 'Pickup', province: 'Province' }, deliveryAddress: { fullAddress: 'Delivery', province: 'Province' }, codAmount: '9007199254740993.01',
  codStatus: 'PendingCollection', shippingFeeAmount: '25000', currency: 'VND', status: 'Delivered', createdAtUtc: '2026-10-25T00:00:00Z', trackingHistory: [] };
afterEach(() => vi.unstubAllGlobals());
it('requires a discrepancy note and submits exact actual COD only after confirmation', async () => {
  const fetch = setup(async () => new Response(null, { status: 204 }));
  const { user } = renderWithProviders(<ShipperWorkspacePage />, { session });
  const amount = await screen.findByRole('textbox', { name: 'Số thực thu' });
  expect(amount).toHaveValue('9007199254740993.01');
  for (const invalid of ['-1', '1.234', '10000000000000000']) {
    await user.clear(amount); await user.type(amount, invalid);
    await user.click(screen.getByRole('button', { name: 'Thu COD ML-SHIPPER' }));
    expect(await screen.findByText('Nhập số tiền không âm, tối đa 16 số nguyên và 2 số lẻ')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  }
  await user.clear(amount); await user.type(amount, '9007199254740992.01');
  await user.click(screen.getByRole('button', { name: 'Thu COD ML-SHIPPER' }));
  expect(await screen.findByText('Nhập ghi chú khi lệch COD')).toBeInTheDocument();
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  await user.type(screen.getByRole('textbox', { name: 'Ghi chú thu COD' }), 'Receiver paid less');
  await user.click(screen.getByRole('button', { name: 'Thu COD ML-SHIPPER' }));
  expect(screen.getByRole('dialog')).toHaveTextContent('9.007.199.254.740.992,01 VND');
  expect(fetch.mock.calls.filter(([, options]) => options?.method === 'POST')).toHaveLength(0);
  await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Xác nhận' }));
  await waitFor(() => expect(fetch.mock.calls.filter(([, options]) => options?.method === 'POST')).toHaveLength(1));
  expect(JSON.parse(String(fetch.mock.calls.find(([, options]) => options?.method === 'POST')![1]?.body))).toEqual({ collectedAmount: '9007199254740992.01', note: 'Receiver paid less' });
});
function setup(write: (path: string, options?: RequestInit) => Promise<Response>, items = [item]) {
  let available = true;
  const fetch = vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/auth/csrf')) return Response.json({ requestToken: 'test' });
    if (options?.method === 'POST' || options?.method === 'PUT') { const result = await write(path, options); if (path.endsWith('/availability') && result.ok) available = false; return result; }
    if (path.endsWith('/profile')) return Response.json({ userId: session.userId, fullName: 'Driver', phoneNumber: null, activeShipmentCount: 9, maxActiveShipments: 30, isAvailableForAssignment: available });
    if (path.endsWith('/working-areas')) return Response.json([]);
    if (path.includes('/cod-summary')) return Response.json({ shipperUserId: session.userId, businessDate: '2026-10-25', pendingCollectionAmount: '9007199254740993.01', pendingCollectionCount: 1, collectedTodayAmount: '0', collectedTodayCount: 0, currency: 'VND' });
    return Response.json({ items, pageNumber: 2, pageSize: 20, totalCount: 21, totalPages: 2 });
  }); vi.stubGlobal('fetch', fetch); return fetch;
}
it('uses server paging and aggregate capacity, keeps exact COD and never fetches all shippers', async () => {
  const fetch = setup(async () => new Response(null, { status: 204 }));
  renderWithProviders(<ShipperWorkspacePage />, { session, path: '/shipper/shipments?stage=CodPending&pageNumber=2' });
  await screen.findByRole('heading', { name: 'ML-SHIPPER' });
  expect(screen.getByText('9/30')).toBeInTheDocument();
  expect(screen.getAllByText('9.007.199.254.740.993,01 VND').length).toBeGreaterThan(0);
  expect(screen.getByRole('button', { name: 'Thu COD ML-SHIPPER' })).toBeEnabled();
  const url = fetch.mock.calls.map(([path]) => String(path)).find(path => path.includes('/workspace'))!;
  expect(url).toContain('stage=CodPending'); expect(url).toContain('pageNumber=2');
  expect(fetch.mock.calls.some(([path]) => String(path).includes('/operations/shippers'))).toBe(false);
});
it('blocks duplicate and unknown collection until a successful workspace read', async () => {
  const fetch = setup(async () => { throw new TypeError('offline'); });
  const { user } = renderWithProviders(<ShipperWorkspacePage />, { session });
  await user.click(await screen.findByRole('button', { name: 'Thu COD ML-SHIPPER' }));
  await user.dblClick(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Xác nhận' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Thu COD ML-SHIPPER' })).toBeDisabled());
  expect(await screen.findByText(/Chưa xác định kết quả/)).toBeInTheDocument();
  expect(fetch.mock.calls.filter(([, options]) => options?.method === 'POST')).toHaveLength(1);
  await user.click(screen.getByRole('button', { name: 'Làm mới' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Thu COD ML-SHIPPER' })).toBeEnabled());
});
it('changes only own availability after confirmation and refreshes the profile', async () => {
  const fetch = setup(async () => new Response(null, { status: 204 }));
  const { user } = renderWithProviders(<ShipperWorkspacePage />, { session });
  await user.click(await screen.findByRole('button', { name: 'Tạm dừng nhận đơn' }));
  expect(fetch.mock.calls.some(([, options]) => options?.method === 'PUT')).toBe(false);
  await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Xác nhận' }));
  await screen.findByRole('button', { name: 'Nhận đơn tự động' });
  const request = fetch.mock.calls.find(([, options]) => options?.method === 'PUT')!;
  expect(JSON.parse(String(request[1]?.body))).toEqual({ isAvailableForAssignment: false });
});
it('requires a failure note and reason code before confirming a failed delivery', async () => {
  setup(async () => new Response(null, { status: 204 }), [{ ...item, status: 'Delivering' }]);
  const { user } = renderWithProviders(<ShipperWorkspacePage />, { session });
  await screen.findByRole('heading', { name: 'ML-SHIPPER' });
  await user.click(screen.getByRole('combobox', { name: 'Trạng thái mới' }));
  await user.click(await screen.findByTitle('Giao thất bại'));
  await user.click(screen.getByRole('button', { name: 'Cập nhật ML-SHIPPER' }));
  expect(await screen.findByText('Nhập lý do giao thất bại')).toBeInTheDocument();
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  await user.type(screen.getByRole('textbox', { name: 'Ghi chú' }), 'Receiver absent');
  await user.click(screen.getByRole('combobox', { name: 'Mã lý do' }));
  await user.click(await screen.findByTitle('Không có người nhận'));
  await user.click(screen.getByRole('button', { name: 'Cập nhật ML-SHIPPER' }));
  expect(await screen.findByRole('dialog')).toHaveTextContent('ML-SHIPPER');
});
it('keeps other shipment controls usable while one collection is pending', async () => {
  let finish!: (value: Response) => void; const pending = new Promise<Response>(resolve => { finish = resolve; });
  const items = [item, { ...item, shipmentId: 'id-2', trackingCode: 'ML-SHIPPER-2', status: 'Assigned' }];
  const fetch = setup(async () => pending, items);
  const { user } = renderWithProviders(<ShipperWorkspacePage />, { session });
  await user.click(await screen.findByRole('button', { name: 'Thu COD ML-SHIPPER' }));
  await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Xác nhận' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Thu COD ML-SHIPPER' })).toBeDisabled());
  expect(screen.getByRole('button', { name: 'Cập nhật ML-SHIPPER-2' })).toBeEnabled();
  expect(fetch.mock.calls.filter(([, options]) => options?.method === 'POST')).toHaveLength(1);
  items.splice(0, 1); finish(new Response(null, { status: 204 }));
  await waitFor(() => expect(screen.queryByRole('heading', { name: 'ML-SHIPPER' })).not.toBeInTheDocument());
});
it('shows a report failure without fabricating zero COD values', async () => {
  const fetch = setup(async () => new Response(null, { status: 204 })); const original = fetch.getMockImplementation()!;
  fetch.mockImplementation(async (path, options) => path.includes('/cod-summary') ? Response.json({ code: 'Browser.RateLimitExceeded', title: 'Limited' }, { status: 429 }) : original(path, options));
  renderWithProviders(<ShipperWorkspacePage />, { session });
  expect(await screen.findByText('Không tải được báo cáo COD. Làm mới để thử lại.')).toBeInTheDocument();
  expect(screen.queryByRole('region', { name: 'Báo cáo COD' })).not.toBeInTheDocument();
  expect(await screen.findByRole('heading', { name: 'ML-SHIPPER' })).toBeInTheDocument();
});
it('keeps a newer unknown outcome locked when an older profile refresh finishes', async () => {
  let finishProfile!: (value: Response) => void; const delayedProfile = new Promise<Response>(resolve => { finishProfile = resolve; }); let reads = 0;
  const fetch = setup(async () => { throw new TypeError('unknown'); }, [item, { ...item, shipmentId: 'id-2', trackingCode: 'ML-SHIPPER-2' }]);
  const original = fetch.getMockImplementation()!;
  fetch.mockImplementation(async (path, options) => path.endsWith('/profile') && ++reads === 2 ? delayedProfile : original(path, options));
  const { user } = renderWithProviders(<ShipperWorkspacePage />, { session });
  await user.click(await screen.findByRole('button', { name: 'Thu COD ML-SHIPPER' }));
  await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Xác nhận' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Thu COD ML-SHIPPER' })).toBeDisabled());
  await user.click(screen.getByRole('button', { name: 'Làm mới' }));
  await user.click(screen.getByRole('button', { name: 'Thu COD ML-SHIPPER-2' }));
  await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Xác nhận' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Thu COD ML-SHIPPER-2' })).toBeDisabled());
  finishProfile(Response.json({ userId: session.userId, fullName: 'Driver', phoneNumber: null, activeShipmentCount: 9, maxActiveShipments: 30, isAvailableForAssignment: true }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Thu COD ML-SHIPPER' })).toBeEnabled());
  expect(screen.getByRole('button', { name: 'Thu COD ML-SHIPPER-2' })).toBeDisabled();
});
