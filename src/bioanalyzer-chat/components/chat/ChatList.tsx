import ChatBubble from './ChatBubble';
import ChatBubbleLoading from './ChatBubbleLoading';
import React from 'react';
import { formatAssistantResponse } from '@/lib/formatAssistantResponse';

interface MessagePart {
  type: string;
  text?: string;
  toolName?: string;
  state?: string;
  output?: any;
  result?: any;
  [key: string]: any;
}

interface Message {
  id?: string | number;
  role: string;
  content?: string | MessagePart[];
  text?: string;
  parts?: MessagePart[];
}

interface ChatListProps {
  messages: Message[];
  isLoading: boolean;
}

function humanizeToolName(toolName: string | undefined): string {
  switch (toolName) {
    case 'askGraph':
      return 'Searching research graph';
    case 'findLiteratureCandidates':
      return 'Finding open-access candidates';
    case 'buildInterimAbstractBriefing':
      return 'Building interim abstract briefing';
    case 'requestLiteratureIngest':
      return 'Enqueueing literature ingest';
    case 'getIngestJobStatus':
      return 'Checking ingest job status';
    case 'waitForIngestAndAskGraph':
      return 'Waiting for ingest and re-querying graph';
    case 'searchLiterature':
      return 'Searching literature';
    case 'getLiteratureSummary':
      return 'Fetching literature summaries';
    case 'getLiteratureAbstract':
      return 'Fetching abstract';
    case 'getDownloadLink':
      return 'Resolving download link';
    case 'downloadFile':
      return 'Downloading stored file';
    default:
      return toolName ? `Running ${toolName}` : 'Running tool';
  }
}

function resolveToolName(part: MessagePart): string | undefined {
  const type = part.type || '';
  return (
    part.toolName ||
    (type.startsWith('tool-') && type !== 'tool-invocation' ? type.slice('tool-'.length) : undefined)
  );
}

function isToolPart(part: MessagePart): boolean {
  const type = part.type || '';
  return type === 'tool-invocation' || type === 'dynamic-tool' || type.startsWith('tool-');
}

function isToolDone(part: MessagePart): boolean {
  const state = String(part.state || part.toolInvocation?.state || '').toLowerCase();
  return (
    state.includes('result') ||
    state.includes('output') ||
    state === 'complete' ||
    state === 'completed'
  );
}

function collectActiveToolLabels(messages: Message[]): string[] {
  const labels: string[] = [];
  const seen = new Set<string>();

  for (const message of messages) {
    if (message.role !== 'assistant' || !message.parts) {
      continue;
    }

    for (const part of message.parts) {
      if (!isToolPart(part) || isToolDone(part)) {
        continue;
      }

      const label = humanizeToolName(resolveToolName(part));
      if (!seen.has(label)) {
        seen.add(label);
        labels.push(label);
      }
    }
  }

  return labels;
}

function collectCompletedToolSummaries(messages: Message[]): string[] {
  const lines: string[] = [];
  const seen = new Set<string>();

  for (const message of messages) {
    if (message.role !== 'assistant' || !message.parts) {
      continue;
    }

    for (const part of message.parts) {
      if (!isToolPart(part) || !isToolDone(part)) {
        continue;
      }

      const toolName = resolveToolName(part);
      const output = part.output ?? part.result ?? part.toolInvocation?.result;
      if (!output || typeof output !== 'object') {
        continue;
      }

      if (toolName === 'requestLiteratureIngest') {
        const data = (output as any).data ?? output;
        const enqueued = data?.enqueued ?? data?.Enqueued ?? [];
        const jobId = data?.jobId ?? data?.JobId;
        if (Array.isArray(enqueued) && enqueued.length > 0) {
          const titles = enqueued
            .map((item: any) => item?.title || item?.Title || item?.pmcId || item?.PmcId)
            .filter(Boolean)
            .slice(0, 5);
          const line = `Queued ingest${jobId ? ` (${jobId})` : ''}: ${titles.join('; ')}`;
          if (!seen.has(line)) {
            seen.add(line);
            lines.push(line);
          }
        }
      }

      if (toolName === 'findLiteratureCandidates') {
        const data = (output as any).data ?? output;
        const candidates = data?.candidates ?? data?.Candidates ?? [];
        if (Array.isArray(candidates) && candidates.length > 0) {
          const titles = candidates
            .map((item: any) => item?.title || item?.Title || item?.pmcId || item?.PmcId)
            .filter(Boolean)
            .slice(0, 5);
          const line = `OA candidates: ${titles.join('; ')}`;
          if (!seen.has(line)) {
            seen.add(line);
            lines.push(line);
          }
        }
      }
    }
  }

  return lines;
}

const ChatList: React.FC<ChatListProps> = ({ messages, isLoading }) => {
  const [toolTraceOpen, setToolTraceOpen] = React.useState(false);

  const displayMessages = messages.filter((message) => {
    if (message.role === 'user') return true;

    if (message.role === 'assistant') {
      const hasText = message.parts
        ? message.parts.some((part) => part.type === 'text' && part.text?.trim())
        : message.text?.trim();
      return Boolean(hasText);
    }

    return false;
  });

  const activeToolLabels = collectActiveToolLabels(messages);
  const completedSummaries = collectCompletedToolSummaries(messages);

  return (
    <ul className="flex flex-col gap-5">
      {displayMessages.map((message) => {
        const rawText = message.parts
          ? message.parts
              .filter((part) => part.type === 'text')
              .map((part) => part.text)
              .join('')
          : message.text || '';
        const normalizedText =
          message.role === 'assistant'
            ? formatAssistantResponse(rawText)
            : rawText;

        return (
          <li key={message?.id}>
            <ChatBubble
              role={message.role}
              text={normalizedText}
              className={`${message.role === 'assistant' ? 'mr-auto' : 'ml-auto'} border-none`}
            />
          </li>
        );
      })}

      {(activeToolLabels.length > 0 || completedSummaries.length > 0) ? (
        <li key="tool-activity">
          <div className="mr-auto w-full max-w-md rounded-md border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-600">
            {activeToolLabels.length > 0 ? (
              <div className="mb-2 space-y-1">
                {activeToolLabels.map((label) => (
                  <div key={label} className="flex items-center gap-2 py-0.5">
                    <span className="inline-block h-1.5 w-1.5 animate-pulse rounded-full bg-blue-500" />
                    <span>{label}…</span>
                  </div>
                ))}
              </div>
            ) : null}

            {completedSummaries.length > 0 ? (
              <div>
                <button
                  type="button"
                  className="mb-1 font-medium text-slate-700 underline-offset-2 hover:underline"
                  aria-expanded={toolTraceOpen}
                  onClick={() => setToolTraceOpen(open => !open)}
                >
                  {toolTraceOpen ? 'Hide tool details' : 'Show tool details'}
                </button>
                {toolTraceOpen ? (
                  <ul className="list-disc space-y-1 pl-4 text-slate-600">
                    {completedSummaries.map(line => (
                      <li key={line}>{line}</li>
                    ))}
                    <li>
                      <a
                        className="text-blue-700 underline"
                        href="/downloads"
                        onClick={event => {
                          // Soft-link placeholder for apps that host downloads elsewhere.
                          // Keep default navigation if a downloads route exists.
                          if (typeof window !== 'undefined' && !document.querySelector('a[href="/downloads"]')) {
                            // no-op; still useful as a discoverable affordance in docs/UI copy
                          }
                        }}
                      >
                        View literature downloads
                      </a>
                    </li>
                  </ul>
                ) : (
                  <div className="text-slate-500">
                    {completedSummaries[0]}
                    {completedSummaries.length > 1 ? ` (+${completedSummaries.length - 1} more)` : ''}
                  </div>
                )}
              </div>
            ) : null}
          </div>
        </li>
      ) : null}

      {isLoading && activeToolLabels.length === 0 ? (
        <li key="loading">
          <ChatBubbleLoading />
        </li>
      ) : null}
    </ul>
  );
};

export default React.memo(ChatList);
