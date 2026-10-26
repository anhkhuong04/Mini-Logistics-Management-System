import { expect, test } from '@playwright/test';
test.skip(Boolean(process.env.MINILOGISTICS_E2E_BASE_URL) && process.env.MINILOGISTICS_E2E_ROOT !== '1', 'Requires the published root-mode host.');
test('root route ownership, old app redirects and public lazy loading preserve backend boundaries', async ({ page }) => {
  const requests: string[] = []; page.on('request', request => requests.push(request.url()));
  await page.goto('./'); await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  expect(requests.some(url => /_blazor|blazor\.web|AdministrationRoutes-|IntegrationClientsPage-/.test(url))).toBe(false);
  await page.goto('/app/faq?from=old'); await expect(page).toHaveURL(/\/faq\?from=old$/);
  for (const path of ['/api/not-real', '/auth/not-real', '/images/not-real', '/data/not-real', '/frontend-assets/missing.js', '/_blazor', '/_framework/blazor.web.js']) {
    const response = await page.request.get(path); expect(response.status()).toBe(404); expect(await response.text()).not.toContain('<div id="root">');
  }
  expect((await page.request.get('/health/live')).ok()).toBe(true);
});

for (const path of ['/faq', '/contact', '/tracking', '/login', '/register-shop', '/Error', '/admin/dashboard', '/operations/assignment', '/shipments/create?shopId=11111111-1111-1111-1111-111111111111']) {
  test(`root deep link and refresh preserve ${path.split('?')[0]}`, async ({ page }) => {
    await page.goto(path);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await page.reload();
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  });
}
