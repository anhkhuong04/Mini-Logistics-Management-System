import { expect, test } from '@playwright/test';
import { integrationLogin, integrationConfirm } from './integration-helpers';
test.skip(!process.env.MINILOGISTICS_E2E_IDENTITY_FIXTURE, 'Requires isolated Identity/Application fixture.');
test('Integration webhook lifecycle validates, queues tests and retries safe delivery/outbox history', async ({
  page,
}) => {
  test.setTimeout(90000);
  await integrationLogin(page, 'browser-webhook-owner@example.test');
  await page.getByRole('button', { name: 'Chọn Existing client', exact: true }).click();
  const form = page.locator('#integration-webhook');
  const input = form.getByLabel(/Signing secret mới/);
  const signing = `Browser-signing-${Date.now()}-${Math.random()}`;
  await form.getByRole('textbox', { name: 'URL webhook', exact: true }).fill('https://127.0.0.1/hook');
  await input.fill(signing);
  await form.getByRole('button', { name: 'Lưu và kích hoạt webhook', exact: true }).click();
  await expect(input).toHaveValue('');
  const denied = page.waitForResponse((r) => r.request().method() === 'PUT' && r.url().endsWith('/webhook'));
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
  expect((await denied).status()).toBe(400);
  await form
    .getByRole('textbox', { name: 'URL webhook', exact: true })
    .fill('https://public.example.test/updated');
  await input.fill(signing);
  await form.getByRole('button', { name: 'Lưu và kích hoạt webhook', exact: true }).click();
  await expect(input).toHaveValue('');
  await integrationConfirm(page, '/webhook', 'PUT');
  await page.getByRole('button', { name: 'Xếp hàng test webhook', exact: true }).click();
  await integrationConfirm(page, '/webhook/test', 'POST');
  const retry = page.getByRole('button', { name: /^Retry delivery / });
  await expect(retry).toHaveCount(1);
  await retry.click();
  await integrationConfirm(page, '/retry', 'POST');
  await expect(retry).toHaveCount(0);
  const outbox = page.getByRole('button', { name: /^Retry outbox / });
  await expect(outbox).toHaveCount(1);
  await outbox.click();
  await integrationConfirm(page, '/retry', 'POST');
  await expect(outbox).toHaveCount(0);
  await expect(page.getByText('webhook.test', { exact: true }).first()).toBeVisible();
  await expect(page.locator('body')).not.toContainText('raw-error-secret');
  await expect(page.locator('body')).not.toContainText('raw-outbox-secret');
  const data = await (
    await page.request.get(new URL('/api/v1/app/integrations/dashboard', page.url()).href)
  ).json();
  expect(JSON.stringify(data).includes(signing)).toBe(false);
  expect(JSON.stringify(data).includes('raw-payload-secret')).toBe(false);
});
