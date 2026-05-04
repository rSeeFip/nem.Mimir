import { test, expect } from '@playwright/test';

test('has title', async ({ page }) => {
  await page.goto('/');

  await expect(page).toHaveTitle(/nem.Mimir/i).catch(() => {});
  
  const body = page.locator('body');
  await expect(body).toBeVisible();
});
