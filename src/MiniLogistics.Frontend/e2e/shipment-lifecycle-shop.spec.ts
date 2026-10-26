import { test, expect, type Page } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
test.skip(!process.env.MINILOGISTICS_E2E_IDENTITY_FIXTURE, 'Requires isolated Identity/data fixture.');
async function login(page: Page, email = 'browser-mutation-owner@example.test') {
  await page.goto('./login'); await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.MINILOGISTICS_E2E_PASSWORD!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/^https:\/\/[^/]+\/(?:app\/)?dashboard(?:\?|$)/);
}
async function fillShipment(page: Page, receiver: string) {
  await page.goto('./shipments/create');
  await expect(page.getByRole('textbox', { name: 'Tên người gửi', exact: true })).toHaveValue('Browser Mutation Shop');
  await page.getByRole('button', { name: 'Tạo đơn hàng', exact: true }).click();
  await expect(page.getByRole('textbox', { name: 'Tên người nhận', exact: true })).toBeFocused();
  await page.getByRole('textbox', { name: 'Tên người nhận', exact: true }).fill(receiver);
  await page.getByRole('textbox', { name: 'Điện thoại người nhận', exact: true }).fill('0901234567');
  const delivery = page.getByRole('group', { name: 'Địa chỉ giao hàng', exact: true });
  await delivery.getByRole('textbox', { name: 'Địa chỉ đường', exact: true }).fill('Synthetic delivery street');
  await delivery.getByRole('combobox', { name: 'Tỉnh / Thành phố', exact: true }).click();
  await delivery.getByRole('combobox', { name: 'Tỉnh / Thành phố', exact: true }).fill('Thành phố Hà Nội');
  await delivery.getByRole('combobox', { name: 'Tỉnh / Thành phố', exact: true }).press('Enter');
  const ward = delivery.getByRole('combobox', { name: 'Phường / Xã', exact: true });
  await ward.click(); await ward.press('ArrowDown'); await ward.press('Enter');
  await page.getByRole('spinbutton', { name: 'Tiền thu hộ COD', exact: true }).fill('100000');
  await expect(page.getByRole('complementary', { name: 'Phí dự kiến' })).toContainText('25.000 VND');
  await mkdir('../../.agent/refactorfe-phase4/screenshots', { recursive: true });
  await page.screenshot({ path: `../../.agent/refactorfe-phase4/screenshots/form-${page.viewportSize()!.width}.png`, fullPage: true,
    mask: [page.getByRole('region', { name: 'Shop hiện tại' }), page.getByRole('textbox'), page.getByRole('spinbutton')] });
}
const write = (page: Page, method: string, suffix: string) => page.waitForResponse(response =>
  response.request().method() === method && new URL(response.url()).pathname.endsWith(suffix) && response.url().includes('/api/v1/app/shops/'));

test('real draft edit submit and cancel preserve server fees history and URL state', async ({ page }) => {
  await login(page); await fillShipment(page, 'Lifecycle Receiver');
  const draftResponse = write(page, 'POST', '/shipments/drafts');
  await page.getByRole('button', { name: 'Lưu nháp', exact: true }).click();
  const draft = await (await draftResponse).json(); expect(draft.status).toBe('Draft'); expect(draft.shippingFeeAmount).toBe('25000');
  await expect(page).toHaveURL(new RegExp(`^https://[^/]+(?:/app)?/shipments/${draft.shipmentId}\\?`));
  await page.getByRole('link', { name: 'Sửa vận đơn', exact: true }).click();
  await expect(page).toHaveURL(/shipmentId=/); await page.reload();
  await expect(page.getByRole('textbox', { name: 'Tên người nhận', exact: true })).toHaveValue('Lifecycle Receiver');
  await page.getByRole('spinbutton', { name: 'Khối lượng (kg)', exact: true }).fill('2');
  await expect(page.getByRole('complementary', { name: 'Phí dự kiến' })).toContainText('35.000 VND');
  const updatedResponse = write(page, 'PUT', `/shipments/${draft.shipmentId}`);
  await page.getByRole('button', { name: 'Lưu nháp', exact: true }).click();
  expect((await (await updatedResponse).json()).shippingFeeAmount).toBe('35000');
  await expect(page.getByRole('heading', { name: 'Chi tiết vận đơn' })).toBeVisible();
  await page.getByRole('button', { name: 'Gửi đơn', exact: true }).click();
  const submittedResponse = write(page, 'POST', `/shipments/${draft.shipmentId}/submit`);
  await page.getByRole('button', { name: 'Xác nhận', exact: true }).click();
  const submitted = await (await submittedResponse).json(); expect(submitted.status).toBe('PendingPickup'); expect(submitted.shippingFeeAmount).toBe('35000');
  await expect(page.getByRole('button', { name: 'Gửi đơn', exact: true })).toHaveCount(0);
  await page.getByRole('link', { name: 'Sửa vận đơn', exact: true }).click();
  await page.getByRole('textbox', { name: 'Tên người nhận', exact: true }).fill('Pending Updated Receiver');
  const pendingEdit = write(page, 'PUT', `/shipments/${draft.shipmentId}`);
  await page.getByRole('button', { name: 'Lưu thay đổi', exact: true }).click(); expect((await pendingEdit).status()).toBe(200);
  await page.getByRole('button', { name: 'Hủy đơn', exact: true }).click();
  await page.getByLabel('Lý do hủy', { exact: true }).fill('Synthetic cancellation');
  const cancelledResponse = write(page, 'POST', `/shipments/${draft.shipmentId}/cancel`);
  await page.getByRole('button', { name: 'Xác nhận', exact: true }).click(); expect((await cancelledResponse).status()).toBe(204);
  await expect(page.getByRole('button', { name: 'Hủy đơn', exact: true })).toHaveCount(0);
  await expect(page.getByText('Synthetic cancellation', { exact: true })).toBeVisible();
});

test('create double click sends one command and mobile form remains contained', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 900 }); await login(page); await fillShipment(page, 'Create Receiver');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  let commands = 0;
  await page.route('**/api/v1/app/shops/*/shipments', async route => {
    if (route.request().method() === 'POST') { commands++; await new Promise(resolve => setTimeout(resolve, 300)); }
    await route.continue();
  });
  const response = write(page, 'POST', '/shipments');
  await page.getByRole('button', { name: 'Tạo đơn hàng', exact: true }).dblclick();
  const created = await (await response).json(); expect(created.status).toBe('PendingPickup'); expect(created.shippingFeeAmount).toBe('25000');
  await expect(page).toHaveURL(new RegExp(`^https://[^/]+(?:/app)?/shipments/${created.shipmentId}\\?`)); expect(commands).toBe(1);
});

test('restricted staff cannot enter the mutation form or act on a shipment', async ({ page }) => {
  await login(page, 'browser-staff@example.test');
  await page.goto('./shipments/create'); await expect(page.getByText('Shop không hoạt động hoặc bạn không có quyền quản lý vận đơn.', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Tạo đơn hàng', exact: true })).toHaveCount(0);
  await page.goto('./shipments'); await page.getByRole('link', { name: 'ML-BROWSER-001', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Hủy đơn', exact: true })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Sửa vận đơn', exact: true })).toHaveCount(0);
});
