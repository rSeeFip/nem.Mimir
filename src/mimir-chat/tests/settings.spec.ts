import { test, expect } from '@playwright/test';

test('settings page loads with heading', async ({ page }) => {
  await page.goto('/settings');

  const body = page.locator('body');
  await expect(body).toBeVisible();

  await expect(page.getByText('Settings')).toBeVisible();
});

test('model dropdown is present and has options', async ({ page }) => {
  await page.goto('/settings');

  const modelSelect = page.getByTestId('model-select');
  await expect(modelSelect).toBeVisible();

  const options = modelSelect.locator('option');
  await expect(options).toHaveCount(7);
});

test('theme toggle button is present', async ({ page }) => {
  await page.goto('/settings');

  const themeToggle = page.getByTestId('theme-toggle');
  await expect(themeToggle).toBeVisible();
});

test('save button is present and triggers saved indicator', async ({ page }) => {
  await page.goto('/settings');

  const saveButton = page.getByTestId('save-button');
  await expect(saveButton).toBeVisible();

  await saveButton.click();

  const savedIndicator = page.getByTestId('saved-indicator');
  await expect(savedIndicator).toBeVisible();
});

test('validation error shown when model is cleared and saved', async ({ page }) => {
  await page.goto('/settings');

  const modelSelect = page.getByTestId('model-select');
  await modelSelect.selectOption('');

  const saveButton = page.getByTestId('save-button');
  await saveButton.click();

  const validationError = page.getByTestId('validation-error');
  await expect(validationError).toBeVisible();
});
