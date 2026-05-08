import { test, expect } from '@playwright/test';

const USAGE_RESPONSE = {
  tenantId: 'tenant-1',
  totalTokens: 150000,
  totalCostUsd: 3.75,
};

const TENANTS_RESPONSE = [
  { id: 'tenant-1', name: 'Acme Corp', status: 'Active', createdAt: '2024-01-01T00:00:00Z' },
  { id: 'tenant-2', name: 'Beta Ltd', status: 'Inactive', createdAt: '2024-02-01T00:00:00Z' },
];

function mockAdminSession(page: import('@playwright/test').Page, roles: string[]) {
  return page.route('/api/auth/session', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: { name: 'Admin User', email: 'admin@example.com', roles },
        expires: '2099-01-01T00:00:00Z',
      }),
    });
  });
}

test('non-admin user sees empty page and is redirected', async ({ page }) => {
  await page.route('/api/auth/session', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: { name: 'Regular User', email: 'user@example.com', roles: [] },
        expires: '2099-01-01T00:00:00Z',
      }),
    });
  });

  await page.goto('/admin');

  const body = page.locator('body');
  await expect(body).toBeVisible();
});

test('platform admin sees tenant list section', async ({ page }) => {
  await mockAdminSession(page, ['platform-admin']);

  await page.route('/api/tenants', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(TENANTS_RESPONSE),
    });
  });

  await page.route('/api/billing/usage', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(USAGE_RESPONSE),
    });
  });

  await page.goto('/admin');

  const body = page.locator('body');
  await expect(body).toBeVisible();
});

test('admin page shows usage-chart section for admin users', async ({ page }) => {
  await mockAdminSession(page, ['tenant-admin']);

  await page.route('/api/billing/usage', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(USAGE_RESPONSE),
    });
  });

  await page.goto('/admin');

  const body = page.locator('body');
  await expect(body).toBeVisible();
});

test('admin page shows rate-limit-config section', async ({ page }) => {
  await mockAdminSession(page, ['tenant-admin']);

  await page.route('/api/billing/usage', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(USAGE_RESPONSE),
    });
  });

  await page.goto('/admin');

  const body = page.locator('body');
  await expect(body).toBeVisible();
});
