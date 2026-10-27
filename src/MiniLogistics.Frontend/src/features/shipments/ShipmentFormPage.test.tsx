import { screen, waitFor } from '@testing-library/react';
import { useState } from 'react';
import { afterEach, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../test/renderWithProviders';
import { ShopContextState } from '../shop-context';
import { ShipmentFormPage } from './ShipmentFormPage';
import { resetCsrf } from '../../shared/api/httpClient';
import type { ShipmentWrite } from './shipmentFormMapping';
const address = { street: 'Street', ward: 'Ward', province: 'Province', country: 'Vietnam' };
const values: ShipmentWrite = {
  senderName: 'Sender',
  senderPhone: '0901234567',
  receiverName: 'Receiver',
  receiverPhone: '0901234567',
  pickupAddress: address,
  deliveryAddress: address,
  weightKg: '1',
  lengthCm: '10',
  widthCm: '10',
  heightCm: '10',
  goodsValueAmount: '0',
  codAmount: '0',
  currency: 'VND',
};
vi.mock('./ShipmentForm', () => ({
  ShipmentForm: ({
    onSubmit,
    blocked,
  }: {
    onSubmit(values: ShipmentWrite, mode: string): Promise<void>;
    blocked: boolean;
  }) => (
    <button
      disabled={blocked}
      onClick={() => {
        void onSubmit(values, 'send');
      }}
    >
      Create command
    </button>
  ),
}));
const shopId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const session = {
  userId: 'user',
  displayName: 'User',
  roles: ['Shop'],
  operationPermissions: [],
  locale: 'vi-VN',
  serverTimeZoneId: 'UTC',
};
afterEach(() => {
  vi.unstubAllGlobals();
  resetCsrf();
});
function renderPage(path = `/shipments/create?shopId=${shopId}`) {
  function Workspace() {
    const [full, setFull] = useState(true);
    return (
      <>
        <button onClick={() => setFull(false)}>Revoke PII</button>
        <ShopContextState.Provider
          value={{
            context: {
              selectedShopId: shopId,
              shops: [
                {
                  shopId,
                  name: 'Shop A',
                  permissions: [
                    'ViewShipments',
                    'ManageShipments',
                    ...(full ? ['ViewFullPii'] : []),
                    'ViewCod',
                  ],
                  isOwner: true,
                  isActive: true,
                  phoneNumber: '0901234567',
                  addressLine: 'Street',
                  ward: 'Ward',
                  province: 'Province',
                  country: 'Vietnam',
                  staffRole: null,
                },
              ],
            },
            loading: false,
            error: null,
            selectShop: vi.fn(),
            reload: vi.fn(),
          }}
        >
          <ShipmentFormPage />
        </ShopContextState.Provider>
      </>
    );
  }
  return renderWithProviders(<Workspace />, { session, path });
}
it('treats a create 409 as an uncertain result and directs to the list without fetching an empty detail ID', async () => {
  const fetch = vi.fn((path: string) =>
    Promise.resolve(
      path.endsWith('/auth/csrf')
        ? Response.json({ requestToken: 'test' })
        : Response.json({ code: 'Application.ConcurrencyConflict', title: 'Conflict' }, { status: 409 }),
    ),
  );
  vi.stubGlobal('fetch', fetch);
  const { user } = renderPage();
  await user.click(screen.getByRole('button', { name: 'Create command' }));
  expect(await screen.findByRole('link', { name: 'Danh sách vận đơn' })).toHaveAttribute(
    'href',
    `/shipments?shopId=${shopId}`,
  );
  expect(screen.getByRole('button', { name: 'Create command' })).toBeDisabled();
  expect(screen.queryByRole('button', { name: 'Tải lại dữ liệu' })).not.toBeInTheDocument();
  expect(fetch.mock.calls.some(([path]) => path.endsWith('/shipments/'))).toBe(false);
});
it('discards a draft submit confirmation and its private snapshot when PII permission is revoked', async () => {
  const id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
  const fetch = vi.fn().mockResolvedValue(
    Response.json({
      ...values,
      shipmentId: id,
      status: 'Draft',
      parcelLengthCm: '10',
      parcelWidthCm: '10',
      parcelHeightCm: '10',
    }),
  );
  vi.stubGlobal('fetch', fetch);
  const { user } = renderPage(`/shipments/create?shopId=${shopId}&shipmentId=${id}`);
  await user.click(await screen.findByRole('button', { name: 'Create command' }));
  expect(screen.getByRole('dialog', { name: 'Gửi đơn nháp?' })).toBeInTheDocument();
  await user.click(screen.getByRole('button', { name: 'Revoke PII' }));
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Create command' })).not.toBeInTheDocument();
  expect(screen.getByRole('alert')).toHaveTextContent('Thông tin đã được che');
});
it('refreshes the saved draft when update succeeds but the following submit fails', async () => {
  const id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
  const detail = {
    ...values,
    shipmentId: id,
    status: 'Draft',
    parcelLengthCm: '10',
    parcelWidthCm: '10',
    parcelHeightCm: '10',
  };
  const fetch = vi.fn((path: string, options?: RequestInit) =>
    Promise.resolve(
      path.endsWith('/auth/csrf')
        ? Response.json({ requestToken: 'test' })
        : options?.method === 'POST'
          ? Response.json(
              { code: 'Application.ValidationFailed', title: 'Submission failed' },
              { status: 400 },
            )
          : Response.json(detail),
    ),
  );
  vi.stubGlobal('fetch', fetch);
  const { user } = renderPage(`/shipments/create?shopId=${shopId}&shipmentId=${id}`);
  await user.click(await screen.findByRole('button', { name: 'Create command' }));
  await user.click(screen.getByRole('button', { name: 'Xác nhận' }));
  await screen.findByRole('alert');
  await waitFor(() =>
    expect(
      fetch.mock.calls.filter(([path, options]) => path.endsWith(`/${id}`) && options?.method === 'GET'),
    ).toHaveLength(2),
  );
});
