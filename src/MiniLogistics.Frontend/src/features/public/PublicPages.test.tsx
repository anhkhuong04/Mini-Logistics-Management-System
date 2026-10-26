import { afterEach, expect, it, vi } from 'vitest';
import { act, screen, waitFor } from '@testing-library/react';
import { useLocation } from 'react-router';
import { renderWithProviders } from '../../test/renderWithProviders';
import { HomePage } from './HomePage';
import { ContactPage } from './ContactPage';
import { FaqPage } from './FaqPage';
import { TrackingPage } from './TrackingPage';
const result = { trackingCode: 'LAK123', accessLevel: 'Summary', currentStatus: 'InTransit', pickupProvince: 'Hà Nội', deliveryProvince: 'Hồ Chí Minh', createdAtUtc: '2026-10-25T18:30:00Z', lastUpdatedAtUtc: '2026-10-25T18:30:00Z', senderName: 'N***', receiverName: 'T***', serverTimeZoneId: 'Asia/Bangkok', timeline: [{ status: 'InTransit', messageCode: 'Shipment.PublicStatus.InTransit', message: 'Safe public message', locale: 'en-US', changedAtUtc: '2026-10-25T18:30:00Z' }] };
function Location() { return <output>{useLocation().search}</output>; }
afterEach(() => { vi.unstubAllGlobals(); vi.useRealTimers(); });
it('preserves existing banner URLs, manual selection and the three-second carousel with pause', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json([
    { id: 'a', title: 'Remote campaign', imageUrl: 'https://cdn.example.test/campaign.png', linkUrl: '/tracking' },
    { id: 'b', title: 'Legacy campaign', imageUrl: '/images/landing/banner2.png', linkUrl: null },
  ])));
  const { user } = renderWithProviders(<HomePage />, { locale: 'en-US' });
  expect(await screen.findByRole('img', { name: 'Remote campaign' })).toBeVisible();
  await user.click(screen.getByRole('button', { name: 'Next banner' })); expect(screen.getByRole('img', { name: 'Legacy campaign' })).toBeVisible();
  await user.click(screen.getByRole('button', { name: 'Pause banners' }));
  vi.useFakeTimers(); await act(async () => { vi.advanceTimersByTime(3000); });
  expect(screen.getByRole('img', { name: 'Legacy campaign' })).toBeVisible();
});
it('keeps the landing usable when banners fail and submits a trimmed tracking query', async () => {
  vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('offline')));
  const { user } = renderWithProviders(<><HomePage /><Location /></>, { locale: 'en-US' });
  expect(screen.getByRole('heading', { level: 1 })).toBeVisible();
  await user.type(screen.getByRole('textbox', { name: 'Tracking code' }), ' LAK123 ');
  await user.click(screen.getByRole('button', { name: 'Track shipment' }));
  expect(screen.getByText('?code=LAK123')).toBeVisible();
});
it('uses real support links without a pretend contact submission', () => {
  const { container } = renderWithProviders(<ContactPage />, { locale: 'en-US' });
  expect(screen.getByRole('link', { name: '1900 6885' })).toHaveAttribute('href', 'tel:19006885');
  expect(screen.getByRole('link', { name: 'support@laklogistics.vn' })).toHaveAttribute('href', 'mailto:support@laklogistics.vn');
  expect(container.querySelector('form')).toBeNull();
});
it('keeps FAQ shipping and COD content and a working tracking destination', () => {
  renderWithProviders(<FaqPage />, { locale: 'en-US' });
  expect(screen.getByText(/2–5 working days/)).toBeVisible();
  expect(screen.getByText(/Monday, Wednesday and Friday/)).toBeVisible();
  expect(screen.getByRole('link', { name: 'Track shipment' })).toHaveAttribute('href', '/tracking');
});
it('loads query tracking without verification inputs and displays the server timezone', async () => {
  const fetch = vi.fn().mockResolvedValue(Response.json(result)); vi.stubGlobal('fetch', fetch);
  renderWithProviders(<TrackingPage />, { path: '/tracking?code=LAK123', locale: 'en-US' });
  expect(await screen.findByText('N***')).toBeVisible();
  expect(screen.getByText('T***')).toBeVisible();
  expect(screen.getAllByText(/Oct 26, 2026, 1:30 AM/).length).toBeGreaterThan(0);
  expect(screen.queryByRole('textbox', { name: /phone/i })).not.toBeInTheDocument();
  expect(fetch.mock.calls[0][0]).toBe('/api/v1/public/tracking/LAK123');
});
it.each([404, 429, 503])('localizes safe errors %s without exposing server detail or retrying automatically', async status => {
  const fetch = vi.fn().mockResolvedValue(Response.json({ title: 'SECRET detail' }, { status, headers: { 'Retry-After': '42' } })); vi.stubGlobal('fetch', fetch);
  renderWithProviders(<TrackingPage />, { path: '/tracking?code=LAK123', locale: 'en-US' });
  expect(await screen.findByRole('alert')).toHaveTextContent(status === 404 ? 'Shipment not found' : status === 429 ? '42 seconds' : 'Unable to load tracking');
  expect(screen.queryByText('SECRET detail')).not.toBeInTheDocument();
  await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));
});
