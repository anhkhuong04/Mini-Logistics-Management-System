import { screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../test/renderWithProviders';
import { LoginPage } from './LoginPage';
import { RegisterShopPage } from './RegisterShopPage';
import { safeReturnUrl, postLoginPath } from './redirects';
afterEach(() => vi.unstubAllGlobals());
describe('authentication pages', () => {
  it('uses English labels when the server locale is English', () => {
    renderWithProviders(<LoginPage />, { locale: 'en-US' });
    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(screen.getByLabelText('Password')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Register a shop' })).toBeInTheDocument();
  });
  it('submits login once, maps safe errors, and clears the password', async () => {
    const fetch = vi.fn().mockResolvedValueOnce(Response.json({ requestToken: 'test' }))
      .mockResolvedValueOnce(Response.json({ code: 'Auth.InvalidCredentials', title: 'Invalid credentials' }, { status: 401 }));
    vi.stubGlobal('fetch', fetch);
    const { user } = renderWithProviders(<LoginPage />);
    await user.type(screen.getByLabelText('Email'), 'user@example.test');
    await user.type(screen.getByLabelText('Mật khẩu'), 'WrongPassword!1');
    await user.click(screen.getByRole('button', { name: 'Đăng nhập' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Email hoặc mật khẩu không đúng');
    expect(screen.getByLabelText('Mật khẩu')).toHaveValue('');
    expect(fetch.mock.calls.filter(call => call[1]?.method === 'POST')).toHaveLength(1);
  });
  it('loads real reference data and clears the ward when province changes', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json({ provinces: [
      { name: 'Tỉnh A', wards: [{ name: 'Phường A' }] }, { name: 'Tỉnh B', wards: [{ name: 'Phường B' }] },
    ], serverTimeZoneId: 'Asia/Bangkok' })));
    const { user } = renderWithProviders(<RegisterShopPage />);
    await waitFor(() => expect(screen.getByLabelText('Tỉnh / Thành phố')).not.toBeDisabled());
    await user.click(screen.getByLabelText('Tỉnh / Thành phố'));
    await user.click(await screen.findByTitle('Tỉnh A'));
    await user.click(screen.getByLabelText('Phường / Xã'));
    await user.click(await screen.findByTitle('Phường A'));
    await user.click(screen.getByLabelText('Tỉnh / Thành phố'));
    await user.click(await screen.findByTitle('Tỉnh B'));
    expect(screen.getByLabelText('Phường / Xã').closest('.ant-select')).not.toHaveTextContent('Phường A');
  });
  it('rejects external or malformed returns and preserves role redirect priority', () => {
    for (const path of ['https://bad.test', '//bad.test', '/\\bad.test', '/app/login', '/app/unknown', '/%2f%2fbad.test'])
      expect(safeReturnUrl(path)).toBeNull();
    expect(safeReturnUrl('/shipments?shopId=abc')).toBe('/app/shipments?shopId=abc');
    expect(postLoginPath(['Shop', 'Shipper', 'Operator'])).toBe('/operations/assignments');
    expect(postLoginPath(['IntegrationAdmin'])).toBe('/partner/integrations');
  });
});
