import { test, expect } from '@playwright/test';
test.skip(!process.env.MINILOGISTICS_E2E_IDENTITY_FIXTURE, 'Requires the isolated .NET Identity browser fixture.');

test('registration signs in with a real cookie, reload keeps session, logout clears it', async ({ page, context }) => {
  await page.goto('./register-shop');
  await page.getByLabel('Họ và tên', { exact: true }).fill('Browser Registration');
  await page.getByLabel('Email', { exact: true }).fill(`browser-new-${crypto.randomUUID()}@example.test`);
  await page.getByLabel('Tên Shop', { exact: true }).fill('Registered Browser Shop');
  await page.getByLabel('Số điện thoại', { exact: true }).fill('0901234567');
  await page.getByLabel('Địa chỉ', { exact: true }).fill('Test registration address');
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.MINILOGISTICS_E2E_PASSWORD!);
  await page.getByLabel('Tỉnh / Thành phố').click();
  await page.getByLabel('Tỉnh / Thành phố').press('ArrowDown');
  await page.getByLabel('Tỉnh / Thành phố').press('Enter');
  await page.getByLabel('Phường / Xã').click();
  await page.getByLabel('Phường / Xã').press('ArrowDown');
  await page.getByLabel('Phường / Xã').press('Enter');
  await page.getByRole('button', { name: 'Đăng ký Shop', exact: true }).click();
  await expect(page).toHaveURL(/\/app\/dashboard(?:\?|$)/);
  await page.goto('./');
  await expect(page.getByText('Registered Browser Shop', { exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByText('Registered Browser Shop', { exact: true })).toBeVisible();
  const cookie = (await context.cookies()).find(item => item.name === '.AspNetCore.Identity.Application');
  expect(cookie?.httpOnly).toBe(true);
  expect(cookie?.secure).toBe(true);
  const logout = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/app/auth/logout');
  await page.getByRole('button', { name: 'Đăng xuất', exact: true }).click();
  expect((await logout).status()).toBe(204);
  await expect(page.getByRole('button', { name: 'Đăng xuất', exact: true })).toHaveCount(0);
  expect((await context.cookies()).some(item => item.name === '.AspNetCore.Identity.Application')).toBe(false);
  await page.goto('./dashboard');
  await expect(page).toHaveURL(/\/app\/login\?/);
  await expect(page.getByRole('heading', { level: 1, name: 'Đăng nhập' })).toBeVisible();
});

test('login failure, remember-me, safe return URL and lockout use real Identity', async ({ page, context }) => {
  await page.goto('./login?returnUrl=https://outside.example');
  await page.getByLabel('Email', { exact: true }).fill('browser-other@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('WrongPassword!1');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Email hoặc mật khẩu không đúng');
  await expect(page.getByLabel('Mật khẩu', { exact: true })).toHaveValue('');
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.MINILOGISTICS_E2E_PASSWORD!);
  await page.getByLabel('Ghi nhớ đăng nhập').check();
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/\/app\/dashboard(?:\?|$)/);
  const cookie = (await context.cookies()).find(item => item.name === '.AspNetCore.Identity.Application');
  expect(cookie!.expires).toBeGreaterThan(Date.now() / 1000);
  await page.goto('./');
  await page.getByRole('button', { name: 'Đăng xuất', exact: true }).click();
  await page.goto('./login');
  await page.getByLabel('Email', { exact: true }).fill('browser-other@example.test');
  for (let attempt = 0; attempt < 5; attempt++) {
    await page.getByLabel('Mật khẩu', { exact: true }).fill('WrongPassword!1');
    const response = page.waitForResponse(response => response.url().endsWith('/auth/login') && response.request().method() === 'POST');
    await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
    await response;
    await expect(page.getByLabel('Mật khẩu', { exact: true })).toHaveValue('');
  }
  await expect(page.getByRole('alert')).toContainText('Tài khoản tạm khóa');
});
