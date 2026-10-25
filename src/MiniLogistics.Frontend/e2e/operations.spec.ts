import { test, expect, type Page } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
test.skip(!process.env.MINILOGISTICS_E2E_IDENTITY_FIXTURE, 'Requires isolated Identity/Application fixture.');
async function login(page: Page, email: string) {
  await page.goto('./login?returnUrl=/operations/assignments');
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.MINILOGISTICS_E2E_PASSWORD!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Điều phối giao nhận' })).toBeVisible();
}
const write = (page: Page, suffix: string) => page.waitForResponse(response => response.request().method() === 'POST'
  && new URL(response.url()).pathname.endsWith(suffix) && response.url().includes('/api/v1/app/operations/'));
async function confirm(page: Page, suffix: string) {
  const response = write(page, suffix);
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
  expect((await response).status()).toBe(204);
  await expect(page.getByRole('dialog')).not.toBeVisible();
}
async function chooseShipper(page: Page, name: string) {
  const input = page.getByRole('dialog').getByRole('combobox', { name: 'Chọn shipper' });
  await input.click(); await input.fill(name); await input.press('Enter');
}
test('Operator mobile assigns reassigns cancels updates and collects with real permissions', async ({ page }) => {
  test.setTimeout(90_000); // Covers assignment, delivery and COD commands plus mobile keyboard behavior.
  await page.setViewportSize({ width: 390, height: 844 });
  await login(page, 'browser-operator@example.test');
  await expect(page.getByRole('tab', { name: /Đối soát/ })).toHaveCount(0);
  await page.getByRole('textbox', { name: 'Tìm vận đơn / người nhận' }).fill('ML-OPS-PENDING');
  await page.getByRole('button', { name: 'Áp dụng', exact: true }).click();
  await expect(page).toHaveURL(/searchText=ML-OPS-PENDING/); await page.reload();
  await expect(page.getByRole('button', { name: 'Phân công ML-OPS-PENDING' })).toBeEnabled();
  const row = page.getByRole('row').filter({ hasText: 'ML-OPS-PENDING' });
  await row.getByRole('checkbox').check();
  await page.getByRole('button', { name: /Retry tự động các đơn đã chọn/ }).click();
  const bulk = write(page, '/assignments/bulk-retry');
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
  const result = await (await bulk).json(); expect(result.assignedCount).toBe(0); expect(result.items[0].result).toBe('NoEligibleShipper');
  await expect(page.getByRole('button', { name: /Retry tự động các đơn đã chọn/ })).toBeDisabled();
  expect(await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth)).toBe(false);
  await mkdir('../../.agent/refactorfe-phase6/screenshots', { recursive: true });
  await page.screenshot({ path: '../../.agent/refactorfe-phase6/screenshots/operations-390.png', fullPage: true,
    mask: [page.getByRole('textbox'), page.getByRole('table')] });
  await page.getByRole('button', { name: 'Phân công ML-OPS-PENDING' }).click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect.poll(() => page.evaluate(() => !!document.activeElement?.closest('.ant-modal-root'))).toBe(true);
  for (let index = 0; index < 6; index++) {
    await page.keyboard.press('Tab');
    const focus = await page.evaluate(() => ({ inModal: !!document.activeElement?.closest('.ant-modal-root'), element: document.activeElement?.tagName }));
    expect(focus, `Tab ${index}: ${focus.element}`).toMatchObject({ inModal: true });
  }
  await page.getByRole('dialog').press('Escape');
  await expect(page.getByRole('button', { name: 'Phân công ML-OPS-PENDING' })).toBeFocused();
  await page.getByRole('button', { name: 'Phân công ML-OPS-PENDING' }).click(); await chooseShipper(page, 'Driver A');
  await expect(page.getByRole('dialog')).toContainText('ngoài vùng'); await confirm(page, '/assign');
  await page.getByRole('tab', { name: 'Theo dõi vận hành' }).click();
  await page.getByRole('button', { name: 'Đổi shipper ML-OPS-PENDING' }).click(); await chooseShipper(page, 'Driver B');
  await page.getByRole('textbox', { name: 'Lý do đổi shipper' }).fill('Rebalance'); await confirm(page, '/reassign');
  await page.getByRole('button', { name: 'Hủy phân công ML-OPS-PENDING' }).click();
  await page.getByRole('textbox', { name: 'Lý do hủy phân công' }).fill('Replan'); await confirm(page, '/cancel-assignment');
  await page.getByRole('tab', { name: 'Chờ phân công' }).click();
  await page.getByRole('button', { name: 'Phân công ML-OPS-PENDING' }).click(); await chooseShipper(page, 'Driver A'); await confirm(page, '/assign');
  await page.getByRole('tab', { name: 'Theo dõi vận hành' }).click();
  for (const status of ['PickingUp', 'PickedUp', 'InTransit', 'Delivering', 'Delivered']) {
    await page.getByRole('button', { name: 'Cập nhật ML-OPS-PENDING' }).click();
    const select = page.getByRole('combobox', { name: 'Trạng thái mới' });
    await select.focus(); await select.press('ArrowDown'); await select.press('Enter');
    await expect(page.getByRole('dialog').locator('.ant-select')).toContainText(status);
    await confirm(page, '/status');
  }
  await page.getByRole('button', { name: 'Thu COD ML-OPS-PENDING' }).click(); await confirm(page, '/cod/collect');
  await expect(page.getByRole('button', { name: 'Thu COD ML-OPS-PENDING' })).toHaveCount(0);
  expect((await page.request.get(new URL('/api/v1/app/operations/cod/settlement-candidates', page.url()).href)).status()).toBe(403);
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth); expect(overflow).toBe(false);
});
test('Admin settlement and alias use React without exposing proof', async ({ page }) => {
  await login(page, 'browser-admin@example.test');
  await page.goto('./operations/assignment?view=cod');
  await expect(page.getByRole('heading', { level: 1, name: 'Điều phối giao nhận' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Đối soát ML-OPS-SETTLE' })).toBeVisible();
  await mkdir('../../.agent/refactorfe-phase6/screenshots', { recursive: true });
  await page.screenshot({ path: '../../.agent/refactorfe-phase6/screenshots/cod-1280.png', fullPage: true, mask: [page.getByRole('table')] });
  await page.getByRole('button', { name: 'Đối soát ML-OPS-SETTLE' }).click(); await confirm(page, '/cod/settle');
  await expect(page.getByRole('button', { name: 'Đối soát ML-OPS-SETTLE' })).toHaveCount(0);
  await expect(page.getByText('Proof of delivery')).toHaveCount(0);
});
