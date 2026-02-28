import { DEFAULT_SYSTEM_PROMPT, DEFAULT_TEMPERATURE } from '@/utils/app/const';
import { OpenAIError, OpenAIStream } from '@/utils/server';

import { ChatBody, Message } from '@/types/chat';

import { getToken } from 'next-auth/jwt';
import type { NextApiRequest, NextApiResponse } from 'next';

import llamaTokenizer from 'llama-tokenizer-js';

const handler = async (req: NextApiRequest, res: NextApiResponse) => {
  try {
    const token = await getToken({ req });
    if (!token?.accessToken) {
      return res.status(401).json({ error: 'Unauthorized' });
    }

    const { model, messages, prompt, temperature } = req.body as ChatBody;

    let promptToSend = prompt;
    if (!promptToSend) {
      promptToSend = DEFAULT_SYSTEM_PROMPT;
    }

    let temperatureToUse = temperature;
    if (temperatureToUse == null) {
      temperatureToUse = DEFAULT_TEMPERATURE;
    }

    const prompt_tokens = llamaTokenizer.encode(promptToSend, false);

    let tokenCount = prompt_tokens.length;
    let messagesToSend: Message[] = [];

    for (let i = messages.length - 1; i >= 0; i--) {
      const message = messages[i];
      const tokens = llamaTokenizer.encode(message.content, false);

      if (tokenCount + tokens.length + 768 > model.tokenLimit) {
        break;
      }
      tokenCount += tokens.length;
      messagesToSend = [message, ...messagesToSend];
    }

    const stream = await OpenAIStream(
      model,
      promptToSend,
      temperatureToUse,
      token.accessToken as string,
      messagesToSend,
    );

    // Stream the response to the client
    res.setHeader('Content-Type', 'text/event-stream');
    res.setHeader('Cache-Control', 'no-cache');
    res.setHeader('Connection', 'keep-alive');

    const reader = stream.getReader();
    const decoder = new TextDecoder();

    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        const text = decoder.decode(value, { stream: true });
        res.write(text);
      }
    } finally {
      reader.releaseLock();
    }

    res.end();
  } catch (error) {
    console.error(error);
    if (error instanceof OpenAIError) {
      return res.status(500).json({ error: error.message });
    } else {
      return res.status(500).json({ error: 'Error' });
    }
  }
};

export default handler;
