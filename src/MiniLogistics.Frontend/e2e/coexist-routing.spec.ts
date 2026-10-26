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
  expect(faq.status()).toBe(new URL(baseURL!).pathname === '/app/' ? 404 : 200);
  expect(await faq.text()).not.toContain('blazor.web.js');
});

test('health OpenAPI and public navigation keep separate route ownership', async ({ request, baseURL }) => {
  const origin = new URL(baseURL!).origin;
  const health = await request.get(`${origin}/health/live`, { headers: { Accept: '*/*' } });
  expect(health.status()).toBe(200);
  expect(health.headers()['content-type']).not.toContain('text/html');
  expect(await health.json()).toMatchObject({ status: 'Healthy' });
  const openapi = await request.get(`${origin}/openapi/v1.json`);
  expect(openapi.status()).toBe(200);
  expect(openapi.headers()['content-type']).toContain('application/json');
  expect((await openapi.json()).openapi).toMatch(/^3\./);
  for (const path of ['/health/missing', '/openapi/missing']) {
    const response = await request.get(`${origin}${path}`, { headers: { Accept: '*/*' } });
    expect(response.status()).toBe(404);
    expect(response.headers()['content-type'] ?? '').not.toContain('text/html');
  }
  for (const path of ['/tracking?code=', '/Error', '/not-found']) {
    const response = await request.get(new URL(path.replace(/^\//, ''), baseURL).href, { headers: { Accept: 'text/html' } });
    expect(response.status()).toBe(200);
    expect(await response.text()).not.toContain('blazor.web.js');
    expect(await response.text()).toContain('<div id="root">');
  }
});
