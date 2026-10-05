export type IngestWaitMode = 'poll' | 'defer';

export type IngestWaitSettings = {
  mode: IngestWaitMode;
  maxAttempts: number;
  initialDelayMs: number;
  maxDelayMs: number;
};

const TERMINAL_JOB_STATUSES = new Set([
  'GraphReady',
  'Failed',
  'Partial',
]);

export function getIngestWaitSettings(): IngestWaitSettings {
  const rawMode = (process.env.INGEST_WAIT_MODE || 'defer').trim().toLowerCase();
  const mode: IngestWaitMode = rawMode === 'poll' ? 'poll' : 'defer';

  const maxAttempts = parsePositiveInt(process.env.INGEST_POLL_MAX_ATTEMPTS, 5);
  const initialDelayMs = parsePositiveInt(process.env.INGEST_POLL_INITIAL_DELAY_MS, 5_000);
  const maxDelayMs = parsePositiveInt(process.env.INGEST_POLL_MAX_DELAY_MS, 15_000);

  return {
    mode,
    maxAttempts: Math.min(Math.max(maxAttempts, 1), 12),
    initialDelayMs: Math.min(Math.max(initialDelayMs, 500), 60_000),
    maxDelayMs: Math.min(Math.max(maxDelayMs, 500), 120_000),
  };
}

export function isTerminalIngestStatus(status: string | undefined | null): boolean {
  if (!status) {
    return false;
  }
  return TERMINAL_JOB_STATUSES.has(status);
}

export function isIngestReadyForGraphRequery(status: string | undefined | null): boolean {
  return status === 'GraphReady' || status === 'Partial';
}

export function computePollDelayMs(attemptIndex: number, settings: IngestWaitSettings): number {
  // attemptIndex is 0-based for the delay *before* the next attempt after the first immediate read.
  const exponential = settings.initialDelayMs * Math.pow(2, Math.max(attemptIndex, 0));
  return Math.min(exponential, settings.maxDelayMs);
}

function parsePositiveInt(value: string | undefined, fallback: number): number {
  const parsed = Number.parseInt(value || '', 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

export function sleep(ms: number, signal?: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    if (signal?.aborted) {
      reject(new DOMException('Aborted', 'AbortError'));
      return;
    }

    const timer = setTimeout(() => {
      signal?.removeEventListener('abort', onAbort);
      resolve();
    }, ms);

    const onAbort = () => {
      clearTimeout(timer);
      reject(new DOMException('Aborted', 'AbortError'));
    };

    signal?.addEventListener('abort', onAbort, { once: true });
  });
}
