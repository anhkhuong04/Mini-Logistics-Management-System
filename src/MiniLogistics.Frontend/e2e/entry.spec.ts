import { test, expect } from '@playwright/test';

test('support is accessible by keyboard and points to the contact page', async ({ page }) => {
  await page.goto('./');
  await expect(page.getByRole('heading', { name: 'Mini Logistics', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Hỗ trợ' }).click();
  await expect(page.getByRole('dialog')).toContainText('Liên hệ hỗ trợ');
  await expect(page.getByRole('link', { name: 'Trang liên hệ' })).toHaveAttribute('href', '/contact');
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).toBeHidden();
});
