import { tool } from 'ai';
import { z } from 'zod';
import { ChatQueryStatus } from './chatQuery';
import { waitForIngestAndAskGraph } from './ingestCloseLoop';
import { buildInterimAbstractBriefing, isInterimAbstractsEnabled } from './interimAbstracts';
import { getAgentFeatureFlags } from './agentFeatureFlags';
import { ResearchApi } from './ResearchApi';

export function getResearchTools(researchApi: ResearchApi) {
  const flags = getAgentFeatureFlags();
  const tools = {
    askGraph: tool({
      description:
        'Query the BioAnalyzer Neo4j graph-RAG corpus for evidence-backed answers. ' +
        'ALWAYS call this first for scientific questions. Returns machine-readable status: ' +
        'Answered, NoResults, InsufficientEvidence, or GraphUnavailable, plus answer text and sources.',
      inputSchema: z.object({
        query: z
          .string()
          .min(1)
          .describe('The user scientific question to ground against the graph corpus'),
      }),
      execute: async ({ query }) => {
        try {
          console.log(`askGraph tool query: ${query}`);
          const result = await researchApi.askGraph(query);
          console.log('askGraph status:', result.status, 'sources:', result.sources?.length ?? 0);

          return {
            success: true,
            status: result.status,
            answered: result.status === ChatQueryStatus.Answered,
            answer: result.answer,
            query: result.query,
            queryHash: result.queryHash,
            retrieval: result.retrieval,
            sources: result.sources,
            retrievalMetricStatus: result.retrievalMetricStatus,
            errorMessage: result.errorMessage ?? null,
          };
        } catch (error) {
          console.error('Error in askGraph tool:', error);
          return {
            success: false,
            status: ChatQueryStatus.GraphUnavailable,
            answered: false,
            answer: '',
            error: true,
            message: `Failed to query graph: ${error instanceof Error ? error.message : 'Unknown error'}`,
          };
        }
      },
    }),


    findLiteratureCandidates: tool({
      description:
        'After a graph miss (NoResults/InsufficientEvidence/GraphUnavailable), find open-access PMC candidates ' +
        'that can later be ingested into the BioAnalyzer pipeline. Returns ranked items with pmcId, title, doi, ' +
        'pdfLink/xmlLink, and alreadyIngested. Prefer this over manually chaining searchLiterature + getDownloadLink. ' +
        'After selecting candidates, call requestLiteratureIngest to enqueue pipeline download/processing.',
      inputSchema: z.object({
        query: z.string().min(1).describe('Scientific query used to discover candidate papers'),
        maxCandidates: z
          .number()
          .int()
          .min(1)
          .max(10)
          .optional()
          .describe('Maximum OA candidates to return (server also enforces a configured cap)'),
        requirePmcId: z
          .boolean()
          .optional()
          .describe('When true (default), only papers with a PMCID are returned'),
        requireOpenAccessLink: z
          .boolean()
          .optional()
          .describe('When true (default), only papers with resolvable PMC OA PDF/XML links are returned'),
      }),
      execute: async ({ query, maxCandidates, requirePmcId, requireOpenAccessLink }) => {
        try {
          console.log(`findLiteratureCandidates query=${query} max=${maxCandidates}`);
          const result = await researchApi.findLiteratureCandidates(query, {
            maxCandidates,
            requirePmcId,
            requireOpenAccessLink,
          });
          return {
            success: true,
            data: result,
          };
        } catch (error) {
          console.error('Error in findLiteratureCandidates tool:', error);
          return {
            error: true,
            message: `Failed to find literature candidates: ${error instanceof Error ? error.message : 'Unknown error'}`,
          };
        }
      },
    }),


    requestLiteratureIngest: tool({
      description:
        'Enqueue one or more OA PMC papers into the BioAnalyzer download + chunk/embed/graph pipeline. ' +
        'Use after findLiteratureCandidates. Prefer candidates with pdfLink/xmlLink that are not alreadyIngested. ' +
        'Returns a jobId. Next call waitForIngestAndAskGraph with that jobId and the original question. ' +
        'Do NOT claim the graph corpus is updated until close-loop reports GraphReady/Partial and askGraph succeeds.',
      inputSchema: z.object({
        items: z
          .array(
            z.object({
              pmcId: z.string().min(1).describe('PMCID with or without PMC prefix'),
              title: z.string().optional().describe('Article title'),
              doi: z.string().optional().describe('DOI if known'),
              downloadLink: z.string().optional().describe('Optional OA PDF HTTPS URL from candidates'),
              xmlLink: z.string().optional().describe('Optional OA XML HTTPS URL from candidates'),
            }),
          )
          .min(1)
          .max(5)
          .describe('Papers to enqueue (server also enforces MaxCandidates)'),
        correlationId: z.string().optional().describe('Optional correlation id for tracing'),
        queryPreview: z.string().optional().describe('Original user question preview for the job record'),
      }),
      execute: async ({ items, correlationId, queryPreview }) => {
        try {
          console.log(`requestLiteratureIngest items=${items.length}`);
          const result = await researchApi.requestLiteratureIngest({
            items,
            correlationId,
            queryPreview,
          });
          return { success: true, data: result };
        } catch (error) {
          console.error('Error in requestLiteratureIngest tool:', error);
          return {
            error: true,
            message: `Failed to request literature ingest: ${error instanceof Error ? error.message : 'Unknown error'}`,
          };
        }
      },
    }),

    getIngestJobStatus: tool({
      description:
        'Get status for a literature ingest job returned by requestLiteratureIngest. ' +
        'Statuses include Accepted, Downloading, Processing, GraphReady, Partial, Failed. ' +
        'Prefer waitForIngestAndAskGraph after requestLiteratureIngest to enforce wait policy and a single graph re-query. ' +
        'Statuses include Accepted, Downloading, Processing, GraphReady, Partial, Failed.',
      inputSchema: z.object({
        jobId: z.string().min(1).describe('Ingest job id from requestLiteratureIngest'),
      }),
      execute: async ({ jobId }) => {
        try {
          console.log(`getIngestJobStatus jobId=${jobId}`);
          const result = await researchApi.getIngestJobStatus(jobId);
          return { success: true, data: result };
        } catch (error) {
          console.error('Error in getIngestJobStatus tool:', error);
          return {
            error: true,
            message: `Failed to get ingest job status: ${error instanceof Error ? error.message : 'Unknown error'}`,
          };
        }
      },
    }),


    waitForIngestAndAskGraph: tool({
      description:
        'Phase 4 close-the-loop: after requestLiteratureIngest, wait for the ingest job using INGEST_WAIT_MODE ' +
        '(poll or defer), then re-query the graph ONCE with the original user question when status is GraphReady or Partial. ' +
        'Use this instead of manually polling getIngestJobStatus + askGraph. Enforces a single expansion round — do not start another candidate/ingest cycle in this turn afterward. ' +
        'If still pending, tell the user processing is async and they can retry with the same jobId later.',
      inputSchema: z.object({
        jobId: z.string().min(1).describe('Ingest job id from requestLiteratureIngest'),
        query: z
          .string()
          .min(1)
          .describe('Original user scientific question to re-ask against the graph after ingest'),
        waitMode: z
          .enum(['poll', 'defer'])
          .optional()
          .describe('Optional override of INGEST_WAIT_MODE for this call only'),
      }),
      execute: async ({ jobId, query, waitMode }) => {
        try {
          console.log(`waitForIngestAndAskGraph jobId=${jobId} waitMode=${waitMode ?? 'env-default'}`);
          const result = await waitForIngestAndAskGraph({
            researchApi,
            jobId,
            query,
            forceWaitMode: waitMode,
          });
          return result;
        } catch (error) {
          console.error('Error in waitForIngestAndAskGraph tool:', error);
          return {
            success: false,
            error: true,
            expansionRound: 1,
            jobId,
            message: `Failed close-the-loop: ${error instanceof Error ? error.message : 'Unknown error'}`,
          };
        }
      },
    }),


    buildInterimAbstractBriefing: tool({
      description:
        'Phase 5: after findLiteratureCandidates (and typically alongside or just before requestLiteratureIngest), ' +
        'fetch abstracts for the top 1–2 OA PMCIDs and return an explicitly labeled interim markdown briefing. ' +
        'Use when INTERIM_ABSTRACTS_ENABLED is on (default). Present interimMarkdown to the user with the disclaimer intact. ' +
        'Do NOT use [Source n] citations for this content. When waitForIngestAndAskGraph later returns a graph Answered result, ' +
        'prefer/supplement with that full-text cited answer and clearly mark what was interim vs graph-backed.',
      inputSchema: z.object({
        query: z.string().min(1).describe('Original user scientific question'),
        pmcIds: z
          .array(z.string().min(1))
          .min(1)
          .max(5)
          .describe('PMC IDs from findLiteratureCandidates (top OA picks)'),
        candidateMeta: z
          .array(
            z.object({
              pmcId: z.string().optional(),
              title: z.string().optional(),
              doi: z.string().optional(),
            }),
          )
          .optional()
          .describe('Optional title/DOI hints from candidates'),
      }),
      execute: async ({ query, pmcIds, candidateMeta }) => {
        try {
          if (!isInterimAbstractsEnabled()) {
            return {
              enabled: false,
              success: true,
              evidenceKind: 'abstract-only',
              message: 'Interim abstracts disabled by INTERIM_ABSTRACTS_ENABLED.',
              interimMarkdown: '',
              sources: [],
            };
          }
          console.log(`buildInterimAbstractBriefing pmcIds=${pmcIds.join(',')}`);
          return await buildInterimAbstractBriefing({
            researchApi,
            query,
            pmcIds,
            candidateMeta,
          });
        } catch (error) {
          console.error('Error in buildInterimAbstractBriefing tool:', error);
          return {
            enabled: true,
            success: false,
            error: true,
            evidenceKind: 'abstract-only',
            message: `Failed to build interim abstract briefing: ${error instanceof Error ? error.message : 'Unknown error'}`,
            interimMarkdown: '',
            sources: [],
          };
        }
      },
    }),

    searchLiterature: tool({
      description:
        'Search PubMed/Entrez for scientific literature IDs. Use after a graph miss when the user wants external papers, ' +
        'or when explicitly asked to search literature. This does NOT query the Neo4j corpus and does not ingest papers.',
      inputSchema: z.object({
        query: z.string().describe('The search query for finding literature'),
        startIndex: z
          .number()
          .optional()
          .default(0)
          .describe('The starting index for pagination (default: 0)'),
      }),
      execute: async ({ query, startIndex = 0 }) => {
        try {
          console.log(`Searching literature with query: ${query}, startIndex: ${startIndex}`);
          const result = await researchApi.searchLiterature(query, startIndex);
          return { success: true, data: result };
        } catch (error) {
          console.error('Error in searchLiterature tool:', error);
          return {
            error: true,
            message: `Failed to search literature: ${error instanceof Error ? error.message : 'Unknown error'}`,
          };
        }
      },
    }),

    getLiteratureSummary: tool({
      description:
        'Get summaries for PubMed literature IDs (titles, PMCID, DOI). Use after searchLiterature. Does not ingest into the graph.',
      inputSchema: z.object({
        ids: z.array(z.string()).describe('Array of literature IDs to get summaries for'),
      }),
      execute: async ({ ids }) => {
        try {
          console.log(`Getting literature summaries for ids: ${ids.join(', ')}`);
          const result = await researchApi.getLiteratureSummary(ids);
          return { success: true, data: result };
        } catch (error) {
          console.error('Error in getLiteratureSummary tool:', error);
          return {
            error: true,
            message: `Failed to get literature summaries: ${error instanceof Error ? error.message : 'Unknown error'}`,
          };
        }
      },
    }),

    getLiteratureAbstract: tool({
      description:
        'Get the abstract for a single PMC article. Prefer buildInterimAbstractBriefing for multi-paper interim answers. ' +
        'Abstracts are NOT graph corpus evidence — never emit [Source n] citations from abstracts alone.',
      inputSchema: z.object({
        pmcId: z.string().describe('The PubMed Central ID of the article'),
      }),
      execute: async ({ pmcId }) => {
        try {
          console.log(`Getting abstract for PMC ID: ${pmcId}`);
          const result = await researchApi.getLiteratureAbstract(pmcId);
          return { success: true, data: result };
        } catch (error) {
          console.error('Error in getLiteratureAbstract tool:', error);
          return {
            error: true,
            message: `Failed to get literature abstract: ${error instanceof Error ? error.message : 'Unknown error'}`,
          };
        }
      },
    }),

    getDownloadLink: tool({
      description:
        'Resolve an open-access download link for a PMC ID. This only returns links; it does NOT download into BioAnalyzer storage or run the processing pipeline.',
      inputSchema: z.object({
        pmcId: z.string().describe('The PubMed Central ID of the article to resolve'),
      }),
      execute: async ({ pmcId }) => {
        try {
          console.log(`Getting download link for PMC ID: ${pmcId}`);
          const result = await researchApi.getDownloadLink(pmcId);
          return { success: true, data: result };
        } catch (error) {
          console.error('Error in getDownloadLink tool:', error);
          return {
            error: true,
            message: `Failed to get download link: ${error instanceof Error ? error.message : 'Unknown error'}`,
          };
        }
      },
    }),

    downloadFile: tool({
      description:
        'Download a previously stored literature file by filename from BioAnalyzer blob storage. Not used for new PMC ingest.',
      inputSchema: z.object({
        fileName: z.string().describe('The name of the file to download'),
      }),
      execute: async ({ fileName }) => {
        try {
          console.log(`Downloading file: ${fileName}`);
          const blob = await researchApi.downloadFile(fileName);
          return {
            success: true,
            message: `File ${fileName} downloaded successfully`,
            data: {
              fileName,
              size: blob.size,
              type: blob.type,
            },
          };
        } catch (error) {
          console.error('Error in downloadFile tool:', error);
          return {
            error: true,
            message: `Failed to download file: ${error instanceof Error ? error.message : 'Unknown error'}`,
          };
        }
      },
    }),
  } as const;

  const filtered: Record<string, unknown> = { askGraph: tools.askGraph };

  if (flags.expansionToolsEnabled) {
    filtered.findLiteratureCandidates = tools.findLiteratureCandidates;
    filtered.searchLiterature = tools.searchLiterature;
    filtered.getLiteratureSummary = tools.getLiteratureSummary;
    filtered.getLiteratureAbstract = tools.getLiteratureAbstract;
    filtered.getDownloadLink = tools.getDownloadLink;
    // Phase C2: downloadFile is hard-gated off by default (AGENT_BLOB_DOWNLOAD_TOOL_ENABLED).
    if (flags.blobDownloadToolEnabled) {
      filtered.downloadFile = tools.downloadFile;
    }
  }

  if (flags.expansionToolsEnabled && flags.interimAbstractsEnabled) {
    filtered.buildInterimAbstractBriefing = tools.buildInterimAbstractBriefing;
  }

  if (flags.expansionToolsEnabled && flags.autoIngestEnabled) {
    filtered.requestLiteratureIngest = tools.requestLiteratureIngest;
    filtered.getIngestJobStatus = tools.getIngestJobStatus;
    filtered.waitForIngestAndAskGraph = tools.waitForIngestAndAskGraph;
  }

  return filtered as typeof tools;
}

export type ResearchToolSet = ReturnType<typeof getResearchTools>;
