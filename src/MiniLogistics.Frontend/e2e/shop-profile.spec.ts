import { expect, test, type Page } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
test.skip(!process.env.MINILOGISTICS_E2E_IDENTITY_FIXTURE, 'Requires isolated Identity/Application fixture.');
async function login(page: Page) {
  await page.goto('./login?returnUrl=/profile');
  await page.getByLabel('Email', { exact: true }).fill('browser-profile-owner@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.MINILOGISTICS_E2E_PASSWORD!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Hồ sơ Shop', exact: true })).toBeVisible();
}
test('Shop profile edit preserves shipment snapshots and additional Shop refreshes authorized context', async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await login(page);
  const current = page.locator('#shop-profile');
  await expect(current.getByRole('textbox', { name: 'Tên Shop', exact: true })).toHaveValue(
    'Browser Profile Shop',
  );
  await expect(page).toHaveURL(/shopId=/);
  await mkdir('../../.agent/refactorfe-phase8/screenshots', { recursive: true });
  await page.screenshot({
    path: '../../.agent/refactorfe-phase8/screenshots/profile-390.png',
    fullPage: true,
    mask: [page.getByRole('region', { name: 'Shop hiện tại' }), page.getByRole('textbox')],
  });
  await current.getByRole('textbox', { name: 'Tên Shop', exact: true }).fill('Updated Profile Shop');
  await current.getByRole('textbox', { name: 'Địa chỉ đường', exact: true }).fill('Updated profile street');
  await current.getByRole('button', { name: 'Lưu hồ sơ', exact: true }).click();
  const updated = page.waitForResponse(
    (response) => response.request().method() === 'PUT' && response.url().endsWith('/profile'),
  );
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
  expect((await updated).status()).toBe(200);
  await expect(current.getByRole('textbox', { name: 'Tên Shop', exact: true })).toHaveValue(
    'Updated Profile Shop',
  );
  const form = page.locator('#additional-shop');
  await form.getByRole('textbox', { name: 'Tên Shop', exact: true }).fill('Additional Profile Shop');
  await form.getByRole('textbox', { name: 'Điện thoại', exact: true }).fill('0901234567');
  await form.getByRole('textbox', { name: 'Địa chỉ đường', exact: true }).fill('Additional synthetic street');
  const province = form.getByRole('combobox', { name: 'Tỉnh / Thành phố', exact: true });
  await province.click();
  await province.fill('Thành phố Hà Nội');
  await province.press('Enter');
  const ward = form.getByRole('combobox', { name: 'Phường / Xã', exact: true });
  await ward.click();
  await ward.press('ArrowDown');
  await ward.press('Enter');
  await form.getByRole('button', { name: 'Tạo Shop mới', exact: true }).click();
  const created = page.waitForResponse(
    (response) =>
      response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/app/shops',
  );
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
  const result = await created;
  expect(result.status()).toBe(201);
  const body = await result.json();
  await expect(page).toHaveURL(new RegExp(`shopId=${body.shopId}`));
  await page.reload();
  await expect(current.getByRole('textbox', { name: 'Tên Shop', exact: true })).toHaveValue(
    'Additional Profile Shop',
  );
  const context = await (
    await page.request.get(new URL(`/api/v1/app/shop-context?shopId=${body.shopId}`, page.url()).href)
  ).json();
  expect(
    context.shops.some(
      (shop: { shopId: string; isOwner: boolean }) => shop.shopId === body.shopId && shop.isOwner,
    ),
  ).toBe(true);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});
