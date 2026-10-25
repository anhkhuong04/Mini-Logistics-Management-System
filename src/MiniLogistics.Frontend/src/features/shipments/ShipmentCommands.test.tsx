import { screen, waitFor } from '@testing-library/react';
import { expect, it, vi } from 'vitest';
import { useState } from 'react';
import { ApiError } from '../../shared/api/httpClient';
import { renderWithProviders } from '../../test/renderWithProviders';
import { ShopContextState } from '../shop-context/ShopContext';
import { useShipmentCommand } from './useShipmentCommand';
import { ShipmentCommandFeedback } from './ShipmentCommandFeedback';
const shopId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const session = { userId: 'user', displayName: 'User', roles: ['Shop'], operationPermissions: [], locale: 'vi-VN', serverTimeZoneId: 'UTC' };
function Harness({ operation, onSuccess }: { operation(): Promise<string>; onSuccess?(value: string): void }) {
  const command = useShipmentCommand(); const [result, setResult] = useState('');
  return <><button disabled={command.pending || command.blocked} onClick={() => { void command.run(operation).then(value => { if (value) { setResult(value.value); onSuccess?.(value.value); } }); }}>Save</button>
    <output>{result}</output><ShipmentCommandFeedback {...command} shopId={shopId} reload={command.reset} /></>;
}
function renderCommand(operation: () => Promise<string>, onSuccess?: (value: string) => void) {
  return renderWithProviders(<ShopContextState.Provider value={{ context: { selectedShopId: shopId, shops: [] }, loading: false, error: null, selectShop: vi.fn(), reload: vi.fn() }}>
    <Harness operation={operation} onSuccess={onSuccess} /></ShopContextState.Provider>, { session });
}
it('sends one command while pending even on double click', async () => {
  let resolve!: (value: string) => void;
  const operation = vi.fn(() => new Promise<string>(done => { resolve = done; })); const { user } = renderCommand(operation);
  await user.dblClick(screen.getByRole('button', { name: 'Save' })); expect(operation).toHaveBeenCalledOnce();
  resolve('ML-SAVED'); await screen.findByText('ML-SAVED'); expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled();
});
it.each([new TypeError('Network unavailable'), new ApiError(201, 'Browser.ProtocolError', 'Invalid server response'), new ApiError(200, 'Browser.ProtocolError', 'Invalid server response')])('does not retry an unknown transport outcome %s and directs the user to check the list', async failure => {
  const operation = vi.fn().mockRejectedValue(failure); const { user } = renderCommand(operation);
  await user.click(screen.getByRole('button', { name: 'Save' }));
  await screen.findByText(/Chưa xác định được kết quả/); expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
  expect(screen.getByRole('link', { name: 'Danh sách vận đơn' })).toHaveAttribute('href', `/shipments?shopId=${shopId}`);
  expect(operation).toHaveBeenCalledOnce();
});
it('blocks a conflict until an explicit reload then permits a new command', async () => {
  const operation = vi.fn().mockRejectedValueOnce(new ApiError(409, 'Application.ConcurrencyConflict', 'Conflict')).mockResolvedValue('ML-RELOADED');
  const { user } = renderCommand(operation); await user.click(screen.getByRole('button', { name: 'Save' }));
  await screen.findByText(/Trạng thái đã thay đổi/); expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
  await user.click(screen.getByRole('button', { name: 'Tải lại dữ liệu' })); await user.click(screen.getByRole('button', { name: 'Save' }));
  await waitFor(() => expect(screen.getByText('ML-RELOADED')).toBeInTheDocument()); expect(operation).toHaveBeenCalledTimes(2);
});
it('ignores a late command result after leaving the identity/Shop workspace', async () => {
  let resolve!: (value: string) => void;
  const onSuccess = vi.fn(); const operation = () => new Promise<string>(done => { resolve = done; });
  const { user, unmount } = renderCommand(operation, onSuccess);
  await user.click(screen.getByRole('button', { name: 'Save' })); unmount();
  resolve('OLD-SHOP'); await Promise.resolve(); await Promise.resolve(); expect(onSuccess).not.toHaveBeenCalled();
});
