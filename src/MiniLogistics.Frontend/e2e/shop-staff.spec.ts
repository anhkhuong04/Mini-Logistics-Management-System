import { expect, test, type Page } from '@playwright/test';
test.skip(!process.env.MINILOGISTICS_E2E_IDENTITY_FIXTURE, 'Requires isolated Identity/Application fixture.');
async function login(page: Page, email: string, route: string) {
  await page.goto(`./login?returnUrl=${encodeURIComponent(route)}`);
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.MINILOGISTICS_E2E_PASSWORD!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`^https://[^/]+(?:/app)?${route}(?:\\?|$)`));
  await expect(page).toHaveURL(/shopId=/);
}
test('Shop staff owner edits by user ID and revoked rights deny an existing cookie session', async ({
  page,
  browser,
}) => {
  const context = await browser.newContext({
    baseURL: process.env.MINILOGISTICS_E2E_BASE_URL,
    ignoreHTTPSErrors: true,
  });
  try {
    const staff = await context.newPage();
    await login(staff, 'browser-settings-staff@example.test', '/dashboard');
    const shopId = new URL(staff.url()).searchParams.get('shopId')!;
    await login(page, 'browser-staff-owner@example.test', '/shop/staff');
    await page.getByRole('button', { name: 'Sửa Settings Staff' }).click();
    await page.getByRole('combobox', { name: 'Vai trò', exact: true }).click();
    await page.getByTitle('Analyst', { exact: true }).click();
    await page.getByRole('button', { name: 'Lưu quyền', exact: true }).click();
    const response = page.waitForResponse(
      (response) => response.request().method() === 'PUT' && response.url().includes('/staff/'),
    );
    await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
    expect((await response).status()).toBe(200);
    expect(
      (await staff.request.get(new URL(`/api/v1/app/shops/${shopId}/cod-report`, staff.url()).href)).status(),
    ).toBe(200);
    await staff.goto(`./shop/cod-report?shopId=${shopId}`);
    await expect(staff.getByRole('heading', { name: 'Báo cáo COD', exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Sửa Settings Staff' }).click();
    await page.getByRole('combobox', { name: 'Vai trò', exact: true }).click();
    await page.getByTitle('Viewer', { exact: true }).click();
    await page.getByRole('button', { name: 'Lưu quyền', exact: true }).click();
    const revoked = page.waitForResponse(
      (response) => response.request().method() === 'PUT' && response.url().includes('/staff/'),
    );
    await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
    expect((await revoked).status()).toBe(200);
    expect(
      (await staff.request.get(new URL(`/api/v1/app/shops/${shopId}/cod-report`, staff.url()).href)).status(),
    ).toBe(403);
    expect(
      (await staff.request.get(new URL(`/api/v1/app/shops/${shopId}/audit`, staff.url()).href)).status(),
    ).toBe(403);
    await staff.reload();
    await expect(staff.getByText('Không có quyền truy cập Shop', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Sửa Settings Staff' }).click();
    await page.getByRole('switch', { name: 'Nhân viên hoạt động' }).uncheck();
    await page.getByRole('button', { name: 'Lưu quyền', exact: true }).click();
    const inactive = page.waitForResponse(
      (response) => response.request().method() === 'PUT' && response.url().includes('/staff/'),
    );
    await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
    expect((await inactive).status()).toBe(200);
    expect(
      (await staff.request.get(new URL(`/api/v1/app/shops/${shopId}/profile`, staff.url()).href)).status(),
    ).toBe(403);
    await expect(page.getByRole('button', { name: 'Tạo nhân viên', exact: true })).toBeEnabled();
    await page.getByRole('textbox', { name: 'Họ tên', exact: true }).fill('Created Settings Staff');
    await page
      .getByRole('textbox', { name: 'Email nhân viên', exact: true })
      .fill('browser-created-settings-staff@example.test');
    await page.getByRole('textbox', { name: 'Điện thoại nhân viên', exact: true }).fill('0901234567');
    await page.getByLabel(/Mật khẩu khởi tạo/).fill(process.env.MINILOGISTICS_E2E_PASSWORD!);
    await page.getByRole('button', { name: 'Tạo nhân viên', exact: true }).click();
    await expect(page.getByLabel(/Mật khẩu khởi tạo/)).toHaveValue('');
    const created = page.waitForResponse(
      (response) =>
        response.request().method() === 'POST' && new URL(response.url()).pathname.endsWith('/staff'),
    );
    await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
    const createdResponse = await created;
    expect(createdResponse.status()).toBe(200);
    expect(await createdResponse.text()).not.toContain(process.env.MINILOGISTICS_E2E_PASSWORD!);
    await expect(page.getByRole('button', { name: 'Sửa Created Settings Staff' })).toBeVisible();
  } finally {
    await context.close();
  }
});
