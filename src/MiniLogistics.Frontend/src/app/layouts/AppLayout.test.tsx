import { DatePicker } from 'antd';
import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { App } from '../App';
import { renderWithProviders } from '../../test/renderWithProviders';
import { AppLayout } from './AppLayout';
import { PageHeader } from '../../shared/ui/PageHeader';

describe('application shell', () => {
  it('offers a skip link and a real login without assuming a signed-in role', () => {
    renderWithProviders(<App />);
    expect(screen.getByRole('link', { name: 'Đến nội dung chính' })).toHaveAttribute('href', '#main-content');
    expect(screen.getByRole('main')).toHaveAttribute('id', 'main-content');
    expect(screen.getByRole('link', { name: 'Đăng nhập' })).toHaveAttribute('href', '/login');
    expect(screen.queryByText('Admin')).not.toBeInTheDocument();
  });

  it('navigates back from a missing page and moves focus to the page heading', async () => {
    const { user } = renderWithProviders(<App />, { path: '/missing?status=example' });
    expect(screen.getByRole('heading', { level: 1, name: 'Không tìm thấy trang' })).toHaveFocus();
    await user.click(screen.getByRole('link', { name: 'Về trang chính' }));
    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: 'Mini Logistics' })).toHaveFocus());
    expect(document.title).toBe('Mini Logistics');
  });

  it('closes the mobile navigation with Escape and returns focus to its trigger', async () => {
    const { user } = renderWithProviders(<App />);
    const trigger = screen.getByRole('button', { name: 'Mở menu' });
    await user.click(trigger);
    expect(await screen.findByRole('dialog')).toHaveAccessibleName('Điều hướng');
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(trigger).toHaveFocus();
  });

  it('renders only navigation supplied by its caller', () => {
    renderWithProviders(<AppLayout navigation={[{ key: 'reports', label: 'Báo cáo được cấp quyền', to: '/reports' }]}>
      <PageHeader title="Báo cáo" />
    </AppLayout>);
    expect(screen.getByRole('menuitem', { name: 'Báo cáo được cấp quyền' })).toBeInTheDocument();
    expect(screen.queryByRole('menuitem', { name: 'Admin' })).not.toBeInTheDocument();
  });

  it('uses English app and Ant Design date-control copy together', async () => {
    const { user } = renderWithProviders(<><App /><DatePicker /></>, { locale: 'en-US' });
    expect(screen.getByRole('link', { name: 'Sign in' })).toHaveAttribute('href', '/login');
    const input = screen.getByPlaceholderText('Select date');
    await user.click(input);
    expect(await screen.findByText('Today')).toBeInTheDocument();
    expect(document.documentElement.lang).toBe('en');
  });

  it('uses the existing culture endpoint with a local return URL', () => {
    renderWithProviders(<App />, { path: '/missing?status=ready' });
    const language = screen.getByRole('link', { name: 'English' });
    const url = new URL(language.getAttribute('href')!, 'https://localhost');
    expect(url.pathname).toBe('/culture/set');
    expect(url.searchParams.get('culture')).toBe('en-US');
    expect(url.searchParams.get('returnUrl')).toBe('/app/missing?status=ready');
  });
});
