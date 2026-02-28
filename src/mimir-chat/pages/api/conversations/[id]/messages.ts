/**
 * BFF proxy: /api/conversations/[id]/messages
 *
 * POST → Accepts a batch of { messages: Message[] } from the client
 *        and forwards each new message individually to
 *        Mimir.Api POST /api/conversations/{id}/messages
 *
 * GET  → Mimir.Api GET /api/conversations/{id}/messages (paginated)
 *
 * The Mimir.Api SendMessage endpoint expects { content, model? } and
 * returns the assistant response.  For persistence of user+assistant
 * message pairs that were already streamed via /api/chat, we only
 * need to store them.  We forward each message to the API which will
 * persist it.
 */
import { OPENAI_API_HOST } from '@/utils/app/const';

import { getToken } from 'next-auth/jwt';
import type { NextApiRequest, NextApiResponse } from 'next';

interface ClientMessage {
  role: 'user' | 'assistant';
  content: string;
}

const handler = async (req: NextApiRequest, res: NextApiResponse) => {
  try {
    const token = await getToken({ req });
    if (!token?.accessToken) {
      return res.status(401).json({ error: 'Unauthorized' });
    }

    const { id } = req.query;
    if (!id || Array.isArray(id)) {
      return res.status(400).json({ error: 'Invalid conversation id' });
    }

    const upstreamBase = `${OPENAI_API_HOST}/api/conversations/${id}/messages`;
    const authHeaders = {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token.accessToken}`,
    };

    if (req.method === 'GET') {
      const { pageNumber, pageSize } = req.query;
      const params = new URLSearchParams();
      if (pageNumber) params.set('pageNumber', String(pageNumber));
      if (pageSize) params.set('pageSize', String(pageSize));
      const qs = params.toString();

      const response = await fetch(`${upstreamBase}${qs ? `?${qs}` : ''}`, {
        headers: authHeaders,
      });

      if (!response.ok) {
        const text = await response.text();
        console.error(`Mimir API error ${response.status}: ${text}`);
        return res.status(response.status).json({ error: text });
      }

      const data = await response.json();
      return res.status(200).json(data);
    }

    if (req.method === 'POST') {
      const { messages } = req.body as { messages: ClientMessage[] };

      if (!messages || !Array.isArray(messages)) {
        return res.status(400).json({ error: 'messages array required' });
      }

      // Forward each message to Mimir.Api sequentially.
      // In practice this is typically 1-2 messages (user + assistant)
      // after a chat exchange is complete.
      for (const msg of messages) {
        const response = await fetch(upstreamBase, {
          method: 'POST',
          headers: authHeaders,
          body: JSON.stringify({
            content: msg.content,
            model: null,
          }),
        });

        if (!response.ok) {
          const text = await response.text();
          console.error(
            `Mimir API error saving message ${response.status}: ${text}`,
          );
          // Don't fail the whole batch — log and continue
        }
      }

      return res.status(201).json({ ok: true });
    }

    return res.status(405).json({ error: 'Method not allowed' });
  } catch (error) {
    console.error('BFF /api/conversations/[id]/messages error:', error);
    return res.status(500).json({ error: 'Internal server error' });
  }
};

export default handler;
