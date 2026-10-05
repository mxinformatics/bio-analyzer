import type { ChatQueryResult } from './chatQuery';
import { ChatQueryStatus } from './chatQuery';
import {
  computePollDelayMs,
  getIngestWaitSettings,
  isIngestReadyForGraphRequery,
  isTerminalIngestStatus,
  sleep,
  type IngestWaitMode,
} from './ingestWaitPolicy';
import type { ResearchApi } from './ResearchApi';

export type IngestJobSnapshot = {
  jobId?: string;
  status?: string;
  items?: Array<{
    pmcId?: string;
    status?: string;
    stage?: string;
    errorMessage?: string | null;
    documentId?: string | null;
  }>;
  errorMessage?: string | null;
  [key: string]: unknown;
};

export type CloseLoopResult = {
  success: boolean;
  waitMode: IngestWaitMode;
  expansionRound: 1;
  jobId: string;
  jobStatus: string;
  timedOut: boolean;
  readyForGraphRequery: boolean;
  pollsPerformed: number;
  job?: IngestJobSnapshot;
  graph?: ChatQueryResult | null;
  message: string;
  error?: boolean;
};

/**
 * Phase 4 close-the-loop helper:
 * wait (poll or defer) for ingest job readiness, then optionally re-query the graph once.
 * Enforces a single expansion round for the caller.
 */
export async function waitForIngestAndAskGraph(options: {
  researchApi: ResearchApi;
  jobId: string;
  query: string;
  signal?: AbortSignal;
  /**
   * When true, force poll even if env default is defer.
   * When false, force defer. When omitted, use INGEST_WAIT_MODE.
   */
  forceWaitMode?: IngestWaitMode;
}): Promise<CloseLoopResult> {
  const { researchApi, jobId, query, signal, forceWaitMode } = options;
  const settings = getIngestWaitSettings();
  const waitMode = forceWaitMode ?? settings.mode;
  const trimmedJobId = jobId.trim();
  const trimmedQuery = query.trim();

  if (!trimmedJobId) {
    return {
      success: false,
      error: true,
      waitMode,
      expansionRound: 1,
      jobId: '',
      jobStatus: 'Unknown',
      timedOut: false,
      readyForGraphRequery: false,
      pollsPerformed: 0,
      graph: null,
      message: 'jobId is required.',
    };
  }

  if (!trimmedQuery) {
    return {
      success: false,
      error: true,
      waitMode,
      expansionRound: 1,
      jobId: trimmedJobId,
      jobStatus: 'Unknown',
      timedOut: false,
      readyForGraphRequery: false,
      pollsPerformed: 0,
      graph: null,
      message: 'query is required for post-ingest askGraph.',
    };
  }

  let pollsPerformed = 0;
  let latestJob: IngestJobSnapshot | undefined;
  let timedOut = false;

  const maxAttempts = waitMode === 'poll' ? settings.maxAttempts : 1;

  for (let attempt = 0; attempt < maxAttempts; attempt++) {
    if (signal?.aborted) {
      throw new DOMException('Aborted', 'AbortError');
    }

    if (attempt > 0 && waitMode === 'poll') {
      const delay = computePollDelayMs(attempt - 1, settings);
      await sleep(delay, signal);
    }

    latestJob = (await researchApi.getIngestJobStatus(trimmedJobId)) as IngestJobSnapshot;
    pollsPerformed += 1;

    const status = String(latestJob?.status || '');
    if (isTerminalIngestStatus(status)) {
      timedOut = false;
      break;
    }

    if (waitMode === 'defer' || attempt === maxAttempts - 1) {
      timedOut = waitMode === 'poll' && !isTerminalIngestStatus(status);
      break;
    }
  }

  const jobStatus = String(latestJob?.status || 'Unknown');
  const readyForGraphRequery = isIngestReadyForGraphRequery(jobStatus);

  if (!readyForGraphRequery) {
    const pendingMessage =
      waitMode === 'defer'
        ? `Ingest job ${trimmedJobId} is ${jobStatus}. Document processing is asynchronous; ask again in a few minutes, or call waitForIngestAndAskGraph later with the same jobId.`
        : timedOut
          ? `Ingest job ${trimmedJobId} is still ${jobStatus} after ${pollsPerformed} poll(s). Sources may still be processing; try again shortly with the same jobId.`
          : `Ingest job ${trimmedJobId} finished with status ${jobStatus} and is not ready for graph re-query.`;

    return {
      success: true,
      waitMode,
      expansionRound: 1,
      jobId: trimmedJobId,
      jobStatus,
      timedOut,
      readyForGraphRequery: false,
      pollsPerformed,
      job: latestJob,
      graph: null,
      message: pendingMessage,
    };
  }

  const graph = await researchApi.askGraph(trimmedQuery);
  const answered = graph.status === ChatQueryStatus.Answered;

  return {
    success: true,
    waitMode,
    expansionRound: 1,
    jobId: trimmedJobId,
    jobStatus,
    timedOut: false,
    readyForGraphRequery: true,
    pollsPerformed,
    job: latestJob,
    graph,
    message: answered
      ? `Ingest job ${trimmedJobId} reached ${jobStatus}. Re-queried graph successfully (expansion round 1 complete).`
      : `Ingest job ${trimmedJobId} reached ${jobStatus}, but graph re-query returned ${graph.status}. Do not start another expansion round in this turn.`,
  };
}
