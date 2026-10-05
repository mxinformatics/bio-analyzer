import {
  createUIMessageStream,
  createUIMessageStreamResponse,
  type UIMessage,
} from 'ai';

function resolveResearchApiBaseUrl(): string {
  const baseUrl =
    process.env.services__researchApi__http__0 ||
    process.env.services__researchApi__https__0 ||
    process.env.RESEARCH_API_BASE_URL ||
    '';

  if (!baseUrl) {
    throw new Error(
      'Research API base URL not configured. Please set either services__researchApi__http__0 or RESEARCH_API_BASE_URL.',
    );
  }

  return baseUrl.replace(/\/$/, '');
}

function parseSseDataPayload(rawPayload: string): string {
  const payload = rawPayload.trim();
  if (!payload || payload === '[DONE]') {
    return '';
  }

  try {
    const parsed = JSON.parse(payload);
    if (typeof parsed === 'string') {
      return parsed;
    }
    return String(parsed);
  } catch {
    return payload;
  }
}

/**
 * Legacy Phase-0 behavior: proxy Research API GET /Chat/stream into a UI message stream.
 */
export async function proxyResearchApiChatStream(options: {
  query: string;
  messages: UIMessage[];
  signal?: AbortSignal;
}): Promise<Response> {
  const { query, messages, signal } = options;
  const researchApiBaseUrl = resolveResearchApiBaseUrl();
  const upstreamUrl = `${researchApiBaseUrl}/Chat/stream?query=${encodeURIComponent(query)}`;
  const headers: HeadersInit = {
    Accept: 'text/event-stream',
  };
  const apiKey = (process.env.RESEARCH_API_KEY || process.env.ApiAuthentication__ApiKey || '').trim();
  if (apiKey) {
    headers['X-Api-Key'] = apiKey;
  }

  const upstreamResponse = await fetch(upstreamUrl, {
    method: 'GET',
    headers,
    signal,
  });

  if (!upstreamResponse.ok || !upstreamResponse.body) {
    const errorBody = await upstreamResponse.text();
    throw new Error(
      `Research API stream request failed: ${upstreamResponse.status} ${upstreamResponse.statusText}. ${errorBody}`,
    );
  }

  const stream = createUIMessageStream({
    originalMessages: messages,
    execute: async ({ writer }) => {
      const textId = crypto.randomUUID();
      writer.write({ type: 'text-start', id: textId });

      const reader = upstreamResponse.body!.getReader();
      const decoder = new TextDecoder();
      let buffer = '';
      let eventName = 'message';
      let dataLines: string[] = [];

      const flushEvent = () => {
        if (dataLines.length === 0) {
          eventName = 'message';
          return false;
        }

        const dataPayload = dataLines.join('\n');
        dataLines = [];

        const isDoneEvent = eventName === 'done' || dataPayload.trim() === '[DONE]';
        eventName = 'message';

        if (isDoneEvent) {
          return true;
        }

        const delta = parseSseDataPayload(dataPayload);
        if (delta) {
          writer.write({ type: 'text-delta', id: textId, delta });
        }

        return false;
      };

      try {
        while (true) {
          const { done, value } = await reader.read();
          if (done) {
            break;
          }

          buffer += decoder.decode(value, { stream: true });

          while (true) {
            const newlineIndex = buffer.indexOf('\n');
            if (newlineIndex < 0) {
              break;
            }

            const line = buffer.slice(0, newlineIndex).replace(/\r$/, '');
            buffer = buffer.slice(newlineIndex + 1);

            if (!line) {
              if (flushEvent()) {
                return;
              }
              continue;
            }

            if (line.startsWith('event:')) {
              eventName = line.slice('event:'.length).trim() || 'message';
              continue;
            }

            if (line.startsWith('data:')) {
              dataLines.push(line.slice('data:'.length).trimStart());
            }
          }
        }

        if (buffer.trim()) {
          dataLines.push(buffer.trim());
        }
        flushEvent();
      } finally {
        writer.write({ type: 'text-end', id: textId });
      }
    },
    onError: error => {
      console.error('Error adapting /Chat/stream response:', error);
      return 'Failed to stream response from Research API.';
    },
  });

  return createUIMessageStreamResponse({ stream });
}

export function extractLatestUserText(messages: UIMessage[]): string {
  for (let i = messages.length - 1; i >= 0; i--) {
    const message = messages[i];
    if (message.role !== 'user') {
      continue;
    }

    const text = message.parts
      .filter(part => part.type === 'text')
      .map(part => ('text' in part ? part.text : ''))
      .join('')
      .trim();

    if (text) {
      return text;
    }
  }

  return '';
}

export function isAgenticChatEnabled(): boolean {
  const value = (process.env.AGENTIC_CHAT_ENABLED || '').trim().toLowerCase();
  return value === '1' || value === 'true' || value === 'yes' || value === 'on';
}
