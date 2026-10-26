import { test, expect } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';

const screenshots = resolve('../../.agent/refactorfe-phase1/screenshots');
mkdirSync(screenshots, { recursive: true });

for (const path of ['login', 'register-shop']) {
  test(`authentication page ${path} fits a 390px viewport`, async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`./${path}`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    if (path === 'register-shop') await expect(page.getByRole('combobox', { name: 'Tỉnh / Thành phố' })).toBeEnabled();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    await page.screenshot({ path: resolve(screenshots, `auth-${path}-390.png`), fullPage: true });
  });
}

for (const width of [390, 768, 1024, 1440]) {
  test(`shell fits ${width}px and exposes navigation appropriate to the viewport`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    await page.goto('./missing');
    await expect(page.getByRole('heading', { level: 1 })).toBeFocused();
    await expect(page.getByRole('main')).toHaveCount(1);
    const trigger = page.getByRole('button', { name: 'Mở menu' });
    if (width < 992) await expect(trigger).toBeVisible();
    else {
      await expect(trigger).toBeHidden();
      await expect(page.getByRole('menuitem', { name: 'Trang chính' })).toBeVisible();
    }
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    await page.screenshot({ path: resolve(screenshots, `shell-${width}.png`), fullPage: true });
  });
}

test('mobile drawer traps keyboard focus, closes with Escape and restores focus', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('./missing');
  const trigger = page.getByRole('button', { name: 'Mở menu' });
  await trigger.focus();
  await page.keyboard.press('Enter');
  const dialog = page.getByRole('dialog', { name: 'Điều hướng' });
  await expect(dialog).toBeVisible();
  for (let index = 0; index < 8; index++) {
    await page.keyboard.press('Tab');
    // Include the drawer layer's own focus target as well as its dialog panel.
    const focus = await page.evaluate(() => ({ inDrawer: Boolean(document.activeElement?.closest('.ant-drawer')), element: document.activeElement?.outerHTML }));
    expect(focus, `Tab ${index}`).toMatchObject({ inDrawer: true });
  }
  for (let index = 0; index < 8; index++) {
    await page.keyboard.press('Shift+Tab');
    expect(await page.evaluate(() => Boolean(document.activeElement?.closest('.ant-drawer')))).toBe(true);
  }
  await page.screenshot({ path: resolve(screenshots, 'drawer-390.png'), fullPage: true });
  await page.keyboard.press('Escape');
  await expect(dialog).toBeHidden();
  await expect(trigger).toBeFocused();
  await trigger.press('Enter');
  await page.getByRole('button', { name: 'Đóng menu' }).click();
  await expect(dialog).toBeHidden();
  await expect(trigger).toBeFocused();
});

test('skip link and route navigation move focus to the intended content', async ({ page }) => {
  await page.goto('./missing?status=ready');
  await expect(page).toHaveTitle('Không tìm thấy trang · Mini Logistics');
  await expect(page.getByRole('heading', { level: 1 })).toBeFocused();
  const skip = page.getByRole('link', { name: 'Đến nội dung chính' });
  await skip.focus();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('main')).toBeFocused();
  await page.getByRole('link', { name: 'Về trang chính' }).click();
  await expect(page).toHaveTitle(/LAK.*Mini Logistics/);
  await expect(page.getByRole('heading', { level: 1 })).toBeFocused();
  await page.goBack();
  await expect(page.getByRole('main')).toBeFocused(); // Restores the skip-link fragment.
  await page.goto('./missing?status=ready');
  await page.getByRole('link', { name: 'Về trang chính' }).click();
  await expect(page.getByRole('heading', { level: 1 })).toBeFocused();
  await page.goBack();
  await expect(page.getByRole('heading', { level: 1, name: 'Không tìm thấy trang' })).toBeFocused();
});

test('choosing a mobile drawer link preserves focus on the destination after the drawer closes', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('./missing');
  await page.getByRole('button', { name: 'Mở menu' }).click();
  const dialog = page.getByRole('dialog', { name: 'Điều hướng' });
  await dialog.getByRole('link', { name: 'Trang chính', exact: true }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByRole('heading', { level: 1 })).toBeFocused();
});

test('culture cookie switches copy, survives reload and preserves route and query', async ({ page, context }) => {
  await page.goto('./missing?status=ready');
  await page.getByRole('link', { name: 'English', exact: true }).click();
  await expect(page).toHaveURL(/^https:\/\/[^/]+\/(?:app\/)?missing\?status=ready$/);
  await expect(page.getByRole('heading', { level: 1, name: 'Page not found' })).toBeVisible();
  await expect(page.locator('html')).toHaveAttribute('lang', 'en');
  const culture = (await context.cookies()).find(cookie => cookie.name === '.AspNetCore.Culture');
  expect(culture?.httpOnly).toBe(true);
  expect(culture?.secure).toBe(true);
  await page.reload();
  await expect(page.getByRole('heading', { level: 1, name: 'Page not found' })).toBeVisible();
  await page.getByRole('link', { name: 'Tiếng Việt', exact: true }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Không tìm thấy trang' })).toBeVisible();
});

test('layout remains usable at 200 percent CSS zoom', async ({ page }) => {
  await page.setViewportSize({ width: 1024, height: 900 });
  await page.goto('./missing');
  await page.evaluate(() => { document.documentElement.style.zoom = '2'; });
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  // CSS zoom leaves media-query width unchanged; also exercise the mobile breakpoint.
  await page.setViewportSize({ width: 768, height: 900 });
  await page.getByRole('button', { name: 'Mở menu', exact: true }).click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).toBeHidden();
  await expect(page.getByRole('button', { name: 'Mở menu', exact: true })).toBeFocused();
  await page.screenshot({ path: resolve(screenshots, 'shell-zoom200.png'), fullPage: true });
});
