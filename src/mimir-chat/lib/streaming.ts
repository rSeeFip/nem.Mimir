export type ReconnectState = 'idle' | 'reconnecting' | 'failed';

export interface SseOptions {
  onMessage: (data: string, eventId: string) => void;
  onError?: (error: Error) => void;
  onReconnect?: (attempt: number) => void;
  onStateChange?: (state: ReconnectState) => void;
  headers?: Record<string, string>;
}

export interface SseClient {
  connect: () => void;
  disconnect: () => void;
  getState: () => ReconnectState;
}

const MAX_RETRIES = 3;
const BASE_BACKOFF_MS = 1000;

export function createSseClient(url: string, options: SseOptions): SseClient {
  let abortController: AbortController | null = null;
  let reconnectState: ReconnectState = 'idle';
  let lastEventId = '';
  let retryCount = 0;
  let disconnected = false;

  function setState(state: ReconnectState) {
    reconnectState = state;
    options.onStateChange?.(state);
  }

  async function connect() {
    if (disconnected) return;

    abortController = new AbortController();

    const headers: Record<string, string> = {
      Accept: 'text/event-stream',
      'Cache-Control': 'no-cache',
      ...options.headers,
    };

    if (lastEventId) {
      headers['Last-Event-ID'] = lastEventId;
    }

    try {
      const response = await fetch(url, {
        headers,
        signal: abortController.signal,
      });

      if (!response.ok) {
        throw new Error(`SSE connection failed: ${response.status}`);
      }

      if (!response.body) {
        throw new Error('Response body is null');
      }

      setState('idle');
      retryCount = 0;

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '';

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        buffer = lines.pop() ?? '';

        let currentId = '';
        let currentData = '';

        for (const line of lines) {
          if (line.startsWith('id: ')) {
            currentId = line.slice(4);
          } else if (line.startsWith('data: ')) {
            currentData += line.slice(6);
          } else if (line === '' && currentData) {
            if (currentId) lastEventId = currentId;
            options.onMessage(currentData, currentId);
            currentId = '';
            currentData = '';
          }
        }
      }
    } catch (err) {
      if (disconnected) return;

      const error = err instanceof Error ? err : new Error(String(err));

      if (error.name === 'AbortError') return;

      options.onError?.(error);

      if (retryCount < MAX_RETRIES) {
        retryCount++;
        setState('reconnecting');
        options.onReconnect?.(retryCount);
        const delay = BASE_BACKOFF_MS * Math.pow(2, retryCount - 1);
        await new Promise((resolve) => setTimeout(resolve, delay));
        connect();
      } else {
        setState('failed');
      }
    }
  }

  function disconnect() {
    disconnected = true;
    abortController?.abort();
    setState('idle');
  }

  function getState(): ReconnectState {
    return reconnectState;
  }

  return { connect, disconnect, getState };
}
