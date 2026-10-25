import { test, expect, type Page } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
test.skip(!process.env.MINILOGISTICS_E2E_IDENTITY_FIXTURE, 'Requires isolated Identity/Application fixture.');
async function login(page: Page) {
  await page.goto('./login?returnUrl=/shipper/shipments');
  await page.getByLabel('Email', { exact: true }).fill('browser-shipper@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.MINILOGISTICS_E2E_PASSWORD!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/\/app\/shipper\/shipments/);
  await expect(page.getByRole('heading', { level: 1, name: 'Đơn được phân công' })).toBeVisible();
}
async function confirm(page: Page, path: string, method = 'POST') {
  const response = page.waitForResponse(response => response.request().method() === method && new URL(response.url()).pathname.endsWith(path));
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
  const result = await response; expect(result.status()).toBe(204);
}
test('Shipper mobile reads only own cards updates status collects pending COD and toggles availability', async ({ page }) => {
  test.setTimeout(60_000); // Several independent persisted commands, confirmation and keyboard checks on mobile.
  await page.setViewportSize({ width: 390, height: 844 }); await login(page);
  await expect(page.getByRole('heading', { name: 'ML-SHIPPER-OTHER' })).toHaveCount(0);
  await expect(page.getByText('Mobile Hub — Mobile Fixture Province')).toBeVisible();
  await expect(page.getByText('2/30', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Tạm dừng nhận đơn', exact: true }).click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect.poll(() => page.evaluate(() => !!document.activeElement?.closest('.ant-modal-root'))).toBe(true);
  for (let index = 0; index < 5; index++) { await page.keyboard.press('Tab'); expect(await page.evaluate(() => !!document.activeElement?.closest('.ant-modal-root'))).toBe(true); }
  await page.getByRole('dialog').press('Escape');
  await expect(page.getByRole('button', { name: 'Tạm dừng nhận đơn', exact: true })).toBeFocused();
  await page.getByRole('button', { name: 'Tạm dừng nhận đơn', exact: true }).click(); await confirm(page, '/availability', 'PUT');
  await page.getByRole('button', { name: 'Nhận đơn tự động', exact: true }).click(); await confirm(page, '/availability', 'PUT');
  const own = page.getByRole('article', { name: 'ML-SHIPPER-MOBILE-1', exact: true });
  await expect(own.getByRole('button', { name: 'Cập nhật ML-SHIPPER-MOBILE-1' })).toBeEnabled();
  await own.getByRole('combobox', { name: 'Trạng thái mới' }).focus();
  await own.getByRole('combobox', { name: 'Trạng thái mới' }).press('ArrowDown'); await own.getByRole('combobox', { name: 'Trạng thái mới' }).press('Enter');
  await own.getByRole('button', { name: 'Cập nhật ML-SHIPPER-MOBILE-1' }).click(); await confirm(page, '/status');
  await expect(own).toContainText('Đang lấy hàng');
  await page.getByRole('tab', { name: 'COD chờ thu', exact: true }).click();
  const pending = page.getByRole('article', { name: 'ML-SHIPPER-COD', exact: true });
  await expect(pending.getByRole('button', { name: 'Thu COD ML-SHIPPER-COD' })).toBeVisible();
  const overflow = await page.evaluate(() => ({ document: document.documentElement.scrollWidth, viewport: window.innerWidth, scrollX: window.scrollX,
    nodes: Array.from(document.querySelectorAll('body *')).map(element => ({ tag: element.tagName, class: element.getAttribute('class'), right: element.getBoundingClientRect().right, width: element.getBoundingClientRect().width }))
      .filter(element => element.right > window.innerWidth || element.width > window.innerWidth).slice(-8) }));
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), JSON.stringify(overflow)).toBe(true);
  await expect.poll(async () => (await pending.getByRole('button', { name: 'Thu COD ML-SHIPPER-COD' }).boundingBox())?.height ?? 0).toBeGreaterThanOrEqual(44);
  await mkdir('../../.agent/refactorfe-phase7/screenshots', { recursive: true });
  await page.screenshot({ path: '../../.agent/refactorfe-phase7/screenshots/shipper-390.png', fullPage: true, mask: [page.getByRole('article'), page.getByRole('region', { name: 'Báo cáo COD' })] });
  await pending.getByRole('button', { name: 'Thu COD ML-SHIPPER-COD' }).click(); await confirm(page, '/cod/collect');
  await expect(pending).toHaveCount(0);
  const summary = await page.request.get(new URL('/api/v1/app/shipper/me/cod-summary', page.url()).href);
  expect((await summary.json()).collectedTodayAmount).toBe('25000');
  await page.reload(); await expect(pending).toHaveCount(0);
});
test('Shipper desktop keeps NotRequired filter deep links and primary controls accessible', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 }); await login(page);
  await page.goto('./shipper/shipments?codStatus=NotRequired&pageNumber=1&pageSize=10');
  await expect(page.getByRole('heading', { name: 'ML-SHIPPER-NOCOD' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'ML-SHIPPER-MOBILE-1' })).toHaveCount(0);
  await page.reload(); await expect(page.getByRole('button', { name: 'Cập nhật ML-SHIPPER-NOCOD' })).toBeEnabled();
  await expect(page.getByRole('link', { name: 'Công việc giao hàng', exact: true })).toHaveAttribute('href', '/app/shipper/shipments');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});
