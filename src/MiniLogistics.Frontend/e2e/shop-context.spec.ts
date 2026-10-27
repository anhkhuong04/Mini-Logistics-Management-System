import { test, expect } from '@playwright/test';
test.skip(
  !process.env.MINILOGISTICS_E2E_IDENTITY_FIXTURE,
  'Requires the isolated .NET Identity browser fixture.',
);

test('owner switches Shop via URL, then staff login receives masked data and limited navigation', async ({
  page,
}) => {
  await page.goto('./login');
  await page.getByLabel('Email', { exact: true }).fill('browser-owner@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.MINILOGISTICS_E2E_PASSWORD!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/^https:\/\/[^/]+\/(?:app\/)?dashboard(?:\?|$)/);
  await page.goto('./dashboard');
  await page.getByRole('combobox', { name: 'Shop hiện tại', exact: true }).click();
  await page.getByTitle('Browser Shop B', { exact: true }).click();
  await expect(page).toHaveURL(/shopId=/);
  const selected = new URL(page.url()).searchParams.get('shopId');
  await page.reload();
  await expect(page.getByRole('region', { name: 'Shop hiện tại' })).toContainText('Browser Shop B');
  const menu = page.getByRole('menu', { name: 'Điều hướng' });
  await expect(menu.getByRole('link', { name: 'Nhân viên Shop' })).toBeVisible();
  await page.getByRole('button', { name: 'Đăng xuất', exact: true }).click();
  await page.goto('./login');
  await page.getByLabel('Email', { exact: true }).fill('browser-staff@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.MINILOGISTICS_E2E_PASSWORD!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/^https:\/\/[^/]+\/(?:app\/)?dashboard(?:\?|$)/);
  await page.goto('./dashboard');
  await expect(page.getByRole('region', { name: 'Shop hiện tại' })).toContainText('Browser Shop A');
  await expect(page.getByRole('region', { name: 'Shop hiện tại' })).not.toContainText('0901234567');
  await expect(menu.getByRole('link', { name: 'Nhân viên Shop' })).toHaveCount(0);
  await expect(menu.getByRole('link', { name: 'Báo cáo COD' })).toHaveCount(0);
  const denied = page.waitForResponse((response) => {
    const url = new URL(response.url());
    return url.pathname === '/api/v1/app/shop-context' && url.searchParams.get('shopId') === selected;
  });
  await page.goto(`./dashboard?shopId=${selected}`);
  expect((await denied).status()).toBe(403);
  await expect(page.getByText('Kh\u00f4ng c\u00f3 quy\u1ec1n truy c\u1eadp', { exact: true })).toBeVisible();
  await expect(page.getByText('Browser Shop B', { exact: true })).toHaveCount(0);
  await page.context().clearCookies();
  await page.goto('./dashboard');
  await expect(page).toHaveURL(/^https:\/\/[^/]+\/(?:app\/)?login\?/);
  await expect(page.getByText('Browser Shop A', { exact: true })).toHaveCount(0);
  await expect(page.getByText('Browser Shop B', { exact: true })).toHaveCount(0);
});
