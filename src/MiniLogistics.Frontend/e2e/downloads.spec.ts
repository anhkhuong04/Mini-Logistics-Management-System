import { test, expect } from '@playwright/test';
test.skip(!process.env.MINILOGISTICS_E2E_IDENTITY_FIXTURE, 'Requires isolated Identity/data fixture.');
async function login(page: import('@playwright/test').Page, email: string) {
  await page.goto('./login'); await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.MINILOGISTICS_E2E_PASSWORD!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click(); await expect(page).toHaveURL(/^https:\/\/[^/]+\/(?:app\/)?dashboard(?:\?|$)/);
}
test('cookie downloads keep current filters CSV filename PDF binary and error feedback', async ({ page }) => {
  await login(page, 'browser-owner@example.test'); await page.goto('./shipments?trackingCode=ML-BROWSER-001&status=PendingPickup&toUtc=2026-10-01T23:59:59.9999999Z');
  const request = page.waitForRequest(request => request.url().includes('/shop/files/shipments/export.csv'));
  const csv = page.waitForEvent('download'); await page.getByRole('button', { name: 'Xuất CSV vận đơn', exact: true }).click();
  expect((await csv).suggestedFilename()).toMatch(/^shipments-.*\.csv$/);
  const query = new URL((await request).url()).searchParams; expect(query.get('trackingCode')).toBe('ML-BROWSER-001'); expect(query.get('status')).toBe('PendingPickup'); expect(query.get('toUtc')).toBe('2026-10-01T23:59:59.9999999Z'); expect(query.has('pageNumber')).toBe(false);
  await page.getByRole('link', { name: 'ML-BROWSER-001', exact: true }).click();
  const pdf = page.waitForEvent('download'); const pdfResponse = page.waitForResponse(response => response.url().includes('/label.pdf'));
  await page.getByRole('button', { name: 'Tải nhãn vận chuyển', exact: true }).click();
  expect((await pdf).suggestedFilename()).toBe('label-ML-BROWSER-001.pdf'); expect((await pdfResponse).headers()['content-type']).toContain('application/pdf');
  await page.route('**/shop/files/**', route => route.fulfill({ status: 429, contentType: 'application/problem+json', body: JSON.stringify({ status: 429, title: 'Limited' }), headers: { 'Retry-After': '60' } }));
  await page.getByRole('button', { name: 'Tải nhãn vận chuyển', exact: true }).click(); await expect(page.getByRole('alert')).toContainText('Đã vượt giới hạn tải file');
});
test('limited staff cannot export or download a PII label', async ({ page }) => {
  await login(page, 'browser-staff@example.test'); await page.goto('./shipments'); await expect(page.getByRole('button', { name: 'Xuất CSV vận đơn', exact: true })).toHaveCount(0);
  await page.getByRole('link', { name: 'ML-BROWSER-001', exact: true }).click(); await expect(page.getByRole('button', { name: 'Tải nhãn vận chuyển', exact: true })).toHaveCount(0);
});
