import { test, expect } from '@playwright/test';
test.skip(!process.env.MINILOGISTICS_E2E_IDENTITY_FIXTURE, 'Requires isolated Identity/data fixture.');
async function login(page: import('@playwright/test').Page, email: string) {
  await page.goto('./login'); await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.MINILOGISTICS_E2E_PASSWORD!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/^https:\/\/[^/]+\/(?:app\/)?dashboard(?:\?|$)/);
}
test('owner list search detail back reload and invalid URL use the real paged endpoint', async ({ page }) => {
  await login(page, 'browser-owner@example.test');
  await page.goto('./shipments');
  await expect(page.getByRole('link', { name: 'ML-BROWSER-001', exact: true })).toBeVisible();
  await page.getByLabel('Mã vận đơn', { exact: true }).fill('ML-BROWSER-001');
  const request = page.waitForRequest(request => request.url().includes('/api/v1/app/shops/') && request.url().includes('trackingCode=ML-BROWSER-001'));
  await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click(); await request;
  await expect(page).toHaveURL(/trackingCode=ML-BROWSER-001/);
  await page.getByRole('link', { name: 'ML-BROWSER-001', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Chi tiết vận đơn' })).toBeFocused();
  await expect(page.getByText('Private Receiver · 0901234567')).toBeVisible();
  await expect(page.getByText('Lịch sử trạng thái', { exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'Danh sách vận đơn', exact: true }).click();
  await expect(page.getByLabel('Mã vận đơn', { exact: true })).toHaveValue('ML-BROWSER-001');
  await page.getByRole('link', { name: 'ML-BROWSER-001', exact: true }).click();
  await page.goBack(); await expect(page.getByLabel('Mã vận đơn', { exact: true })).toHaveValue('ML-BROWSER-001');
  await page.reload(); await expect(page.getByLabel('Mã vận đơn', { exact: true })).toHaveValue('ML-BROWSER-001');
  await page.goto('./shipments?pageSize=100000'); await expect(page.getByRole('alert')).toContainText('Bộ lọc không hợp lệ');
});
test('limited staff sees masked list/detail and a mobile table without COD actions', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await login(page, 'browser-staff@example.test'); await page.goto('./shipments');
  const link = page.getByRole('link', { name: 'ML-BROWSER-001', exact: true }); await expect(link).toBeVisible();
  await expect(page.getByRole('columnheader', { name: 'COD', exact: true })).toHaveCount(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await link.click(); await expect(page.getByText('Private Receiver · 0901234567')).toHaveCount(0);
  await expect(page.getByRole('heading', { name: 'Chi tiết vận đơn' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});
