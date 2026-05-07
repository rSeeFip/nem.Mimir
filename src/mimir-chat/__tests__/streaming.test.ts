import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createSseClient, ReconnectState } from '@/lib/streaming';

function makeStreamResponse(chunks: string[]): Response {
  const encoder = new TextEncoder();
  const stream = new ReadableStream({
    async start(controller) {
      for (const chunk of chunks) {
        controller.enqueue(encoder.encode(chunk));
        await new Promise((r) => setTimeout(r, 0));
      }
      controller.close();
    },
  });
  return new Response(stream, {
    status: 200,
    headers: { 'Content-Type': 'text/event-stream' },
  });
}

describe('createSseClient', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('parses SSE messages and calls onMessage', async () => {
    const messages: string[] = [];
    const fetchMock = vi.fn().mockResolvedValue(
      makeStreamResponse(['id: 1\ndata: hello\n\n', 'id: 2\ndata: world\n\n']),
    );
    vi.stubGlobal('fetch', fetchMock);

    const client = createSseClient('http://test/stream', {
      onMessage: (data) => messages.push(data),
    });

    client.connect();
    await new Promise((r) => setTimeout(r, 50));

    expect(messages).toEqual(['hello', 'world']);
  });

  it('sends Last-Event-ID header on reconnect', async () => {
    let callCount = 0;
    const fetchMock = vi.fn().mockImplementation(() => {
      callCount++;
      if (callCount === 1) {
        return makeStreamResponse(['id: 5\ndata: first\n\n']);
      }
      return makeStreamResponse(['id: 6\ndata: resumed\n\n']);
    });
    vi.stubGlobal('fetch', fetchMock);

    const messages: string[] = [];
    const client = createSseClient('http://test/stream', {
      onMessage: (data) => messages.push(data),
    });

    client.connect();
    await new Promise((r) => setTimeout(r, 50));

    client.connect();
    await new Promise((r) => setTimeout(r, 50));

    const secondCall = fetchMock.mock.calls[1];
    const headers = secondCall[1]?.headers as Record<string, string>;
    expect(headers['Last-Event-ID']).toBe('5');
  });

  it('transitions to failed state after max retries', async () => {
    vi.useFakeTimers();
    const states: ReconnectState[] = [];
    const fetchMock = vi.fn().mockRejectedValue(new Error('network error'));
    vi.stubGlobal('fetch', fetchMock);

    const client = createSseClient('http://test/stream', {
      onMessage: () => {},
      onStateChange: (s) => states.push(s),
    });

    client.connect();

    for (let i = 0; i < 4; i++) {
      await vi.runAllTimersAsync();
    }

    expect(states).toContain('failed');
    vi.useRealTimers();
  });

  it('disconnect aborts the connection', async () => {
    const abortSpy = vi.fn();
    const mockController = { abort: abortSpy, signal: { aborted: false } };
    vi.stubGlobal('AbortController', vi.fn(() => mockController));

    const fetchMock = vi.fn().mockReturnValue(new Promise(() => {}));
    vi.stubGlobal('fetch', fetchMock);

    const client = createSseClient('http://test/stream', { onMessage: () => {} });
    client.connect();
    client.disconnect();

    expect(abortSpy).toHaveBeenCalled();
  });
});
