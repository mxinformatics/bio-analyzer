import { normalizeChatQueryResult, type ChatQueryResult } from './chatQuery';

export type ResearchApiOptions = {
  /** Verified session subject when available; falls back to AGENT_REQUESTER_ID. */
  requesterId?: string;
  /** Entra access token for Research.Api (preferred over API key). */
  accessToken?: string;
};

export class ResearchApi {
  private baseUrl: string;
  private requesterIdOverride?: string;
  private accessToken?: string;

  constructor(options?: ResearchApiOptions) {
    // Try to get the base URL from environment variables
    this.baseUrl =
      process.env.services__researchApi__http__0 ||
      process.env.services__researchApi__https__0 ||
      process.env.RESEARCH_API_BASE_URL ||
      '';

    if (!this.baseUrl) {
      throw new Error('Research API base URL not configured. Please set either services__researchApi__http__0 or RESEARCH_API_BASE_URL environment variable.');
    }

    // Remove trailing slash if present
    this.baseUrl = this.baseUrl.replace(/\/$/, '');
    this.requesterIdOverride = options?.requesterId?.trim() || undefined;
    this.accessToken = options?.accessToken?.trim() || undefined;
  }

  private resolveRequesterId(): string {
    return (this.requesterIdOverride || process.env.AGENT_REQUESTER_ID || '').trim();
  }


  /**
   * Phase D2: stable per-turn idempotency key from query + sorted PMCIDs (not a random UUID).
   */
  private async buildIngestIdempotencyKey(queryPreview: string | undefined, items: Array<{ pmcId: string }>): Promise<string> {
    const pmcIds = items
      .map(i => (i.pmcId || '').trim().toUpperCase())
      .filter(Boolean)
      .sort();
    const material = `${(queryPreview || '').trim()}|${pmcIds.join(',')}`;
    const data = new TextEncoder().encode(material);
    const digest = await crypto.subtle.digest('SHA-256', data);
    return Array.from(new Uint8Array(digest))
      .map(b => b.toString(16).padStart(2, '0'))
      .join('');
  }

  private resolveApiKey(): string {
    return (process.env.RESEARCH_API_KEY || process.env.ApiAuthentication__ApiKey || '').trim();
  }

  private buildAgentHeaders(extra?: HeadersInit): Headers {
    const headers = new Headers(extra || {});
    if (!headers.has('Accept')) {
      headers.set('Accept', 'application/json');
    }
    // Phase C1: do NOT send X-Requester-Id / body.requestedBy.
    // Research.Api binds requester from Entra JWT (oid/appid) or API-key principal only.
    // Prefer Entra bearer token; fall back to shared API key for local Aspire.
    if (this.accessToken) {
      headers.set('Authorization', `Bearer ${this.accessToken}`);
    } else {
      const apiKey = this.resolveApiKey();
      if (apiKey) {
        headers.set('X-Api-Key', apiKey);
      }
    }
    return headers;
  }


  /**
   * Make a GET request to the Research API
   * @param url - The full URL to request
   * @returns JSON response from the API
   */
  private async getResponse(url: string): Promise<any> {
    try {
      console.log(`Getting from Research API URL: ${url}`);
      const response = await fetch(url, {
        method: 'GET',
        headers: this.buildAgentHeaders({ 'Content-Type': 'application/json' }),
      });

      if (!response.ok) {
        throw new Error(`Research API request failed: ${response.status} ${response.statusText}`);
      }

      console.log(`Research API response status: ${response.status}`);
      const data = await response.json();

      return data;
    } catch (error) {
      console.error('Error calling Research API:', error);
      throw error;
    }
  }

  /**
   * Search literature using a query string
   * @param query - The search query
   * @param startIndex - The starting index for pagination
   * @returns JSON response from the API
   */
  async searchLiterature(query: string, startIndex: number = 0): Promise<any> {
    const url = `${this.baseUrl}/Literature?query=${encodeURIComponent(query)}&startIndex=${startIndex}`;
    console.log('Research API searchLiterature called with query:', query, 'startIndex:', startIndex);
    return this.getResponse(url);
  }

  /**
   * Get summaries for a list of literature IDs
   * @param ids - Array of literature IDs
   * @returns JSON response from the API
   */
  async getLiteratureSummary(ids: string[]): Promise<any> {
    const queryString = ids.map(id => `ids=${encodeURIComponent(id)}`).join('&');
    const url = `${this.baseUrl}/Literature/summary?${queryString}`;
    console.log('Research API getLiteratureSummary called with ids:', ids);
    return this.getResponse(url);
  }

  /**
   * Get abstract for a specific PMC ID
   * @param pmcId - The PubMed Central ID
   * @returns JSON response from the API
   */
  async getLiteratureAbstract(pmcId: string): Promise<any> {
    const url = `${this.baseUrl}/Literature/abstract?pmcId=${encodeURIComponent(pmcId)}`;
    console.log('Research API getLiteratureAbstract called with pmcId:', pmcId);
    return this.getResponse(url);
  }

  /**
   * Get download link for a literature reference
   * @param pmcId - The PubMed Central ID
   * @returns JSON response from the API
   */
  async getDownloadLink(pmcId: string): Promise<any> {
    const url = `${this.baseUrl}/Literature/download?pmcId=${encodeURIComponent(pmcId)}`;
    console.log('Research API getDownloadLink called with pmcId:', pmcId);
    return this.getResponse(url);
  }

  /**
   * View all downloaded literature files
   * @returns JSON response from the API
   */
  async viewDownloads(): Promise<any> {
    const url = `${this.baseUrl}/Literature/downloads/view`;
    console.log('Research API viewDownloads called');
    return this.getResponse(url);
  }

  /**
   * Download a specific file by name
   * @param fileName - The name of the file to download
   * @returns File blob from the API
   */
  async downloadFile(fileName: string): Promise<Blob> {
    try {
      const url = `${this.baseUrl}/Literature/downloads/${encodeURIComponent(fileName)}`;
      console.log(`Downloading file from Research API URL: ${url}`);
      
      const response = await fetch(url, {
        method: 'GET',
        headers: this.buildAgentHeaders(),
      });

      if (!response.ok) {
        throw new Error(`Research API download failed: ${response.status} ${response.statusText}`);
      }

      console.log(`Research API download response status: ${response.status}`);
      const blob = await response.blob();

      return blob;
    } catch (error) {
      console.error('Error downloading file from Research API:', error);
      throw error;
    }
  }

  /**
   * Structured graph-RAG query (Phase 0). Prefer this for agent tools over scraping /Chat/stream text.
   * Uses POST /Chat/query; falls back to GET /Chat/evidence if POST is unavailable.
   */
  async queryGraph(query: string): Promise<ChatQueryResult> {
    if (!query || !query.trim()) {
      throw new Error('Query cannot be null or empty.');
    }

    const trimmed = query.trim();
    console.log('Research API queryGraph called with query:', trimmed);

    try {
      const postUrl = `${this.baseUrl}/Chat/query`;
      const postResponse = await fetch(postUrl, {
        method: 'POST',
        headers: this.buildAgentHeaders({ 'Content-Type': 'application/json' }),
        body: JSON.stringify({ query: trimmed }),
      });

      if (postResponse.ok) {
        const data = await postResponse.json();
        return normalizeChatQueryResult(data as Record<string, unknown>);
      }

      if (postResponse.status !== 404 && postResponse.status !== 405) {
        const body = await postResponse.text();
        throw new Error(
          `Research API queryGraph failed: ${postResponse.status} ${postResponse.statusText}. ${body}`,
        );
      }
    } catch (error) {
      if (!(error instanceof TypeError)) {
        // network TypeError may still allow GET fallback; rethrow API errors
        const message = error instanceof Error ? error.message : String(error);
        if (message.startsWith('Research API queryGraph failed:')) {
          throw error;
        }
      }
      console.warn('POST /Chat/query failed; trying GET /Chat/evidence', error);
    }

    const getUrl = `${this.baseUrl}/Chat/evidence?query=${encodeURIComponent(trimmed)}`;
    const data = await this.getResponse(getUrl);
    return normalizeChatQueryResult(data as Record<string, unknown>);
  }


  /**
   * Phase 2: ranked OA-ingestible literature candidates (search + summary + PMC OA links).
   */
  async findLiteratureCandidates(
    query: string,
    options?: {
      maxCandidates?: number;
      requirePmcId?: boolean;
      requireOpenAccessLink?: boolean;
    },
  ): Promise<any> {
    if (!query || !query.trim()) {
      throw new Error('Query cannot be null or empty.');
    }

    const url = `${this.baseUrl}/Literature/candidates`;
    console.log('Research API findLiteratureCandidates called with query:', query);
    const response = await fetch(url, {
      method: 'POST',
      headers: this.buildAgentHeaders({ 'Content-Type': 'application/json' }),
      body: JSON.stringify({
        query: query.trim(),
        maxCandidates: options?.maxCandidates ?? 0,
        requirePmcId: options?.requirePmcId,
        requireOpenAccessLink: options?.requireOpenAccessLink,
      }),
    });

    if (!response.ok) {
      const body = await response.text();
      throw new Error(
        `Research API findLiteratureCandidates failed: ${response.status} ${response.statusText}. ${body}`,
      );
    }

    return response.json();
  }


  /**
   * Phase 3: enqueue OA literature for download + document processing.
   */
  async requestLiteratureIngest(body: {
    items: Array<{
      pmcId: string;
      title?: string;
      doi?: string;
      downloadLink?: string;
      xmlLink?: string;
    }>;
    correlationId?: string;
    requestedBy?: string;
    queryPreview?: string;
    idempotencyKey?: string;
  }): Promise<any> {
    const url = `${this.baseUrl}/Literature/ingest`;
    console.log('Research API requestLiteratureIngest called with item count:', body.items?.length ?? 0);
    const headers = this.buildAgentHeaders({ 'Content-Type': 'application/json' });
    const idempotencyKey =
      body.idempotencyKey?.trim() ||
      (await this.buildIngestIdempotencyKey(body.queryPreview, body.items || []));
    headers.set('Idempotency-Key', idempotencyKey);
    const response = await fetch(url, {
      method: 'POST',
      headers,
      body: JSON.stringify({
        items: body.items,
        correlationId: body.correlationId,
        queryPreview: body.queryPreview,
        // Never send client-controlled requestedBy; server derives identity from auth.
        idempotencyKey,
      }),
    });
    if (!response.ok) {
      const errBody = await response.text();
      throw new Error(
        `Research API requestLiteratureIngest failed: ${response.status} ${response.statusText}. ${errBody}`,
      );
    }
    return response.json();
  }

  /**
   * Phase 3: poll agentic literature ingest job status.
   */
  async getIngestJobStatus(jobId: string): Promise<any> {
    if (!jobId || !jobId.trim()) {
      throw new Error('jobId is required.');
    }
    const url = `${this.baseUrl}/Literature/ingest/${encodeURIComponent(jobId.trim())}`;
    console.log('Research API getIngestJobStatus called for jobId:', jobId);
    return this.getResponse(url);
  }


  /**
   * Phase 5: batch abstract fetch for interim answers while ingest runs.
   */
  async getLiteratureAbstractsBatch(pmcIds: string[]): Promise<any> {
    if (!pmcIds?.length) {
      throw new Error('At least one pmcId is required.');
    }
    const url = `${this.baseUrl}/Literature/abstracts/batch`;
    console.log('Research API getLiteratureAbstractsBatch called for', pmcIds.length, 'ids');
    const response = await fetch(url, {
      method: 'POST',
      headers: this.buildAgentHeaders({ 'Content-Type': 'application/json' }),
      body: JSON.stringify({ pmcIds }),
    });
    if (!response.ok) {
      const body = await response.text();
      throw new Error(
        `Research API getLiteratureAbstractsBatch failed: ${response.status} ${response.statusText}. ${body}`,
      );
    }
    return response.json();
  }

  /** Alias used by agent tools. */
  async askGraph(query: string): Promise<ChatQueryResult> {
    return this.queryGraph(query);
  }

}
