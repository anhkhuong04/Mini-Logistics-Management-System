import { expect, type Page } from '@playwright/test';
export async function integrationLogin(page: Page, email: string) {
  await page.goto('./login?returnUrl=/partner/integrations');
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.MINILOGISTICS_E2E_PASSWORD!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Quản lý tích hợp', exact: true })).toBeVisible();
  await expect(page).toHaveURL(/shopId=/);
}
export async function integrationConfirm(page: Page, suffix: string, method: string) {
  const confirmation = page.getByRole('dialog', { name: 'Xác nhận thao tác tích hợp?', exact: true });
  const result = page.waitForResponse(
    (r) => new URL(r.url()).pathname.endsWith(suffix) && r.request().method() === method,
  );
  await confirmation.getByRole('button', { name: 'Xác nhận', exact: true }).click();
  expect((await result).ok()).toBe(true);
  // The next action must not target the previous dialog while its portal is closing.
  await expect(confirmation).toBeHidden();
}
