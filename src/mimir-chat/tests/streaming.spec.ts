import { test, expect } from '@playwright/test';

const STREAMING_PAGE_HTML = `<!DOCTYPE html>
<html>
<head><title>Streaming Test - nem.Mimir</title></head>
<body>
  <div data-testid="connection-state">idle</div>
  <button data-testid="connect-btn">Connect</button>
  <button data-testid="disconnect-btn" disabled>Disconnect</button>
  <button data-testid="reconnect-btn" style="display:none">Reconnect</button>
  <div data-testid="message-log"></div>
  <script>
    const stateEl = document.querySelector('[data-testid="connection-state"]');
    const connectBtn = document.querySelector('[data-testid="connect-btn"]');
    const disconnectBtn = document.querySelector('[data-testid="disconnect-btn"]');
    const reconnectBtn = document.querySelector('[data-testid="reconnect-btn"]');
    const messageLog = document.querySelector('[data-testid="message-log"]');

    let es = null;

    function setState(state) {
      stateEl.textContent = state;
      stateEl.setAttribute('data-state', state);
    }

    connectBtn.addEventListener('click', () => {
      setState('connecting');
      es = new EventSource('/api/stream');
      es.onopen = () => {
        setState('connected');
        connectBtn.disabled = true;
        disconnectBtn.disabled = false;
        reconnectBtn.style.display = 'none';
      };
      es.onmessage = (e) => {
        const div = document.createElement('div');
        div.setAttribute('data-testid', 'stream-message');
        div.textContent = e.data;
        messageLog.appendChild(div);
      };
      es.onerror = () => {
        setState('failed');
        connectBtn.disabled = false;
        disconnectBtn.disabled = true;
        reconnectBtn.style.display = '';
        es.close();
      };
    });

    disconnectBtn.addEventListener('click', () => {
      if (es) { es.close(); es = null; }
      setState('idle');
      connectBtn.disabled = false;
      disconnectBtn.disabled = true;
      reconnectBtn.style.display = 'none';
    });

    reconnectBtn.addEventListener('click', () => {
      reconnectBtn.style.display = 'none';
      connectBtn.click();
    });
  </script>
</body>
</html>`;

test.beforeEach(async ({ page }) => {
  await page.route('/streaming-test', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'text/html',
      body: STREAMING_PAGE_HTML,
    });
  });
});

test('streaming page shows idle connection state on load', async ({ page }) => {
  await page.goto('/streaming-test');

  const stateEl = page.getByTestId('connection-state');
  await expect(stateEl).toBeVisible();
  await expect(stateEl).toHaveText('idle');
});

test('connect button is visible and disconnect button is disabled initially', async ({ page }) => {
  await page.goto('/streaming-test');

  const connectBtn = page.getByTestId('connect-btn');
  const disconnectBtn = page.getByTestId('disconnect-btn');

  await expect(connectBtn).toBeVisible();
  await expect(connectBtn).toBeEnabled();
  await expect(disconnectBtn).toBeDisabled();
});

test('reconnect button is hidden on initial load', async ({ page }) => {
  await page.goto('/streaming-test');

  const reconnectBtn = page.getByTestId('reconnect-btn');
  await expect(reconnectBtn).not.toBeVisible();
});

test('SSE connection transitions to connected state on successful stream', async ({ page }) => {
  await page.route('/api/stream', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'text/event-stream',
      headers: {
        'Cache-Control': 'no-cache',
        Connection: 'keep-alive',
      },
      body: 'data: hello\n\ndata: world\n\n',
    });
  });

  await page.goto('/streaming-test');

  const connectBtn = page.getByTestId('connect-btn');
  await connectBtn.click();

  const stateEl = page.getByTestId('connection-state');
  await expect(stateEl).toHaveText('connected', { timeout: 5000 });

  const disconnectBtn = page.getByTestId('disconnect-btn');
  await expect(disconnectBtn).toBeEnabled();
});

test('failed SSE connection shows reconnect button', async ({ page }) => {
  await page.route('/api/stream', async (route) => {
    await route.fulfill({
      status: 500,
      contentType: 'text/plain',
      body: 'Internal Server Error',
    });
  });

  await page.goto('/streaming-test');

  const connectBtn = page.getByTestId('connect-btn');
  await connectBtn.click();

  const stateEl = page.getByTestId('connection-state');
  await expect(stateEl).toHaveText('failed', { timeout: 5000 });

  const reconnectBtn = page.getByTestId('reconnect-btn');
  await expect(reconnectBtn).toBeVisible();
});
