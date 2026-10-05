/** Machine-readable chat/graph outcomes from Research API (Phase 0). */
export const ChatQueryStatus = {
  Answered: 'Answered',
  NoResults: 'NoResults',
  InsufficientEvidence: 'InsufficientEvidence',
  GraphUnavailable: 'GraphUnavailable',
} as const;

export type ChatQueryStatusValue =
  (typeof ChatQueryStatus)[keyof typeof ChatQueryStatus];

export type ChatEvidenceSource = {
  index: number;
  documentName: string;
  sourceSystemDocId: string;
  pmcId: string;
  pageNumber: number;
  chunkIndex: number;
  score: number;
  evidence: string;
};

export type ChatRetrievalSummary = {
  totalResults: number;
  selectedResults: number;
  minScore: number;
  avgScore: number;
  maxScore: number;
  threshold: number;
  topK: number;
  minEvidenceCount: number;
  maxEvidenceCount: number;
  retrievalDurationMs: number;
};

export type ChatQueryResult = {
  status: string;
  query: string;
  queryHash: string;
  answer: string;
  retrieval: ChatRetrievalSummary;
  sources: ChatEvidenceSource[];
  retrievalMetricStatus: string;
  errorMessage?: string | null;
};

/** Normalize ASP.NET default camelCase JSON into a stable client shape. */
export function normalizeChatQueryResult(raw: Record<string, unknown>): ChatQueryResult {
  const retrievalRaw = (raw.retrieval ?? raw.Retrieval ?? {}) as Record<string, unknown>;
  const sourcesRaw = (raw.sources ?? raw.Sources ?? []) as unknown[];

  const num = (v: unknown, fallback = 0) =>
    typeof v === 'number' && !Number.isNaN(v) ? v : fallback;

  const str = (v: unknown, fallback = '') =>
    typeof v === 'string' ? v : fallback;

  return {
    status: str(raw.status ?? raw.Status, ChatQueryStatus.InsufficientEvidence),
    query: str(raw.query ?? raw.Query),
    queryHash: str(raw.queryHash ?? raw.QueryHash),
    answer: str(raw.answer ?? raw.Answer),
    retrievalMetricStatus: str(raw.retrievalMetricStatus ?? raw.RetrievalMetricStatus),
    errorMessage:
      raw.errorMessage === null || raw.ErrorMessage === null
        ? null
        : str(raw.errorMessage ?? raw.ErrorMessage, '') || null,
    retrieval: {
      totalResults: num(retrievalRaw.totalResults ?? retrievalRaw.TotalResults),
      selectedResults: num(retrievalRaw.selectedResults ?? retrievalRaw.SelectedResults),
      minScore: num(retrievalRaw.minScore ?? retrievalRaw.MinScore),
      avgScore: num(retrievalRaw.avgScore ?? retrievalRaw.AvgScore),
      maxScore: num(retrievalRaw.maxScore ?? retrievalRaw.MaxScore),
      threshold: num(retrievalRaw.threshold ?? retrievalRaw.Threshold),
      topK: num(retrievalRaw.topK ?? retrievalRaw.TopK),
      minEvidenceCount: num(retrievalRaw.minEvidenceCount ?? retrievalRaw.MinEvidenceCount),
      maxEvidenceCount: num(retrievalRaw.maxEvidenceCount ?? retrievalRaw.MaxEvidenceCount),
      retrievalDurationMs: num(retrievalRaw.retrievalDurationMs ?? retrievalRaw.RetrievalDurationMs),
    },
    sources: sourcesRaw.map((item, i) => {
      const s = (item ?? {}) as Record<string, unknown>;
      return {
        index: num(s.index ?? s.Index, i + 1),
        documentName: str(s.documentName ?? s.DocumentName),
        sourceSystemDocId: str(s.sourceSystemDocId ?? s.SourceSystemDocId),
        pmcId: str(s.pmcId ?? s.PmcId),
        pageNumber: num(s.pageNumber ?? s.PageNumber),
        chunkIndex: num(s.chunkIndex ?? s.ChunkIndex),
        score: num(s.score ?? s.Score),
        evidence: str(s.evidence ?? s.Evidence),
      };
    }),
  };
}
