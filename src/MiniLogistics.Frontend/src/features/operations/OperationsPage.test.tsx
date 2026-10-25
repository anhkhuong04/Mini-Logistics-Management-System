import { screen, waitFor, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../test/renderWithProviders';
import { OperationsPage } from './OperationsPage';
import { OperationsFilters } from './OperationsFilters';
const session = { userId: 'ops-user', displayName: 'Operator', roles: ['Operator'], operationPermissions: [
  'operations.shipment.view', 'operations.assignment.assign', 'operations.assignment.bulk_retry_auto', 'operations.assignment.retry_auto'], locale: 'vi-VN', serverTimeZoneId: 'UTC' };
const pending = { shipmentId: 'id-1', trackingCode: 'ML-PENDING', receiverName: 'Receiver', pickupProvince: 'Hà Nội',
  deliveryProvince: 'Hà Nội', codAmount: '1000', shippingFeeAmount: '25000', currency: 'VND', createdAtUtc: '2026-10-01T00:00:00Z', isSlaOverdue: true };
afterEach(() => vi.unstubAllGlobals());
function setup(write: () => Promise<Response>, items = [pending]) {
  const fetch = vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/auth/csrf')) return Response.json({ requestToken: 'test' });
    if (options?.method === 'POST') return write();
    if (path.includes('/shippers')) return Response.json([]);
    return Response.json({ items, pageNumber: 1, pageSize: 25, totalCount: items.length, totalPages: 1 });
  });
  vi.stubGlobal('fetch', fetch); return fetch;
}
it('keeps partial bulk results visible and clears submitted selection', async () => {
  setup(async () => Response.json({ requestedCount: 2, retriedCount: 2, assignedCount: 1, skippedCount: 0,
    items: [{ shipmentId: 'id-1', trackingCode: 'ML-PENDING', status: 'Assigned', result: 'Assigned', reason: 'Assigned to driver' },
      { shipmentId: 'id-2', trackingCode: 'ML-PENDING-2', status: 'PendingPickup', result: 'NoEligibleShipper', reason: 'No capacity' }] }),
    [pending, { ...pending, shipmentId: 'id-2', trackingCode: 'ML-PENDING-2' }]);
  const { user } = renderWithProviders(<OperationsPage />, { session });
  await screen.findByText('ML-PENDING');
  await user.click(screen.getByRole('checkbox', { name: 'Chọn ML-PENDING' }));
  await user.click(screen.getByRole('checkbox', { name: 'Chọn ML-PENDING-2' }));
  await user.click(screen.getByRole('button', { name: /Retry tự động các đơn đã chọn/ }));
  await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Xác nhận' }));
  await screen.findByText('NoEligibleShipper: No capacity');
  await waitFor(() => expect(screen.getByRole('button', { name: 'Retry ML-PENDING-2' }).closest('tr')).toHaveTextContent('No capacity'));
  expect(screen.getByRole('button', { name: /Retry tự động các đơn đã chọn/ })).toBeDisabled();
  expect(screen.queryByRole('tab', { name: /Đối soát/ })).not.toBeInTheDocument();
});
it('blocks replay after an unknown mutation outcome until authoritative reload', async () => {
  const fetch = setup(async () => { throw new TypeError('offline'); });
  const { user } = renderWithProviders(<OperationsPage />, { session }); await screen.findByText('ML-PENDING');
  await user.click(screen.getByRole('button', { name: 'Retry ML-PENDING' }));
  await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Xác nhận' }));
  await screen.findByText(/Chưa xác định kết quả/);
  expect(screen.getByRole('button', { name: 'Retry ML-PENDING' })).toBeDisabled();
  expect(fetch.mock.calls.filter(([, options]) => options?.method === 'POST')).toHaveLength(1);
  await user.click(screen.getByRole('button', { name: 'Làm mới' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Retry ML-PENDING' })).toBeEnabled());
});
it('keeps a stale-capacity conflict blocked when reload fails, then unlocks on a successful read', async () => {
  let failRead = false;
  const fetch = vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/auth/csrf')) return Response.json({ requestToken: 'test' });
    if (options?.method === 'POST') { failRead = true; return Response.json({ code: 'Application.CapacityReached', title: 'Reload' }, { status: 409 }); }
    if (failRead) return Response.json({ code: 'Browser.RateLimitExceeded', title: 'Read temporarily limited' }, { status: 429 });
    if (path.includes('/shippers')) return Response.json([]);
    return Response.json({ items: [pending], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 });
  }); vi.stubGlobal('fetch', fetch);
  const { user } = renderWithProviders(<OperationsPage />, { session }); await screen.findByText('ML-PENDING');
  await user.click(screen.getByRole('button', { name: 'Retry ML-PENDING' }));
  await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Xác nhận' }));
  await screen.findByText(/Dữ liệu hoặc sức chứa đã thay đổi/);
  expect(screen.getByRole('button', { name: 'Retry ML-PENDING' })).toBeDisabled();
  await user.click(screen.getByRole('button', { name: 'Làm mới' }));
  await screen.findByText('Không tải được danh sách. Làm mới để thử lại.');
  failRead = false; await user.click(screen.getByRole('button', { name: 'Làm mới' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Retry ML-PENDING' })).toBeEnabled());
  expect(fetch.mock.calls.filter(([, options]) => options?.method === 'POST')).toHaveLength(1);
});
it('keeps the reassign combobox labelled separately from the shipper filter', async () => {
  const item = { ...pending, status: 'Assigned', codStatus: 'PendingCollection', receiverPhone: '0901234567',
    activeShipperId: 'driver-a', activeShipperName: 'Driver A', activeShipperPhone: null, trackingHistory: [],
    pickupAddress: { province: 'Hà Nội', fullAddress: 'Pickup' }, deliveryAddress: { province: 'Hà Nội', fullAddress: 'Delivery' } };
  vi.stubGlobal('fetch', vi.fn(async (path: string) => path.includes('/shippers') ? Response.json([])
    : Response.json({ items: [item], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 })));
  const { user } = renderWithProviders(<OperationsPage />, { path: '/operations/assignments?view=operations',
    session: { ...session, operationPermissions: [...session.operationPermissions, 'operations.assignment.reassign'] } });
  await user.click(screen.getByText('Bộ lọc nâng cao'));
  expect(screen.getByRole('combobox', { name: 'Shipper' })).toBeInTheDocument();
  await user.click(await screen.findByRole('button', { name: 'Đổi shipper ML-PENDING' }));
  expect(within(screen.getByRole('dialog')).getByRole('combobox', { name: 'Chọn shipper' })).toBeInTheDocument();
});
it.each(['minCodAmount', 'maxCodAmount'] as const)('normalizes a cleared %s rather than serializing null into the URL', async field => {
  const apply = vi.fn();
  const { user } = renderWithProviders(<OperationsFilters timeZone="UTC" filters={{ view: 'pending', pageNumber: 1, pageSize: 25, slaOnly: false, [field]: '100' }} shippers={[]} onApply={apply} />);
  await user.clear(screen.getByRole('spinbutton', { name: field === 'minCodAmount' ? 'COD tối thiểu' : 'COD tối đa' }));
  await user.click(screen.getByRole('button', { name: 'Áp dụng' }));
  await waitFor(() => expect(apply).toHaveBeenCalled());
  expect(apply.mock.calls[0][0][field]).toBeUndefined();
});
it('offers only the COD status filters supported by the legacy Operations workspace', async () => {
  const { user } = renderWithProviders(<OperationsFilters timeZone="UTC" filters={{ view: 'operations', pageNumber: 1, pageSize: 25, slaOnly: false }} shippers={[]} onApply={vi.fn()} />);
  await user.click(screen.getByText('Bộ lọc nâng cao'));
  await user.click(screen.getByRole('combobox', { name: 'Trạng thái COD' }));
  expect(await screen.findByTitle('PendingCollection')).toBeInTheDocument();
  expect(screen.queryByTitle('NotRequired')).not.toBeInTheDocument();
});
it('does not unlock a new unknown failure when an older refresh finishes its shipper read', async () => {
  let finishShippers!: (value: Response) => void; let shipperReads = 0;
  const delayed = new Promise<Response>(resolve => { finishShippers = resolve; });
  const items = [pending, { ...pending, shipmentId: 'id-2', trackingCode: 'ML-PENDING-2' }];
  vi.stubGlobal('fetch', vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/auth/csrf')) return Response.json({ requestToken: 'test' });
    if (options?.method === 'POST') throw new TypeError('unknown outcome');
    if (path.includes('/shippers')) return ++shipperReads === 2 ? delayed : Response.json([]);
    return Response.json({ items, pageNumber: 1, pageSize: 25, totalCount: 2, totalPages: 1 });
  }));
  const { user } = renderWithProviders(<OperationsPage />, { session });
  await user.click(await screen.findByRole('button', { name: 'Retry ML-PENDING' }));
  await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Xác nhận' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Retry ML-PENDING' })).toBeDisabled());
  await user.click(screen.getByRole('button', { name: 'Làm mới' }));
  await user.click(await screen.findByRole('button', { name: 'Retry ML-PENDING-2' }));
  await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Xác nhận' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Retry ML-PENDING-2' })).toBeDisabled());
  finishShippers(Response.json([]));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Retry ML-PENDING' })).toBeEnabled());
  expect(screen.getByRole('button', { name: 'Retry ML-PENDING-2' })).toBeDisabled();
});
