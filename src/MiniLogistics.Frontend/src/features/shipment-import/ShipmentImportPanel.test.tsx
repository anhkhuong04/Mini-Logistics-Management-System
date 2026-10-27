import { fireEvent, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../test/renderWithProviders';
import { ShopContextState } from '../shop-context';
import { resetCsrf } from '../../shared/api/httpClient';
import { ShipmentImportPanel } from './ShipmentImportPanel';
const shopId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const draft = {
  rowNumber: 2,
  clientOrderCode: 'CODE',
  receiverName: 'Receiver',
  receiverPhone: 'bad',
  deliveryStreet: 'Street',
  deliveryWard: 'Ward',
  deliveryProvince: 'Province',
  deliveryCountry: 'Vietnam',
  weightKg: '1',
  lengthCm: '10',
  widthCm: '10',
  heightCm: '10',
  goodsValueAmount: '0',
  codAmount: '9007199254740993.01',
  note: null,
};
const preview = {
  shopId,
  totalRows: 1,
  validRows: 0,
  invalidRows: 1,
  rows: [
    {
      draft,
      isValid: false,
      errors: ['Invalid phone'],
      routeType: null,
      shippingFeeAmount: null,
      chargeableWeightKg: null,
      currency: 'VND',
    },
  ],
};
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
function renderPanel() {
  return renderWithProviders(
    <ShopContextState.Provider
      value={{
        context: {
          selectedShopId: shopId,
          shops: [
            {
              shopId,
              name: 'Shop',
              permissions: ['ViewShipments', 'ManageShipments'],
              isActive: true,
              isOwner: false,
              phoneNumber: '***',
              addressLine: '***',
              ward: '',
              province: '',
              country: '',
              staffRole: 'Custom',
            },
          ],
        },
        loading: false,
        error: null,
        selectShop: vi.fn(),
        reload: vi.fn(),
      }}
    >
      <ShipmentImportPanel />
    </ShopContextState.Provider>,
    { session },
  );
}
// This interaction renders the real AntD editable table and collapses it twice; retain the same assertions under slower CI CPUs.
it('does not auto upload, submits edited exact rows once, and retains pending and unknown guards across collapse', async () => {
  let finish!: (response: Response) => void;
  const fetch = vi.fn((path: string) =>
    path.endsWith('/confirm')
      ? new Promise<Response>((done) => {
          finish = done;
        })
      : Promise.resolve(Response.json(path.endsWith('/csrf') ? { requestToken: 'test' } : preview)),
  );
  vi.stubGlobal('fetch', fetch);
  const { user, container } = renderPanel();
  await user.click(screen.getByRole('button', { name: 'Nhập CSV' }));
  await user.upload(
    container.querySelector('input[type="file"]')!,
    new File(['csv'], 'input.csv', { type: 'text/csv' }),
  );
  expect(fetch).not.toHaveBeenCalled();
  await user.click(screen.getByRole('button', { name: 'Kiểm tra CSV' }));
  await screen.findByText('Invalid phone');
  const receiver = screen.getByRole('textbox', { name: /Dòng 2 · Tên người nhận/ });
  fireEvent.change(receiver, { target: { value: 'Edited Receiver' } });
  await user.dblClick(screen.getByRole('button', { name: 'Xác nhận nhập' }));
  const calls = fetch.mock.calls.filter(([path]) => path.endsWith('/confirm'));
  expect(calls).toHaveLength(1);
  await user.click(screen.getByRole('button', { name: 'Đóng nhập CSV' }));
  await user.click(screen.getByRole('button', { name: 'Nhập CSV' }));
  expect(screen.getByRole('button', { name: 'Xác nhận nhập' })).toBeDisabled();
  const sent = JSON.parse(
    (fetch.mock.calls as unknown as Array<[string, RequestInit]>).find(([path]) =>
      path.endsWith('/confirm'),
    )![1].body as string,
  );
  expect(sent.rows[0].receiverName).toBe('Edited Receiver');
  expect(sent.rows[0].codAmount).toBe('9007199254740993.01');
  expect(sent).not.toHaveProperty('isValid');
  finish(Response.json({ code: 'Application.Unavailable', title: 'Unavailable' }, { status: 503 }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Chưa xác định được kết quả xác nhận');
  await user.click(screen.getByRole('button', { name: 'Đóng nhập CSV' }));
  await user.click(screen.getByRole('button', { name: 'Nhập CSV' }));
  expect(screen.getByRole('button', { name: 'Xác nhận nhập' })).toBeDisabled();
  expect(screen.getByRole('button', { name: 'Bắt đầu lại' })).toBeDisabled();
  expect(fetch.mock.calls.filter(([path]) => path.endsWith('/confirm'))).toHaveLength(1);
}, 15_000);
it('never confirms a malformed money cell silently replaced by parser zero', async () => {
  vi.stubGlobal(
    'fetch',
    vi.fn((path: string) =>
      Promise.resolve(
        Response.json(
          path.endsWith('/csrf')
            ? { requestToken: 'test' }
            : {
                ...preview,
                rows: [
                  {
                    ...preview.rows[0],
                    draft: { ...draft, receiverPhone: '0901234567', codAmount: '0' },
                    errors: ['codAmount must be a valid decimal number.'],
                    invalidNumericFields: ['codAmount'],
                  },
                ],
              },
        ),
      ),
    ),
  );
  const { user, container } = renderPanel();
  await user.click(screen.getByRole('button', { name: 'Nhập CSV' }));
  await user.upload(
    container.querySelector('input[type="file"]')!,
    new File(['csv'], 'input.csv', { type: 'text/csv' }),
  );
  await user.click(screen.getByRole('button', { name: 'Kiểm tra CSV' }));
  await screen.findByText('codAmount must be a valid decimal number.');
  expect(screen.getByRole('spinbutton', { name: /Dòng 2 · Tiền thu hộ COD/ })).toHaveValue('');
  expect(screen.getByRole('button', { name: 'Xác nhận nhập' })).toBeDisabled();
});
it('rejects oversized files before sending them', async () => {
  const fetch = vi.fn();
  vi.stubGlobal('fetch', fetch);
  const { user, container } = renderPanel();
  await user.click(screen.getByRole('button', { name: 'Nhập CSV' }));
  await user.upload(
    container.querySelector('input[type="file"]')!,
    new File([new Uint8Array(2_000_001)], 'large.csv', { type: 'text/csv' }),
  );
  await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('2.000.000 byte'));
  expect(fetch).not.toHaveBeenCalled();
});
