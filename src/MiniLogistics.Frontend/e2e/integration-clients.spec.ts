import { expect, test } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
import { integrationLogin, integrationConfirm } from './integration-helpers';
test.skip(!process.env.MINILOGISTICS_E2E_IDENTITY_FIXTURE, 'Requires isolated Identity/Application fixture.');
test('Integration client lifecycle preserves scopes, transient keys and real revocation', async ({
  page,
}) => {
  test.setTimeout(90000);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.context().grantPermissions(['clipboard-read', 'clipboard-write']);
  await integrationLogin(page, 'browser-integration-owner@example.test');
  await page.getByRole('textbox', { name: 'Tên API client', exact: true }).fill('Browser Portal Client');
  await page.getByRole('button', { name: 'Tạo API client', exact: true }).click();
  await integrationConfirm(page, '/integrations/clients', 'POST');
  const keyInput = page.getByLabel('API key mới', { exact: true });
  await expect(keyInput).toBeVisible();
  const firstKey = await keyInput.inputValue();
  expect(firstKey.startsWith('ml_test_')).toBe(true);
  await page.getByRole('button', { name: 'Sao chép API key', exact: true }).click();
  await expect(page.getByRole('dialog').getByRole('status')).toContainText('Đã sao chép API key.');
  await page.getByRole('button', { name: 'Đã lưu key, đóng', exact: true }).click();
  await expect(keyInput).toHaveCount(0);
  await expect(page.getByText('Client: Browser Portal Client', { exact: true })).toBeVisible();
  const security = page.locator('#integration-security');
  for (const name of ['CreateShipment', 'CancelShipment', 'WebhookManage'])
    await security.getByRole('checkbox', { name, exact: true }).uncheck();
  await security.getByRole('button', { name: 'Lưu quyền API', exact: true }).click();
  await integrationConfirm(page, '/security', 'PUT');
  await page.getByRole('button', { name: 'Thu hồi API client', exact: true }).click();
  await integrationConfirm(page, '/active', 'PUT');
  const revoked = await page.request.get(new URL('/api/v1/partner/shipments/ML-NOT-FOUND', page.url()).href, {
    headers: { Authorization: `Bearer ${firstKey}` },
  });
  expect(revoked.status()).toBe(403);
  expect((await revoked.json()).error.code).toBe('PartnerApi.ApiClientInactive');
  await page.getByRole('button', { name: 'Rotate API key', exact: true }).click();
  await integrationConfirm(page, '/rotate-key', 'POST');
  await expect(keyInput).toBeVisible();
  const replacement = await keyInput.inputValue();
  expect(replacement !== firstKey).toBe(true);
  await page.goBack();
  await expect(keyInput).toHaveCount(0);
  await page.goForward();
  await expect(keyInput).toHaveCount(0);
  await expect(page.getByText('Client: Browser Portal Client', { exact: true })).toBeVisible();
  const oldKey = await page.request.get(new URL('/api/v1/partner/shipments/ML-NOT-FOUND', page.url()).href, {
    headers: { Authorization: `Bearer ${firstKey}` },
  });
  expect(oldKey.status()).toBe(401);
  expect((await oldKey.json()).error.code).toBe('PartnerApi.InvalidApiKey');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await expect(page.getByLabel(/Signing secret mới/)).toHaveValue('');
  await mkdir('../../.agent/refactorfe-phase10/screenshots', { recursive: true });
  await page.screenshot({
    path: '../../.agent/refactorfe-phase10/screenshots/integration-390.png',
    fullPage: true,
    mask: [page.locator('.session-tools')],
  });
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.screenshot({
    path: '../../.agent/refactorfe-phase10/screenshots/integration-1280.png',
    fullPage: true,
    mask: [page.locator('.session-tools')],
  });
});
test('Integration manager sees only granted province and cannot forge another Shop', async ({ page }) => {
  await integrationLogin(page, 'browser-integration-manager@example.test');
  const result = await (
    await page.request.get(new URL('/api/v1/app/integrations/dashboard', page.url()).href)
  ).json();
  expect(result.granularPermissionEnabled).toBe(true);
  expect(result.shops.length).toBe(1);
  expect(result.shops[0].shopName).toBe('Portal Shop');
  const foreign = await page.request.get(
    new URL(
      `/api/v1/app/integrations/dashboard?shopId=${process.env.MINILOGISTICS_E2E_FOREIGN_INTEGRATION_SHOP}`,
      page.url(),
    ).href,
  );
  expect(foreign.status()).toBe(403);
  await expect(page.getByText('Excluded Portal Shop', { exact: true })).toHaveCount(0);
});
