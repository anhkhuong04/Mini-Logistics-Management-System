import { expect, test } from '@playwright/test';
import { adminLogin, confirmAdmin } from './admin-helpers';
test.skip(!process.env.MINILOGISTICS_E2E_IDENTITY_FIXTURE, 'Requires isolated Identity/Application fixture.');
test('Admin banner upload failure blocks metadata; valid image supports full lifecycle', async ({ page }) => {
  test.setTimeout(90000);
  await page.setViewportSize({ width: 390, height: 844 });
  await adminLogin(page, '/admin/banners');
  await page.getByRole('textbox', { name: 'Tiêu đề banner', exact: true }).fill('Phase9 Banner');
  await page
    .locator('input[type=file]')
    .setInputFiles({ name: 'bad.png', mimeType: 'image/png', buffer: Buffer.from('not an image') });
  await expect(page.getByRole('alert')).toContainText('Upload thất bại');
  await expect(page.getByRole('button', { name: 'Lưu banner', exact: true })).toBeDisabled();
  const png = Buffer.from(
    'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jD1cAAAAASUVORK5CYII=',
    'base64',
  );
  await page
    .locator('input[type=file]')
    .setInputFiles({ name: 'banner.png', mimeType: 'image/png', buffer: png });
  await expect(page.getByRole('textbox', { name: 'Đường dẫn ảnh', exact: true })).toHaveValue(
    /^\/images\/banners\//,
  );
  await page.getByRole('button', { name: 'Lưu banner', exact: true }).click();
  await confirmAdmin(page, '/admin/banners', 'POST');
  await page.getByRole('button', { name: 'Sửa Phase9 Banner', exact: true }).click();
  await page.getByRole('spinbutton', { name: 'Thứ tự banner', exact: true }).fill('4');
  await page.getByRole('button', { name: 'Lưu banner', exact: true }).click();
  const saved = page.waitForResponse(
    (r) => r.request().method() === 'PUT' && r.url().includes('/admin/banners/'),
  );
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
  expect((await saved).ok()).toBe(true);
  await page.getByRole('button', { name: 'Đổi hoạt động Phase9 Banner', exact: true }).click();
  await confirmAdmin(page, '/toggle', 'POST');
  const publicBanners = await (
    await page.request.get(new URL('/api/v1/public/banners', page.url()).href)
  ).json();
  expect(publicBanners.some((row: { title: string }) => row.title === 'Phase9 Banner')).toBe(false);
  await page.getByRole('button', { name: 'Xóa Phase9 Banner', exact: true }).click();
  const deleted = page.waitForResponse(
    (r) => r.request().method() === 'DELETE' && r.url().includes('/admin/banners/'),
  );
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
  expect((await deleted).ok()).toBe(true);
  await expect(page.getByRole('button', { name: 'Sửa Phase9 Banner', exact: true })).toHaveCount(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});
