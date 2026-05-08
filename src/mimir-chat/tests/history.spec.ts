import { test, expect } from '@playwright/test';

const CONVERSATIONS_RESPONSE = {
  items: [
    { id: '1', title: 'First conversation', updatedAt: '2024-01-01T00:00:00Z' },
    { id: '2', title: 'Second conversation', updatedAt: '2024-01-02T00:00:00Z' },
  ],
  page: 1,
  pageSize: 20,
  totalCount: 2,
  hasNextPage: false,
};

test.beforeEach(async ({ page }) => {
  await page.route('/api/conversations*', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(CONVERSATIONS_RESPONSE),
    });
  });
});

test('history page loads and shows heading', async ({ page }) => {
  await page.goto('/history');

  const body = page.locator('body');
  await expect(body).toBeVisible();

  await expect(page.getByText('Chat History')).toBeVisible();
});

test('search input is present and accepts text', async ({ page }) => {
  await page.goto('/history');

  const searchInput = page.getByTestId('search-input');
  await expect(searchInput).toBeVisible();

  await searchInput.fill('test query');
  await expect(searchInput).toHaveValue('test query');
});

test('load more button is visible when hasNextPage is true', async ({ page }) => {
  await page.route('/api/conversations*', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ ...CONVERSATIONS_RESPONSE, hasNextPage: true }),
    });
  });

  await page.goto('/history');

  const loadMore = page.getByTestId('load-more');
  await expect(loadMore).toBeVisible();
});

test('load more button is hidden when hasNextPage is false', async ({ page }) => {
  await page.goto('/history');

  const loadMore = page.getByTestId('load-more');
  await expect(loadMore).not.toBeVisible();
});
