import { expect, test } from '@playwright/test';

test('refreshing a frontend deep link loads React assets and runtime paths', async ({ page }) => {
  await page.goto('./dashboard');
  await page.reload();
  await expect(page.locator('#root')).not.toBeEmpty();
  expect(await page.evaluate(() => Object.keys(window.__MINI_LOGISTICS_CONFIG__ ?? {}).sort()))
    .toEqual(['assetBasePath', 'basePath']);
  await expect(page.locator('script[src*="blazor.web.js"]')).toHaveCount(0);
});

test('backend and asset routes retain their own responses', async ({ request, baseURL }) => {
  const origin = new URL(baseURL!).origin;
  for (const path of ['/api/v1/app/missing', '/auth/missing', '/shop/files/missing']) {
    const response = await request.get(`${origin}${path}`);
    expect(response.status()).toBe(404);
    expect(response.headers()['content-type'] ?? '').not.toContain('text/html');
  }
  const asset = await request.get(`${origin}/frontend-assets/missing.js`);
  expect(asset.status()).toBe(404);
  expect(await asset.text()).not.toContain('<div id="root">');
  const faq = await request.get(`${origin}/faq`);
  expect(faq.status()).toBe(200);
  expect(await faq.text()).toContain('blazor.web.js');
});
