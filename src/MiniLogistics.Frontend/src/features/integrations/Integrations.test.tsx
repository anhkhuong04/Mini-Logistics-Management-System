import { act, screen, within, waitFor } from '@testing-library/react';
import { useEffect } from 'react';
import { useQueryClient, type QueryClient } from '@tanstack/react-query';
import { Route, Routes } from 'react-router';
import { afterEach, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../test/renderWithProviders';
import { requestJson, resetCsrf } from '../../shared/api/httpClient';
import { RequireSession } from '../../shared/auth/RequireSession';
import { ShopContextProvider } from '../shop-context';
import { IntegrationClientsPage } from './IntegrationClientsPage';
import { OneTimeSecretModal } from './OneTimeSecretModal';
import { safeReturnUrl, postLoginPath } from '../auth';
const shopId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const session = {
  userId: 'portal',
  displayName: 'Portal',
  roles: ['Shop'],
  operationPermissions: [],
  locale: 'vi-VN',
  serverTimeZoneId: 'Asia/Bangkok',
};
const client = {
  apiClientId: 'client-id',
  shopId,
  shopName: 'Portal Shop',
  name: 'Storefront',
  apiKeyPrefix: 'ml_test_prefix',
  isActive: true,
  scopes: ['Quote', 'WebhookManage'],
  allowedIpAddresses: [],
  expiresAtUtc: null,
  webhookEndpoint: { url: 'https://public.example.test/hook', isActive: true },
  recentDeliveries: [],
  recentCredentialAudits: [],
  webhookMetrics: { totalDeliveries: 0, succeededDeliveries: 0, successRate: '0', averageLatencyMs: null },
  usage: null,
};
function setup(write: (path: string, options: RequestInit) => Promise<Response>) {
  const fetch = vi.fn(async (path: string, options?: RequestInit) => {
    if (path.endsWith('/csrf')) return Response.json({ requestToken: 'test' });
    if (path.endsWith('/auth/me'))
      return Response.json({ code: 'Browser.Unauthenticated', title: 'Expired' }, { status: 401 });
    if (options?.method === 'PUT' || options?.method === 'POST') return write(path, options);
    return Response.json({
      environment: 'Sandbox',
      granularPermissionEnabled: false,
      shops: [{ shopId, shopName: 'Portal Shop', isActive: true }],
      apiClients: [client],
      outboxFailures: [],
    });
  });
  vi.stubGlobal('fetch', fetch);
  return fetch;
}
afterEach(() => {
  vi.unstubAllGlobals();
  resetCsrf();
});
it('chooses an integration-authorized Shop instead of the shipment-only default context', async () => {
  const viewOnly = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
  const fetch = vi.fn(async (path: string) =>
    path.includes('/shop-context')
      ? Response.json({ code: 'Application.Forbidden', title: 'Shipment context denied' }, { status: 403 })
      : Response.json({
          environment: 'Sandbox',
          granularPermissionEnabled: false,
          shops: [{ shopId, shopName: 'Portal Shop', isActive: true }],
          apiClients: [],
          outboxFailures: [],
        }),
  );
  vi.stubGlobal('fetch', fetch);
  renderWithProviders(
    <ShopContextProvider>
      <IntegrationClientsPage />
    </ShopContextProvider>,
    { session, path: '/partner/integrations' },
  );
  expect(await screen.findByRole('textbox', { name: 'Tên API client' })).toBeInTheDocument();
  expect(
    fetch.mock.calls.some(([path]) => path.includes('/integrations/dashboard') && path.includes(viewOnly)),
  ).toBe(false);
  expect(
    fetch.mock.calls.some(([path]) => path.includes('/integrations/dashboard') && path.includes(shopId)),
  ).toBe(true);
  expect(fetch.mock.calls.some(([path]) => path.includes('/shop-context'))).toBe(false);
});
function CacheProbe({ capture }: { capture: (cache: QueryClient) => void }) {
  const cache = useQueryClient();
  useEffect(() => capture(cache), [cache, capture]);
  return null;
}
it('never caches a revealed key and removes it when the session expires', async () => {
  setup(async () =>
    Response.json({
      apiClientId: 'client-id',
      apiKeyPrefix: 'ml_test_',
      apiKey: 'ephemeral-ui-key-not-real',
    }),
  );
  let cache: QueryClient | undefined;
  const { user } = renderWithProviders(
    <>
      <CacheProbe
        capture={(value) => {
          cache = value;
        }}
      />
      <Routes>
        <Route
          path="/partner/integrations"
          element={
            <RequireSession roles={['Shop']}>
              <IntegrationClientsPage />
            </RequireSession>
          }
        />
        <Route path="/login" element={<p>Signed out</p>} />
      </Routes>
    </>,
    { session, path: `/partner/integrations?shopId=${shopId}&clientId=client-id` },
  );
  await user.click(await screen.findByRole('button', { name: 'Rotate API key' }));
  await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Xác nhận' }));
  expect(await screen.findByLabelText('API key mới')).toHaveValue('ephemeral-ui-key-not-real');
  expect(
    JSON.stringify(
      cache!
        .getQueryCache()
        .getAll()
        .map((q) => q.state.data),
    ),
  ).not.toContain('ephemeral-ui-key-not-real');
  await act(async () => {
    await requestJson('/api/v1/app/auth/me').catch(() => undefined);
  });
  expect(screen.queryByLabelText('API key mới')).not.toBeInTheDocument();
  expect(cache!.getQueryCache().getAll()).toHaveLength(0);
});
it('routes integration returns and IntegrationAdmin login into the SPA', () => {
  expect(safeReturnUrl(`/partner/integrations?shopId=${shopId}`)).toBe(
    `/app/partner/integrations?shopId=${shopId}`,
  );
  expect(postLoginPath(['IntegrationAdmin'])).toBe('/app/partner/integrations');
  expect(safeReturnUrl('/partner/integrations/evil')).toBeNull();
});
it('copies a one-time key with feedback and clears DOM on close', async () => {
  const close = vi.fn();
  const view = renderWithProviders(
    <OneTimeSecretModal
      value={{ apiClientId: 'id', apiKeyPrefix: 'ml_test_', apiKey: 'ui-only-secret-not-real' }}
      close={close}
    />,
  );
  const copy = vi.spyOn(navigator.clipboard, 'writeText').mockResolvedValue();
  await view.user.click(screen.getByRole('button', { name: 'Sao chép API key' }));
  expect(copy).toHaveBeenCalledWith('ui-only-secret-not-real');
  expect(await screen.findByRole('status')).toHaveTextContent('Đã sao chép');
  await view.user.click(screen.getByRole('button', { name: 'Đã lưu key, đóng' }));
  expect(close).toHaveBeenCalledOnce();
  view.unmount();
  expect(screen.queryByLabelText('API key mới')).not.toBeInTheDocument();
});
it('keeps rotated key out of reads and removes it when closing the modal', async () => {
  const fetch = setup(async () =>
    Response.json({ apiClientId: 'client-id', apiKeyPrefix: 'ml_test_', apiKey: 'one-time-ui-key-not-real' }),
  );
  const { user } = renderWithProviders(<IntegrationClientsPage />, {
    session,
    path: `/partner/integrations?shopId=${shopId}&clientId=client-id`,
  });
  await user.click(await screen.findByRole('button', { name: 'Rotate API key' }));
  expect(fetch.mock.calls.some(([, o]) => o?.method === 'POST')).toBe(false);
  await user.dblClick(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Xác nhận' }));
  expect(await screen.findByLabelText('API key mới')).toHaveValue('one-time-ui-key-not-real');
  expect(fetch.mock.calls.filter(([p]) => p.endsWith('/rotate-key'))).toHaveLength(1);
  await user.click(screen.getByRole('button', { name: 'Đã lưu key, đóng' }));
  await waitFor(() => expect(screen.queryByLabelText('API key mới')).not.toBeInTheDocument());
  expect(fetch.mock.calls.some(([p]) => p.includes('one-time-ui-key'))).toBe(false);
});
it.each([0, 409])(
  'never retries an uncertain or conflicting rotate (%s) before authoritative refresh',
  async (status) => {
    const fetch = setup(async () => {
      if (status === 409)
        return Response.json({ code: 'Application.Conflict', title: 'Changed' }, { status });
      throw new TypeError('offline');
    });
    const { user } = renderWithProviders(<IntegrationClientsPage />, {
      session,
      path: `/partner/integrations?shopId=${shopId}&clientId=client-id`,
    });
    await user.click(await screen.findByRole('button', { name: 'Rotate API key' }));
    await user.dblClick(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Xác nhận' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Rotate API key' })).toBeDisabled());
    expect(fetch.mock.calls.filter(([p]) => p.endsWith('/rotate-key'))).toHaveLength(1);
    expect(screen.queryByLabelText('API key mới')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Làm mới' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Rotate API key' })).toBeEnabled());
  },
);
it('clears signing secret before confirmation and never renders the stored secret', async () => {
  const fetch = setup(async () => Response.json({ url: 'https://public.example.test/hook', isActive: true }));
  const { user } = renderWithProviders(<IntegrationClientsPage />, {
    session,
    path: `/partner/integrations?shopId=${shopId}&clientId=client-id`,
  });
  const secret = await screen.findByLabelText(/Signing secret mới/);
  expect(secret).toHaveValue('');
  await user.click(secret);
  await user.paste('ui-signing-secret-that-is-at-least-thirty-two-bytes');
  await user.click(screen.getByRole('button', { name: 'Lưu và kích hoạt webhook' }));
  const dialog = await screen.findByRole('dialog');
  expect(secret).toHaveValue('');
  expect(dialog).not.toHaveTextContent('ui-signing-secret');
  await user.click(within(dialog).getByRole('button', { name: 'Xác nhận' }));
  await waitFor(() => expect(fetch.mock.calls.filter(([, o]) => o?.method === 'PUT')).toHaveLength(1));
  const body = JSON.parse(String(fetch.mock.calls.find(([, o]) => o?.method === 'PUT')![1]?.body));
  expect(body.signingSecret).toBe('ui-signing-secret-that-is-at-least-thirty-two-bytes');
  expect(secret).toHaveValue('');
});
