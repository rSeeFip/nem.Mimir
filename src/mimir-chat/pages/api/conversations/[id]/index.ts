/**
 * BFF proxy: /api/conversations/[id]
 *
 * GET    → Mimir.Api GET /api/conversations/{id} (get with messages)
 * PUT    → Mimir.Api PUT /api/conversations/{id}/title (update title)
 * DELETE → Mimir.Api DELETE /api/conversations/{id} (delete)
 */
import { OPENAI_API_HOST } from '@/utils/app/const';

import { getToken } from 'next-auth/jwt';
import type { NextApiRequest, NextApiResponse } from 'next';

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

    const upstreamBase = `${OPENAI_API_HOST}/api/conversations/${id}`;
    const authHeaders = {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token.accessToken}`,
    };

    if (req.method === 'GET') {
      const response = await fetch(upstreamBase, { headers: authHeaders });

      if (!response.ok) {
        const text = await response.text();
        console.error(`Mimir API error ${response.status}: ${text}`);
        return res.status(response.status).json({ error: text });
      }

      const data = await response.json();
      return res.status(200).json(data);
    }

    if (req.method === 'PUT') {
      // Proxies title update: PUT /api/conversations/{id} → PUT /api/conversations/{id}/title
      const response = await fetch(`${upstreamBase}/title`, {
        method: 'PUT',
        headers: authHeaders,
        body: JSON.stringify(req.body),
      });

      if (!response.ok) {
        const text = await response.text();
        console.error(`Mimir API error ${response.status}: ${text}`);
        return res.status(response.status).json({ error: text });
      }

      return res.status(204).end();
    }

    if (req.method === 'DELETE') {
      const response = await fetch(upstreamBase, {
        method: 'DELETE',
        headers: authHeaders,
      });

      if (!response.ok) {
        const text = await response.text();
        console.error(`Mimir API error ${response.status}: ${text}`);
        return res.status(response.status).json({ error: text });
      }

      return res.status(204).end();
    }

    return res.status(405).json({ error: 'Method not allowed' });
  } catch (error) {
    console.error('BFF /api/conversations/[id] error:', error);
    return res.status(500).json({ error: 'Internal server error' });
  }
};

export default handler;
