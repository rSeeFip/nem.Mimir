import { OPENAI_API_HOST } from '@/utils/app/const';

import { getToken } from 'next-auth/jwt';
import type { NextApiRequest, NextApiResponse } from 'next';

const handler = async (req: NextApiRequest, res: NextApiResponse) => {
  try {
    const token = await getToken({ req });
    if (!token?.accessToken) {
      return res.status(401).json({ error: 'Unauthorized' });
    }

    const upstreamBase = `${OPENAI_API_HOST}/api/billing/usage`;

    if (req.method === 'GET') {
      const { from, to } = req.query;
      const params = new URLSearchParams();
      if (from) params.set('from', String(from));
      if (to) params.set('to', String(to));
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

    return res.status(405).json({ error: 'Method not allowed' });
  } catch (error) {
    console.error('BFF /api/billing/usage error:', error);
    return res.status(500).json({ error: 'Internal server error' });
  }
};

export default handler;
