import { test, expect } from '@playwright/test';
import { mkdir } from 'node:fs/promises';

test.skip(!process.env.MINILOGISTICS_E2E_IDENTITY_FIXTURE, 'Requires isolated Identity/data fixture.');
async function login(page: import('@playwright/test').Page, email: string) {
  await page.goto('./login');
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.MINILOGISTICS_E2E_PASSWORD!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/^https:\/\/[^/]+\/(?:app\/)?dashboard(?:\?|$)/);
}

test('KPI drill-down issues a real scoped shipment request with exact UTC filters', async ({ page }) => {
  await login(page, 'browser-owner@example.test');
  await expect(page.getByRole('link', { name: 'Chờ lấy hàng: 1', exact: true })).toBeVisible();
  const shop = new URL(page.url()).searchParams.get('shopId')!;
  await page.goto(`./dashboard?shopId=${shop}&fromUtc=2026-10-01T00:00:00Z&toUtc=2026-10-01T23:59:59.9999999Z`);
  const request = page.waitForRequest(request => {
    const url = new URL(request.url());
    return url.pathname === `/api/v1/app/shops/${shop}/shipments` && url.searchParams.get('status') === 'PendingPickup';
  });
  await page.getByRole('link', { name: 'Chờ lấy hàng: 1', exact: true }).click();
  const url = new URL((await request).url());
  expect(url.searchParams.get('fromUtc')).toBe('2026-10-01T00:00:00Z');
  expect(url.searchParams.get('toUtc')).toBe('2026-10-01T23:59:59.9999999Z');
  expect(url.searchParams.get('pageNumber')).toBe('1');
  await expect(page.getByRole('link', { name: 'ML-BROWSER-001', exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Vận đơn', exact: true })).toBeFocused();
});

test('limited staff dashboard keeps masking and fits mobile tablet and desktop', async ({ page }) => {
  await login(page, 'browser-staff@example.test');
  await expect(page.getByRole('link', { name: 'Chờ lấy hàng: 1', exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'ML-BROWSER-001', exact: true })).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Shop hiện tại', exact: true })).toHaveCount(0);
  await expect(page.getByText('COD chờ thu', { exact: true })).toHaveCount(0);
  await expect(page.getByRole('columnheader', { name: 'COD', exact: true })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Tạo đơn hàng', exact: true })).toHaveCount(0);
  await expect(page.getByText('Private Receiver', { exact: true })).toHaveCount(0);
  await mkdir('../../.agent/refactorfe-phase3/screenshots', { recursive: true });
  for (const width of [390, 768, 1024, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    await page.screenshot({ path: `../../.agent/refactorfe-phase3/screenshots/dashboard-${width}.png`, fullPage: true });
  }
});
