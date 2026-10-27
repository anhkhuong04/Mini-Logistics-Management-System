import { test, expect } from '@playwright/test';

test('support is accessible by keyboard and points to the contact page', async ({ page }) => {
  await page.goto('./');
  await expect(
    page.getByRole('heading', { name: 'LAK | Nền tảng quản lý logistics', exact: true }),
  ).toBeVisible();
  const contact = page.getByRole('navigation').getByRole('link', { name: 'Liên hệ', exact: true });
  await expect(contact).toHaveAttribute('href', /\/(?:app\/)?contact$/);
  await contact.focus();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { name: 'Thông tin liên hệ', exact: true })).toBeFocused();
  const phone = page.getByRole('main').getByRole('link', { name: '1900 6885', exact: true });
  await expect(phone).toHaveAttribute('href', 'tel:19006885');
  await phone.focus();
  await expect(phone).toBeFocused();
});
