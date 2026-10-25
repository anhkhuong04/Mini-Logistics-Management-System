import { expect, test, type Page } from '@playwright/test';
test.skip(!process.env.MINILOGISTICS_E2E_IDENTITY_FIXTURE, 'Requires isolated Identity/Application fixture.');
async function login(page: Page, email: string) {
  await page.goto('./login?returnUrl=/shop/cod-report'); await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.MINILOGISTICS_E2E_PASSWORD!); await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/shopId=/);
}
test('Shop reporting notifications preferences and masked audit persist through real APIs', async ({ page }) => {
  await login(page, 'browser-report-owner@example.test'); const shopId = new URL(page.url()).searchParams.get('shopId')!;
  await expect(page.getByRole('heading', { name: 'Báo cáo COD', exact: true })).toBeVisible(); await expect(page.getByText('ML-SETTINGS-COD', { exact: true })).toBeVisible();
  await page.getByRole('textbox', { name: 'Từ ngày (UTC)' }).fill('2026-10-01'); await page.getByRole('button', { name: 'Áp dụng', exact: true }).click();
  await expect(page).toHaveURL(/fromUtc=2026-10-01/); await page.reload(); await expect(page.getByText('ML-SETTINGS-COD', { exact: true })).toBeVisible();
  const download = page.waitForEvent('download'); await page.getByRole('button', { name: 'Xuất COD CSV', exact: true }).click(); expect((await download).suggestedFilename()).toMatch(/\.csv$/);
  await page.goto(`./shop/notifications?shopId=${shopId}`); await expect(page.getByText('Settings delivery notice', { exact: true })).toBeVisible();
  const read = page.waitForResponse(response => response.request().method() === 'POST' && response.url().endsWith('/read'));
  await page.getByRole('button', { name: 'Đã đọc Settings delivery notice' }).click(); expect((await read).status()).toBe(204);
  await expect(page.getByRole('button', { name: 'Đã đọc Settings delivery notice' })).toHaveCount(0);
  await page.getByRole('checkbox', { name: 'cod.collected', exact: true }).uncheck(); await page.getByRole('button', { name: 'Lưu tùy chọn', exact: true }).click();
  const preferences = page.waitForResponse(response => response.request().method() === 'PUT' && response.url().endsWith('/preferences'));
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click(); expect((await preferences).status()).toBe(200);
  await page.reload(); await expect(page.getByRole('checkbox', { name: 'cod.collected', exact: true })).not.toBeChecked();
  await page.goto(`./shop/audit?shopId=${shopId}`); await expect(page.getByRole('heading', { name: 'Nhật ký Shop', exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'FixtureSettingsAudit', exact: true })).toBeVisible(); expect(await page.content()).not.toContain('settings-secret-fixture');
});
test('restricted Shop reader gets masked profile and cannot read COD audit preferences or export', async ({ page }) => {
  await login(page, 'browser-report-reader@example.test'); const shopId = new URL(page.url()).searchParams.get('shopId')!;
  await expect(page.getByText('Không có quyền truy cập Shop', { exact: true })).toBeVisible();
  await page.goto(`./profile?shopId=${shopId}`); await expect(page.getByText('Hồ sơ chỉ đọc theo quyền hiện tại')).toBeVisible(); await expect(page.getByRole('button', { name: 'Lưu hồ sơ' })).toHaveCount(0);
  for (const part of ['cod-report', 'audit', 'notifications/preferences', 'staff']) expect((await page.request.get(new URL(`/api/v1/app/shops/${shopId}/${part}`, page.url()).href)).status()).toBe(403);
  expect((await page.request.get(new URL(`/shop/files/cod-report.csv?shopId=${shopId}`, page.url()).href)).status()).toBe(400);
  await page.goto(`./shop/notifications?shopId=${shopId}`); await expect(page.getByRole('heading', { name: 'Thông báo Shop', exact: true })).toBeVisible(); await expect(page.getByRole('button', { name: 'Lưu tùy chọn' })).toHaveCount(0);
});
