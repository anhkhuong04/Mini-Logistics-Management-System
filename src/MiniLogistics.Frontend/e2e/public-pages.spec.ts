import { test, expect } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';

for (const width of [390, 768, 1024, 1440]) {
  test(`public pages fit ${width}px and keep support links functional`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    for (const path of ['', 'faq', 'contact']) {
      await page.goto(`./${path}`);
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
      await expect(page.getByRole('heading', { level: 1 })).toBeFocused();
      await expect(page.getByRole('main')).toHaveCount(1);
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
      if (width === 390 || width === 1440) {
        const screenshots = resolve('../../.agent/refactorfe-phase11/screenshots');
        mkdirSync(screenshots, { recursive: true });
        await page.screenshot({
          path: resolve(screenshots, `public-${path || 'home'}-${width}.png`),
          fullPage: true,
        });
      }
    }
    await expect(page.getByRole('main').getByRole('link', { name: '1900 6885' })).toHaveAttribute(
      'href',
      'tel:19006885',
    );
    await expect(page.getByRole('main').locator('form')).toHaveCount(0);
  });
}
test('public pages remain usable with banner failure, keyboard and 200 percent zoom', async ({ page }) => {
  await page.route('**/api/v1/public/banners', (route) =>
    route.fulfill({ status: 503, contentType: 'application/problem+json', body: '{}' }),
  );
  await page.setViewportSize({ width: 1024, height: 900 });
  await page.goto('./');
  await page.evaluate(() => {
    document.documentElement.style.zoom = '2';
  });
  const input = page.getByRole('textbox', { name: 'Mã vận đơn' });
  await input.focus();
  await input.fill('LAK404');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/tracking\?code=LAK404$/);
  await expect(page.getByRole('heading', { level: 1 })).toBeFocused();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});
