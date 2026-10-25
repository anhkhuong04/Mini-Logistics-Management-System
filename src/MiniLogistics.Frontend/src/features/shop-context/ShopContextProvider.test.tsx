import { screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../test/renderWithProviders';
import { ShopContextProvider } from './ShopContextProvider';
import { useShopContext } from './ShopContext';
import { useSession, type Session } from '../../shared/auth/SessionContext';
import { resetCsrf } from '../../shared/api/httpClient';
const a = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const b = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
const session: Session = { userId: 'user-a', displayName: 'User A', roles: ['Shop'], operationPermissions: [], locale: 'vi-VN', serverTimeZoneId: 'UTC' };
afterEach(() => { vi.unstubAllGlobals(); resetCsrf(); });
function Probe() {
  const { context, loading, selectShop } = useShopContext();
  const { logout } = useSession();
  return <><div>{loading ? 'Loading' : context?.shops.find(shop => shop.shopId === context.selectedShopId)?.name ?? 'No shop'}</div>
    <button onClick={() => selectShop(b)}>Select B</button><button onClick={() => void logout()}>Logout</button></>;
}
describe('tenant context', () => {
  it('cancels A and never renders late A after selecting B', async () => {
    let resolveA!: (response: Response) => void;
    let signalA: AbortSignal | undefined;
    vi.stubGlobal('fetch', vi.fn((path: string, options: RequestInit) => {
      if (path.includes(a)) { signalA = options.signal!; return new Promise<Response>(resolve => { resolveA = resolve; }); }
      return Promise.resolve(Response.json({ selectedShopId: b, shops: [{ shopId: b, name: 'Shop B', permissions: ['ViewShipments'] }] }));
    }));
    const { user } = renderWithProviders(<ShopContextProvider><Probe /></ShopContextProvider>, { session, path: `/?shopId=${a}` });
    await waitFor(() => expect(signalA).toBeDefined());
    await user.click(screen.getByRole('button', { name: 'Select B' }));
    expect(await screen.findByText('Shop B')).toBeInTheDocument();
    expect(signalA?.aborted).toBe(true);
    resolveA(Response.json({ selectedShopId: a, shops: [{ shopId: a, name: 'Private Shop A', permissions: [] }] }));
    await waitFor(() => expect(screen.queryByText('Private Shop A')).not.toBeInTheDocument());
  });
  it('removes context and cached data immediately on logout', async () => {
    vi.stubGlobal('fetch', vi.fn((path: string) => Promise.resolve(path.includes('csrf')
      ? Response.json({ requestToken: 'test' }) : path.includes('logout') ? new Response(null, { status: 204 })
      : Response.json({ selectedShopId: a, shops: [{ shopId: a, name: 'Private Shop A', permissions: [] }] }))));
    const { user } = renderWithProviders(<ShopContextProvider><Probe /></ShopContextProvider>, { session, path: `/?shopId=${a}` });
    expect(await screen.findByText('Private Shop A')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Logout' }));
    await waitFor(() => expect(screen.queryByText('Private Shop A')).not.toBeInTheDocument());
  });
});
