import { expect, type Page } from '@playwright/test';
export async function adminLogin(page: Page, path: string) {
  await page.goto(`./login?returnUrl=${encodeURIComponent(path)}`);
  await page.getByLabel('Email', { exact: true }).fill('browser-admin@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.MINILOGISTICS_E2E_PASSWORD!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`^https://[^/]+(?:/app)?${path.split('?')[0]}`));
}
export async function confirmAdmin(page: Page, suffix: string, method: string) {
  const response = page.waitForResponse(
    (r) => new URL(r.url()).pathname.endsWith(suffix) && r.request().method() === method,
  );
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
  expect((await response).ok()).toBe(true);
}
