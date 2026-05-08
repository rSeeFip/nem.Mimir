import { useEffect, useRef, useState } from 'react';
import { createSseClient } from '../../lib/streaming';

interface StreamingMessageProps {
  url: string;
  payload: object;
}

export default function StreamingMessage({ url, payload }: StreamingMessageProps) {
  const [text, setText] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const clientRef = useRef<ReturnType<typeof createSseClient> | null>(null);

  useEffect(() => {
    setText('');
    setLoading(true);
    setError(null);

    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };

    const urlWithPayload = url;
    void payload;

    const client = createSseClient(urlWithPayload, {
      headers,
      onMessage: (data) => {
        setLoading(false);
        setText((prev) => prev + data);
      },
      onError: (err) => {
        setLoading(false);
        setError(err.message);
      },
      onStateChange: (state) => {
        if (state === 'failed') {
          setLoading(false);
          setError('Connection failed after maximum retries.');
        }
      },
    });

    clientRef.current = client;
    client.connect();

    return () => {
      client.disconnect();
    };
  }, [url, payload]);

  if (loading && !text) {
    return (
      <div className="flex items-center gap-2 text-gray-400 text-sm" data-testid="streaming-loading">
        <span className="animate-pulse">●</span>
        <span>Streaming...</span>
      </div>
    );
  }

  if (error) {
    return (
      <div className="text-red-400 text-sm" data-testid="streaming-error">
        {error}
      </div>
    );
  }

  return (
    <div className="whitespace-pre-wrap text-sm" data-testid="streaming-message">
      {text}
    </div>
  );
}
