import { test, expect, type Page } from '@playwright/test';

test.skip(!process.env.MINILOGISTICS_E2E_IDENTITY_FIXTURE, 'Requires isolated Identity/Application fixture.');
async function login(page: Page, email: string, destination: string) {
  await page.goto(`./login?returnUrl=${encodeURIComponent(destination)}`);
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.MINILOGISTICS_E2E_PASSWORD!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`^https://[^/]+(?:/app)?${destination.replaceAll('/', '\\/')}(?:\\?|$)`));
}
async function confirm(page: Page, suffix: string) {
  const response = page.waitForResponse(item => item.request().method() === 'POST' && new URL(item.url()).pathname.endsWith(suffix));
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
  expect((await response).status()).toBe(204);
}

test('cross-role Shop create Operator assign Shipper failed retry deliver collect Admin settle', async ({ browser }) => {
  test.setTimeout(120_000); // One complete lifecycle across four isolated real-cookie sessions.
  const contexts = await Promise.all(Array.from({ length: 4 }, () => browser.newContext({ ignoreHTTPSErrors: true, baseURL: process.env.MINILOGISTICS_E2E_BASE_URL })));
  try {
    const [shop, operator, shipper, admin] = await Promise.all(contexts.map(context => context.newPage()));
    await login(shop, 'browser-cross-shop@example.test', '/dashboard');
    await expect(shop).toHaveURL(/shopId=[0-9a-f-]{36}/);
    const shopId = new URL(shop.url()).searchParams.get('shopId')!;
    await shop.goto(`./shipments/create?shopId=${shopId}`);
    await expect(shop.getByRole('textbox', { name: 'Tên người gửi', exact: true })).toHaveValue('Browser Cross Shop');
    await shop.getByRole('button', { name: 'Tạo đơn hàng', exact: true }).click();
    await expect(shop.getByRole('textbox', { name: 'Tên người nhận', exact: true })).toBeFocused();
    await shop.getByRole('textbox', { name: 'Tên người nhận', exact: true }).fill('Cross-role Receiver');
    await shop.getByRole('textbox', { name: 'Điện thoại người nhận', exact: true }).fill('0901234567');
    const delivery = shop.getByRole('group', { name: 'Địa chỉ giao hàng', exact: true });
    await delivery.getByRole('textbox', { name: 'Địa chỉ đường', exact: true }).fill('Synthetic cross-role street');
    const province = delivery.getByRole('combobox', { name: 'Tỉnh / Thành phố', exact: true });
    await province.click(); await province.fill('Thành phố Hà Nội'); await province.press('Enter');
    const ward = delivery.getByRole('combobox', { name: 'Phường / Xã', exact: true });
    await ward.click(); await ward.press('ArrowDown'); await ward.press('Enter');
    await shop.getByRole('spinbutton', { name: 'Tiền thu hộ COD', exact: true }).fill('100000');
    await expect(shop.getByRole('complementary', { name: 'Phí dự kiến' })).toContainText('25.000 VND');
    await expect(shop.getByRole('textbox', { name: 'Tên người nhận', exact: true })).toHaveValue('Cross-role Receiver');
    const create = shop.waitForResponse(response => response.request().method() === 'POST' && /\/shops\/[^/]+\/shipments$/.test(new URL(response.url()).pathname));
    await shop.getByRole('button', { name: 'Tạo đơn hàng', exact: true }).click();
    const createdResponse = await create; expect(createdResponse.status()).toBe(201);
    const created = await createdResponse.json(); expect(created.status).toBe('PendingPickup');
    await expect(shop.getByRole('heading', { name: 'Chi tiết vận đơn' })).toBeVisible();

    await login(operator, 'browser-operator@example.test', '/operations/assignments');
    await operator.getByRole('textbox', { name: 'Tìm vận đơn / người nhận' }).fill(created.trackingCode);
    await operator.getByRole('button', { name: 'Áp dụng', exact: true }).click();
    await operator.getByRole('button', { name: `Phân công ${created.trackingCode}` }).click();
    const driver = operator.getByRole('dialog').getByRole('combobox', { name: 'Chọn shipper' });
    await driver.click(); await driver.fill('Cross Driver'); await driver.press('Enter');
    await expect(operator.getByRole('dialog')).toContainText('Cross Driver');
    await confirm(operator, '/assign');
    expect((await operator.request.get(new URL('/api/v1/app/operations/cod/settlement-candidates', operator.url()).href)).status()).toBe(403);

    await login(shipper, 'browser-cross-shipper@example.test', '/shipper/shipments');
    const task = shipper.getByRole('article', { name: created.trackingCode, exact: true });
    for (const [status, label] of [['PickingUp', 'Đang lấy hàng'], ['PickedUp', 'Đã lấy hàng'], ['InTransit', 'Đang vận chuyển'],
      ['Delivering', 'Đang giao'], ['DeliveryFailed', 'Giao thất bại'], ['Delivering', 'Đang giao'], ['Delivered', 'Đã giao']]) {
      await expect(task.getByRole('button', { name: `Cập nhật ${created.trackingCode}` })).toBeEnabled();
      const select = task.getByRole('combobox', { name: 'Trạng thái mới' });
      await select.click(); await select.fill(label); await select.press('Enter');
      await expect(task.locator('.ant-select').first()).toContainText(label);
      if (status === 'DeliveryFailed') {
        const reason = task.getByRole('combobox', { name: 'Mã lý do' });
        await reason.click(); await reason.fill('Không có người nhận'); await reason.press('Enter');
        await task.getByRole('textbox', { name: 'Ghi chú' }).fill('Cross-role receiver unavailable');
      }
      await task.getByRole('button', { name: `Cập nhật ${created.trackingCode}` }).click();
      await confirm(shipper, `/shipments/${created.shipmentId}/status`);
    }
    await expect(task.getByRole('button', { name: `Thu COD ${created.trackingCode}` })).toBeEnabled();
    const pending = await (await shipper.request.get(new URL('/api/v1/app/shipper/me/workspace?stage=CodPending', shipper.url()).href)).json();
    expect(pending.items).toEqual(expect.arrayContaining([expect.objectContaining({ shipmentId: created.shipmentId, status: 'Delivered', codStatus: 'PendingCollection' })]));
    await task.getByRole('textbox', { name: 'Số thực thu', exact: true }).fill('99000');
    await task.getByRole('textbox', { name: 'Ghi chú thu COD', exact: true }).fill('Cross-role COD shortfall');
    await task.getByRole('button', { name: `Thu COD ${created.trackingCode}` }).click();
    await confirm(shipper, `/shipments/${created.shipmentId}/cod/collect`);
    await expect(task).toHaveCount(0);

    await login(admin, 'browser-admin@example.test', '/operations/assignments');
    await admin.getByRole('tab', { name: /Đối soát/ }).click();
    await admin.getByRole('button', { name: `Đối soát ${created.trackingCode}` }).click();
    await confirm(admin, `/shipments/${created.shipmentId}/cod/settle`);
    await expect(admin.getByRole('button', { name: `Đối soát ${created.trackingCode}` })).toHaveCount(0);
  } finally { await Promise.all(contexts.map(context => context.close())); }
});
