/**
 * BFF proxy: /api/conversations
 *
 * GET  → Mimir.Api GET /api/conversations (list)
 * POST → Mimir.Api POST /api/conversations (create)
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

    const upstreamBase = `${OPENAI_API_HOST}/api/conversations`;

    if (req.method === 'GET') {
      const { pageNumber, pageSize } = req.query;
      const params = new URLSearchParams();
      if (pageNumber) params.set('pageNumber', String(pageNumber));
      if (pageSize) params.set('pageSize', String(pageSize));
      const qs = params.toString();

      const response = await fetch(`${upstreamBase}${qs ? `?${qs}` : ''}`, {
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token.accessToken}`,
        },
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
      const response = await fetch(upstreamBase, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token.accessToken}`,
        },
        body: JSON.stringify(req.body),
      });

      if (!response.ok) {
        const text = await response.text();
        console.error(`Mimir API error ${response.status}: ${text}`);
        return res.status(response.status).json({ error: text });
      }

      const data = await response.json();
      return res.status(201).json(data);
    }

    return res.status(405).json({ error: 'Method not allowed' });
  } catch (error) {
    console.error('BFF /api/conversations error:', error);
    return res.status(500).json({ error: 'Internal server error' });
  }
};

export default handler;
