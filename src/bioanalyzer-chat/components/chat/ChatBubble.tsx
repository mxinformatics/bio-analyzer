import { Card } from '@/components/ui/card';
import React from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';

interface ChatBubbleProps {
  role: string;
  text: string;
  className?: string;
  width?: string;
}
const citationHrefRegex = /^#citation-source-\d+$/;

function linkifySourceCitations(markdown: string): string {
  return markdown.replace(/\[Source\s+(\d+)\]/g, '[Source $1](#citation-source-$1)');
}

type CitationMetadata = {
  document?: string;
  page?: string;
  chunk?: string;
  score?: string;
  sourceSystemDocId?: string;
};

function extractCitationMetadata(rawText: string): Record<string, CitationMetadata> {
  const metadataBySource: Record<string, CitationMetadata> = {};
  const lines = rawText.split(/\r?\n/);

  for (const line of lines) {
    const sourceMatch = line.match(/\[Source\s+(\d+)\]/i);
    if (!sourceMatch) {
      continue;
    }

    const sourceNumber = sourceMatch[1];
    if (!line.includes('Document=')) {
      continue;
    }

    const field = (name: string) => line.match(new RegExp(`${name}=([^;]+)`))?.[1]?.trim();
    metadataBySource[sourceNumber] = {
      document: field('Document'),
      page: field('Page'),
      chunk: field('Chunk'),
      score: field('Score'),
      sourceSystemDocId: field('SourceSystemDocId'),
    };
  }

  return metadataBySource;
}

const ChatBubble: React.FC<ChatBubbleProps> = ({ role, text, className = '', width = 'w-fit max-w-md' }) => {
  const assistantMarkdown = linkifySourceCitations(text);
  const citationMetadataBySource = React.useMemo(() => extractCitationMetadata(text), [text]);
  const [openCitation, setOpenCitation] = React.useState<string | null>(null);
  const markdownContainerRef = React.useRef<HTMLDivElement | null>(null);
  const citationTriggerRefs = React.useRef<Record<string, HTMLButtonElement | null>>({});
  const citationPopoverRefs = React.useRef<Record<string, HTMLSpanElement | null>>({});
  const previousOpenCitationRef = React.useRef<string | null>(null);

  React.useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (!markdownContainerRef.current?.contains(event.target as Node)) {
        setOpenCitation(null);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  React.useEffect(() => {
    if (!openCitation) {
      return;
    }

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        setOpenCitation(null);
      }
    };

    document.addEventListener('keydown', handleEscape);
    return () => document.removeEventListener('keydown', handleEscape);
  }, [openCitation]);

  React.useEffect(() => {
    const previousOpenCitation = previousOpenCitationRef.current;

    if (openCitation) {
      requestAnimationFrame(() => {
        citationPopoverRefs.current[openCitation]?.focus();
      });
    } else if (previousOpenCitation) {
      citationTriggerRefs.current[previousOpenCitation]?.focus();
    }

    previousOpenCitationRef.current = openCitation;
  }, [openCitation]);
  return (
    <Card className={`p-5 flex flex-col gap-3 text-wrap break- border-none whitespace-pre-wrap ${width} ${className}`}>
      <h5 className="text-lg font-semibold">{role === 'assistant' ? `✴️ BioAnalyzer` : `👤 ${role}`}</h5>
      {role === 'assistant' ? (
        <div className="text-sm leading-7" ref={markdownContainerRef}>
          <ReactMarkdown
            remarkPlugins={[remarkGfm]}
            components={{
              p: ({ children }) => <p className="mb-3 last:mb-0">{children}</p>,
              ul: ({ children }) => <ul className="mb-2 list-disc pl-5">{children}</ul>,
              li: ({ children }) => <li className="mb-1">{children}</li>,
              a: ({ href, children }) => {
                const isCitation = typeof href === 'string' && citationHrefRegex.test(href);
                const sourceNumber = typeof href === 'string'
                  ? href.match(/citation-source-(\d+)/)?.[1]
                  : undefined;
                const citationKey = sourceNumber ? `source-${sourceNumber}` : href ?? null;
                const isPopoverOpen = citationKey !== null && openCitation === citationKey;

                if (isCitation) {
                  const metadata = sourceNumber ? citationMetadataBySource[sourceNumber] : undefined;
                  const popoverId = citationKey ? `${citationKey}-popover` : undefined;
                  return (
                    <span className="relative inline-flex">
                      <button
                        type="button"
                        ref={(element) => {
                          if (citationKey) {
                            citationTriggerRefs.current[citationKey] = element;
                          }
                        }}
                        onClick={(event) => {
                          event.preventDefault();
                          event.stopPropagation();
                          if (citationKey === null) {
                            return;
                          }
                          setOpenCitation((current) => current === citationKey ? null : citationKey);
                        }}
                        aria-haspopup="dialog"
                        aria-expanded={isPopoverOpen}
                        aria-controls={popoverId}
                        className="inline-flex items-center rounded-full border border-blue-300 bg-blue-50 px-2 py-0.5 text-xs font-medium text-blue-700 hover:bg-blue-100"
                      >
                        {children}
                      </button>
                      {isPopoverOpen ? (
                        <span
                          id={popoverId}
                          ref={(element) => {
                            if (citationKey) {
                              citationPopoverRefs.current[citationKey] = element;
                            }
                          }}
                          role="dialog"
                          tabIndex={-1}
                          aria-label={sourceNumber ? `Citation Source ${sourceNumber}` : 'Citation details'}
                          className="absolute left-0 top-full z-20 mt-2 w-72 rounded-md border border-slate-200 bg-white p-3 text-xs leading-5 text-slate-700 shadow-lg"
                        >
                          <span className="mb-1 block text-[11px] font-semibold uppercase tracking-wide text-slate-500">
                            {sourceNumber ? `Source ${sourceNumber}` : 'Citation'}
                          </span>
                          {metadata ? (
                            <span className="block space-y-0.5">
                              {metadata.document ? <span className="block"><strong>Document:</strong> {metadata.document}</span> : null}
                              {metadata.page ? <span className="block"><strong>Page:</strong> {metadata.page}</span> : null}
                              {metadata.chunk ? <span className="block"><strong>Chunk:</strong> {metadata.chunk}</span> : null}
                              {metadata.score ? <span className="block"><strong>Score:</strong> {metadata.score}</span> : null}
                              {metadata.sourceSystemDocId ? <span className="block"><strong>Doc ID:</strong> {metadata.sourceSystemDocId}</span> : null}
                            </span>
                          ) : (
                            <span className="block">
                              This citation references retrieved evidence supporting the statement. Source metadata was not included in this message.
                            </span>
                          )}
                        </span>
                      ) : null}
                    </span>
                  );
                }

                return (
                  <a
                    href={href}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-blue-600 underline break-all"
                  >
                    {children}
                  </a>
                );
              },
            }}
          >
            {assistantMarkdown}
          </ReactMarkdown>
        </div>
      ) : (
        <div>{text}</div>
      )}
    </Card>
  );
};

export default ChatBubble;
