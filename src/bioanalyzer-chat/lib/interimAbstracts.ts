import type { ResearchApi } from './ResearchApi';

export type InterimAbstractSource = {
  pmcId: string;
  title: string;
  abstract: string;
  doi?: string;
};

export type InterimAbstractBriefing = {
  enabled: boolean;
  success: boolean;
  evidenceKind: 'abstract-only';
  disclaimer: string;
  query: string;
  sources: InterimAbstractSource[];
  failures: Array<{ pmcId: string; errorMessage?: string | null }>;
  /** Pre-formatted markdown the agent should present (or lightly adapt without removing the disclaimer). */
  interimMarkdown: string;
  message: string;
  error?: boolean;
};

const DISCLAIMER =
  '**Interim answer — abstracts only.** This is **not** full-text graph corpus evidence. Full-text ingest may still be in progress; a later graph-backed answer with `[Source n]` citations may replace or supplement this.';

export function isInterimAbstractsEnabled(): boolean {
  const value = (process.env.INTERIM_ABSTRACTS_ENABLED || 'true').trim().toLowerCase();
  // Default ON for Phase 5 QoL; set false/0/off to disable.
  if (value === '0' || value === 'false' || value === 'no' || value === 'off') {
    return false;
  }
  return true;
}

export function getInterimAbstractMaxSources(): number {
  const parsed = Number.parseInt(process.env.INTERIM_ABSTRACTS_MAX || '2', 10);
  if (!Number.isFinite(parsed) || parsed <= 0) {
    return 2;
  }
  return Math.min(parsed, 5);
}

function normalizePmcId(pmcId: string): string {
  const trimmed = pmcId.trim();
  if (!trimmed) {
    return '';
  }
  return trimmed.toUpperCase().startsWith('PMC') ? `PMC${trimmed.slice(3)}` : `PMC${trimmed}`;
}

function truncate(text: string, max: number): string {
  const normalized = text.replace(/\s+/g, ' ').trim();
  if (normalized.length <= max) {
    return normalized;
  }
  return `${normalized.slice(0, max - 1)}…`;
}

function buildInterimMarkdown(query: string, sources: InterimAbstractSource[]): string {
  const lines: string[] = [
    DISCLAIMER,
    '',
    `Question: ${query}`,
    '',
  ];

  if (sources.length === 0) {
    lines.push('_No abstracts could be retrieved for the selected candidates._');
    return lines.join('\n');
  }

  lines.push('Abstract-based notes (not graph citations):');
  sources.forEach((source, index) => {
    const label = `Abstract ${index + 1}`;
    lines.push(`- **${label}** — ${source.title || 'Untitled'} (\`${source.pmcId}\`${source.doi ? `; DOI ${source.doi}` : ''})`);
    lines.push(`  - ${truncate(source.abstract || 'No abstract text available.', 900)}`);
  });

  lines.push('');
  lines.push(
    '_Do not use `[Source n]` markers for this interim content. Those markers are reserved for graph-RAG evidence._',
  );

  return lines.join('\n');
}

/**
 * Fetch abstracts for candidate PMCIDs and build an explicitly labeled interim briefing.
 */
export async function buildInterimAbstractBriefing(options: {
  researchApi: ResearchApi;
  query: string;
  pmcIds: string[];
  /** Optional title/doi hints from findLiteratureCandidates */
  candidateMeta?: Array<{ pmcId?: string; title?: string; doi?: string }>;
}): Promise<InterimAbstractBriefing> {
  const { researchApi, query, pmcIds, candidateMeta } = options;
  const enabled = isInterimAbstractsEnabled();
  if (!enabled) {
    return {
      enabled: false,
      success: true,
      evidenceKind: 'abstract-only',
      disclaimer: DISCLAIMER,
      query,
      sources: [],
      failures: [],
      interimMarkdown: '',
      message: 'Interim abstracts are disabled (INTERIM_ABSTRACTS_ENABLED=false).',
    };
  }

  const max = getInterimAbstractMaxSources();
  const normalizedIds = [...new Set(pmcIds.map(normalizePmcId).filter(Boolean))].slice(0, max);
  if (normalizedIds.length === 0) {
    return {
      enabled: true,
      success: false,
      error: true,
      evidenceKind: 'abstract-only',
      disclaimer: DISCLAIMER,
      query,
      sources: [],
      failures: [],
      interimMarkdown: '',
      message: 'At least one pmcId is required for interim abstracts.',
    };
  }

  const metaByPmc = new Map<string, { title?: string; doi?: string }>();
  for (const meta of candidateMeta || []) {
    if (!meta?.pmcId) continue;
    metaByPmc.set(normalizePmcId(meta.pmcId), { title: meta.title, doi: meta.doi });
  }

  let batch: any;
  try {
    batch = await researchApi.getLiteratureAbstractsBatch(normalizedIds);
  } catch (error) {
    // Fallback: sequential single-abstract calls if batch endpoint unavailable.
    const items: any[] = [];
    const failures: any[] = [];
    for (const pmcId of normalizedIds) {
      try {
        const one = await researchApi.getLiteratureAbstract(pmcId);
        items.push({
          pmcId,
          title: one?.title ?? one?.Title ?? '',
          description: one?.description ?? one?.Description ?? '',
        });
      } catch (inner) {
        failures.push({
          pmcId,
          errorMessage: inner instanceof Error ? inner.message : String(inner),
        });
      }
    }
    batch = { items, failures };
    if (items.length === 0) {
      return {
        enabled: true,
        success: false,
        error: true,
        evidenceKind: 'abstract-only',
        disclaimer: DISCLAIMER,
        query,
        sources: [],
        failures,
        interimMarkdown: '',
        message: `Failed to fetch abstracts: ${error instanceof Error ? error.message : 'Unknown error'}`,
      };
    }
  }

  const rawItems = (batch?.items ?? batch?.Items ?? []) as any[];
  const rawFailures = (batch?.failures ?? batch?.Failures ?? []) as any[];

  const sources: InterimAbstractSource[] = rawItems.map(item => {
    const pmcId = normalizePmcId(String(item.pmcId ?? item.PmcId ?? ''));
    const meta = metaByPmc.get(pmcId);
    return {
      pmcId,
      title: String(item.title ?? item.Title ?? meta?.title ?? ''),
      abstract: String(item.description ?? item.Description ?? item.abstract ?? ''),
      doi: meta?.doi,
    };
  });

  const failures = rawFailures.map(item => ({
    pmcId: normalizePmcId(String(item.pmcId ?? item.PmcId ?? '')),
    errorMessage: item.errorMessage ?? item.ErrorMessage ?? null,
  }));

  const interimMarkdown = buildInterimMarkdown(query, sources);

  return {
    enabled: true,
    success: sources.length > 0,
    evidenceKind: 'abstract-only',
    disclaimer: DISCLAIMER,
    query,
    sources,
    failures,
    interimMarkdown,
    message:
      sources.length > 0
        ? `Built interim abstract briefing from ${sources.length} paper(s). Present interimMarkdown to the user with the disclaimer; do not use [Source n] citations.`
        : 'No abstracts retrieved for the requested PMCIDs.',
    error: sources.length === 0,
  };
}
