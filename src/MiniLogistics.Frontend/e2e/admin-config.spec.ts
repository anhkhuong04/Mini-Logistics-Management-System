import { expect, test } from '@playwright/test';
import { adminLogin, confirmAdmin } from './admin-helpers';
test.skip(!process.env.MINILOGISTICS_E2E_IDENTITY_FIXTURE, 'Requires isolated Identity/Application fixture.');
test('Admin hub and configuration changes create new versions', async ({ page }) => {
  test.setTimeout(90000);
  await adminLogin(page, '/admin/hubs');
  await page.getByRole('textbox', { name: 'Mã Hub', exact: true }).fill('PHASE9-E2E');
  await page.getByRole('textbox', { name: 'Tên Hub', exact: true }).fill('Phase9 Hub');
  await page.locator('#admin-hub_province').fill('Phase9 Synthetic Province');
  await page.getByRole('button', { name: 'Lưu Hub', exact: true }).click();
  await confirmAdmin(page, '/admin/hubs', 'POST');
  await expect(page.getByText('Phase9 Hub', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Sửa PHASE9-E2E', exact: true }).click();
  await page.getByRole('textbox', { name: 'Tên Hub', exact: true }).fill('Phase9 Updated Hub');
  await page.getByRole('button', { name: 'Lưu Hub', exact: true }).click();
  const updated = page.waitForResponse(
    (r) => r.request().method() === 'PUT' && /\/admin\/hubs\/[a-f0-9-]+$/.test(new URL(r.url()).pathname),
  );
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
  expect((await updated).ok()).toBe(true);
  await page.getByRole('button', { name: 'Đổi hoạt động PHASE9-E2E', exact: true }).click();
  await confirmAdmin(page, '/active', 'PUT');
  await page.getByRole('link', { name: 'Cấu hình', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Cấu hình hệ thống', exact: true })).toBeVisible();
  await page
    .getByRole('textbox', { name: 'Tỉnh / Thành phố', exact: true })
    .fill('Phase9 Synthetic Province');
  await page.getByRole('textbox', { name: 'Vùng', exact: true }).fill('Phase9 Region');
  await page.getByRole('button', { name: 'Lưu vùng tuyến', exact: true }).click();
  await confirmAdmin(page, '/routes', 'PUT');
  await page.getByRole('button', { name: 'Lưu biểu phí mới', exact: true }).click();
  await confirmAdmin(page, '/fees', 'POST');
  const result = await (
    await page.request.get(new URL('/api/v1/app/admin/configuration', page.url()).href)
  ).json();
  expect(
    result.routeRegions.some(
      (row: { province: string; region: string; isActive: boolean }) =>
        row.province === 'Phase9 Synthetic Province' && row.region === 'Phase9 Region' && row.isActive,
    ),
  ).toBe(true);
  expect(
    result.feeRules.some(
      (row: { routeType: string; isActive: boolean }) => row.routeType === 'InterRegion' && row.isActive,
    ),
  ).toBe(true);
});
