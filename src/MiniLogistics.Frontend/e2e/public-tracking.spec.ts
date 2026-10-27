import { test, expect } from '@playwright/test';
const summary = {
  trackingCode: 'PUBLIC123',
  accessLevel: 'Summary',
  currentStatus: 'InTransit',
  pickupProvince: 'Hà Nội',
  deliveryProvince: 'Hồ Chí Minh',
  senderName: 'N***',
  receiverName: 'T***',
  createdAtUtc: '2026-10-25T18:30:00Z',
  lastUpdatedAtUtc: '2026-10-25T18:30:00Z',
  serverTimeZoneId: 'Asia/Bangkok',
  timeline: [
    {
      status: 'InTransit',
      messageCode: 'SHIPMENT_IN_TRANSIT',
      message: 'Vận đơn đang được vận chuyển.',
      locale: 'vi-VN',
      changedAtUtc: '2026-10-25T18:30:00Z',
    },
  ],
};
test('public tracking preserves query across reload and back, with masked summary and localized failures', async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.route('**/api/v1/public/tracking/*', (route) => {
    const code = new URL(route.request().url()).pathname.split('/').at(-1);
    return code === 'PUBLIC123'
      ? route.fulfill({ json: summary })
      : route.fulfill({
          status: code === 'LIMIT' ? 429 : 404,
          contentType: 'application/problem+json',
          headers: { 'Retry-After': '42' },
          body: '{"title":"Internal detail must not display"}',
        });
  });
  await page.goto('./tracking?code=PUBLIC123');
  await expect(page.getByText('N***', { exact: true })).toBeVisible();
  await expect(page.getByRole('textbox')).toHaveCount(1);
  await page.reload();
  await expect(page.getByText('T***', { exact: true })).toBeVisible();
  const input = page.getByRole('textbox', { name: 'Mã vận đơn' });
  await input.fill('MISSING');
  await input.press('Enter');
  await expect(page.getByRole('alert')).toContainText('Không tìm thấy vận đơn');
  await input.fill('LIMIT');
  await input.press('Enter');
  await expect(page.getByRole('alert')).toContainText('42 giây');
  await expect(page.getByText('Internal detail must not display')).toHaveCount(0);
  await page.goBack();
  await expect(input).toHaveValue('MISSING');
  await page.goBack();
  await expect(page.getByText('N***', { exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});
test('public tracking uses the real summary endpoint without personal details', async ({ page }) => {
  test.skip(!process.env.MINILOGISTICS_E2E_IDENTITY_FIXTURE, 'Requires isolated shipment data.');
  await page.goto('./tracking?code=ML-BROWSER-001');
  await expect(page.getByRole('heading', { name: 'ML-BROWSER-001', exact: true })).toBeVisible();
  const response = await page.request.get(new URL('/api/v1/public/tracking/ML-BROWSER-001', page.url()).href);
  expect(response.status()).toBe(200);
  const body = await response.json();
  expect(body.accessLevel).toBe('Summary');
  expect(body.senderName).toContain('*');
  expect(body.receiverName).toContain('*');
  for (const field of ['senderPhone', 'receiverPhone', 'pickupAddress', 'deliveryAddress'])
    expect(Object.hasOwn(body, field)).toBe(false);
});
