import { screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../test/renderWithProviders';
import { SessionProvider } from './SessionProvider';
import { RequireSession } from './RequireSession';
import { useSession } from './SessionContext';
import { requestJson } from '../api/httpClient';
afterEach(() => vi.unstubAllGlobals());
function Probe() {
  const { session, isLoading } = useSession();
  return <><p>{isLoading ? 'Session loading' : session?.displayName ?? 'Signed out'}</p>
    <button onClick={() => { void requestJson('/api/v1/app/protected').catch(() => {}); }}>Expire</button></>;
}
describe('session boundary', () => {
  it('never flashes protected content while loading and handles anonymous responses', async () => {
    let respond!: (response: Response) => void;
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(resolve => { respond = resolve; })));
    renderWithProviders(<SessionProvider><RequireSession><p>Private content</p></RequireSession><Probe /></SessionProvider>);
    expect(screen.queryByText('Private content')).not.toBeInTheDocument();
    expect(screen.getByText('Session loading')).toBeInTheDocument();
    respond(Response.json({ code: 'Browser.Unauthenticated' }, { status: 401 }));
    expect(await screen.findByText('Signed out')).toBeInTheDocument();
    expect(screen.queryByText('Private content')).not.toBeInTheDocument();
  });
  it('removes the current user after a protected request returns 401', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json({ code: 'Browser.Unauthenticated' }, { status: 401 })));
    const { user } = renderWithProviders(<Probe />, { session: { userId: 'A', displayName: 'Private User A', roles: ['Shop'], operationPermissions: [], locale: 'vi-VN', serverTimeZoneId: 'UTC' } });
    await user.click(screen.getByRole('button', { name: 'Expire' }));
    await waitFor(() => expect(screen.queryByText('Private User A')).not.toBeInTheDocument());
    expect(screen.getByText('Signed out')).toBeInTheDocument();
  });
});
