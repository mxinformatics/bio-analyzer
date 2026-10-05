import { NextRequest, NextResponse } from 'next/server';
import {
  convertToModelMessages,
  stepCountIs,
  streamText,
  type UIMessage,
} from 'ai';
import type { Session } from 'next-auth';
import { AGENT_SYSTEM_PROMPT } from '@/lib/agentPrompt';
import { createFoundryChatModel, getAgentGenerationSettings } from '@/lib/foundryModel';
import {
  extractLatestUserText,
  isAgenticChatEnabled,
  proxyResearchApiChatStream,
} from '@/lib/legacyChatStream';
import { requireApiSession } from '@/lib/requireApiSession';
import { ResearchApi } from '@/lib/ResearchApi';
import { getResearchTools } from '@/lib/ResearchTools';

type ChatRequestBody = {
  messages?: UIMessage[];
};

function resolveRequesterFromSession(session: Session): string | undefined {
  const user = session.user as Session['user'] & { id?: string; oid?: string; sub?: string };
  const candidate =
    user?.id ||
    user?.oid ||
    user?.email ||
    user?.name ||
    process.env.AGENT_REQUESTER_ID ||
    '';
  const trimmed = candidate.trim();
  return trimmed || undefined;
}

async function handleAgenticChat(options: {
  messages: UIMessage[];
  signal?: AbortSignal;
  session: Session;
}): Promise<Response> {
  const { messages, signal, session } = options;
  const researchApi = new ResearchApi({
    requesterId: resolveRequesterFromSession(session),
    accessToken: (session as { accessToken?: string }).accessToken,
  });
  const tools = getResearchTools(researchApi);
  const model = createFoundryChatModel();
  const { maxOutputTokens, temperature } = getAgentGenerationSettings();
  const modelMessages = await convertToModelMessages(messages);

  const result = streamText({
    model,
    system: AGENT_SYSTEM_PROMPT,
    messages: modelMessages,
    tools,
    toolChoice: 'auto',
    stopWhen: stepCountIs(12),
    temperature,
    maxOutputTokens,
    abortSignal: signal,
    onError: ({ error }) => {
      console.error('Agentic chat streamText error:', error);
    },
  });

  return result.toUIMessageStreamResponse({
    originalMessages: messages,
    onError: error => {
      console.error('Error streaming agentic UI messages:', error);
      return 'Failed to stream agentic chat response.';
    },
  });
}

export async function POST(req: NextRequest) {
  try {
    const auth = await requireApiSession();
    if (!auth.ok) {
      return auth.response;
    }

    const body = (await req.json()) as ChatRequestBody;
    const messages = Array.isArray(body.messages) ? body.messages : [];
    const query = extractLatestUserText(messages);

    if (!query) {
      return NextResponse.json({ error: 'Query cannot be null or empty.' }, { status: 400 });
    }

    if (isAgenticChatEnabled()) {
      console.log('AGENTIC_CHAT_ENABLED: using tool-calling agent path');
      return await handleAgenticChat({
        messages,
        signal: req.signal,
        session: auth.session,
      });
    }

    console.log('AGENTIC_CHAT_ENABLED off: proxying Research API /Chat/stream');
    return await proxyResearchApiChatStream({
      query,
      messages,
      signal: req.signal,
    });
  } catch (error) {
    console.error('Error in /api/chat:', error);
    const message = error instanceof Error ? error.message : 'Internal Server Error';
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
