using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Azure.AI.Inference;
using BioAnalyzer.Research.Api.Domain.Models;
using BioAnalyzer.Research.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace BioAnalyzer.Research.Api.Domain.Clients;

public class AiClient(
    ChatCompletionsClient chatCompletionsClient,
    IGraphQueryClient graphQueryClient,
    IRetrievalMetricsClient retrievalMetricsClient,
    IOptions<OpenAiConfiguration> configuration,
    ILogger<AiClient> logger) : IAiClient
{
    private static readonly Regex SourceCitationRegex = new(@"\[Source\s+\d+\]", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex BulletLineRegex = new(@"^\s*(?:[-*•]|\d+\.)\s+", RegexOptions.Compiled);
    private const string InsufficientEvidenceResponse =
        "I don't know based on the currently available graph evidence for that question.";

    // Stable retrieval metric status strings (table/metrics contract).
    private const string MetricSuccess = "Success";
    private const string MetricThresholdBypass = "ThresholdBypass";
    private const string MetricNoResults = "NoResults";
    private const string MetricInsufficientEvidence = "InsufficientEvidence";
    private const string MetricGraphRetrievalFailed = "GraphRetrievalFailed";

    private readonly ChatCompletionsClient _chatClient = chatCompletionsClient;
    private readonly IGraphQueryClient _graphQueryClient = graphQueryClient;
    private readonly IRetrievalMetricsClient _retrievalMetricsClient = retrievalMetricsClient;
    private readonly OpenAiConfiguration _configuration = configuration.Value;
    private readonly ILogger<AiClient> _logger = logger;

    public async Task<string> QueryAsync(string query, CancellationToken cancellationToken = default)
    {
        var result = await QueryStructuredAsync(query, cancellationToken).ConfigureAwait(false);
        return result.Answer;
    }

    public async IAsyncEnumerable<string> StreamQueryAsync(
        string query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var result = await QueryStructuredAsync(query, cancellationToken).ConfigureAwait(false);
        foreach (var chunk in ChunkForStreaming(result.Answer, 180))
        {
            yield return chunk;
        }
    }

    public async Task<ChatQueryResult> QueryStructuredAsync(string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var evidencePackage = await QueryDocuments(query, cancellationToken).ConfigureAwait(false);
        var baseResult = BuildBaseResult(query, evidencePackage);

        if (string.IsNullOrWhiteSpace(evidencePackage.SourcesText)
            || evidencePackage.ChatStatus is ChatQueryStatus.NoResults
                or ChatQueryStatus.InsufficientEvidence
                or ChatQueryStatus.GraphUnavailable)
        {
            _logger.LogInformation(
                "Returning structured fallback for query status {Status}: {Query}",
                evidencePackage.ChatStatus,
                query);

            baseResult.Status = evidencePackage.ChatStatus;
            baseResult.Answer = InsufficientEvidenceResponse;
            baseResult.ErrorMessage = string.IsNullOrWhiteSpace(evidencePackage.ErrorMessage)
                ? null
                : evidencePackage.ErrorMessage;
            return baseResult;
        }

        var response = await QueryOpenAi(query, evidencePackage.SourcesText, cancellationToken).ConfigureAwait(false);
        var guardedResponse = ApplyResponseGuardrails(response, evidencePackage.SourceCount, query);

        if (string.Equals(guardedResponse, InsufficientEvidenceResponse, StringComparison.Ordinal))
        {
            baseResult.Status = ChatQueryStatus.InsufficientEvidence;
            baseResult.Answer = InsufficientEvidenceResponse;
            baseResult.RetrievalMetricStatus = string.IsNullOrWhiteSpace(baseResult.RetrievalMetricStatus)
                ? MetricInsufficientEvidence
                : baseResult.RetrievalMetricStatus;
            return baseResult;
        }

        baseResult.Status = ChatQueryStatus.Answered;
        baseResult.Answer = guardedResponse;
        return baseResult;
    }

    private async Task<EvidencePackage> QueryDocuments(string query, CancellationToken cancellationToken = default)
    {
        var retrievalStopwatch = Stopwatch.StartNew();
        IReadOnlyList<GraphQueryResultItem> searchResults;
        try
        {
            searchResults = await _graphQueryClient
                .QueryAsync(query, _configuration.GraphTopK, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            retrievalStopwatch.Stop();
            _logger.LogWarning(ex, "Graph retrieval failed for query: {Query}", query);
            await PersistRetrievalMetricAsync(
                    BuildRetrievalMetric(
                        query,
                        status: MetricGraphRetrievalFailed,
                        allResults: [],
                        selectedResults: [],
                        retrievalDurationMs: (int)retrievalStopwatch.ElapsedMilliseconds,
                        errorMessage: ex.Message),
                    cancellationToken)
                .ConfigureAwait(false);
            return EvidencePackage.Failed(
                ChatQueryStatus.GraphUnavailable,
                MetricGraphRetrievalFailed,
                (int)retrievalStopwatch.ElapsedMilliseconds,
                ex.Message);
        }

        if (searchResults.Count == 0)
        {
            retrievalStopwatch.Stop();
            _logger.LogInformation("Graph retrieval returned zero results for query: {Query}", query);
            await PersistRetrievalMetricAsync(
                    BuildRetrievalMetric(
                        query,
                        status: MetricNoResults,
                        allResults: searchResults,
                        selectedResults: [],
                        retrievalDurationMs: (int)retrievalStopwatch.ElapsedMilliseconds),
                    cancellationToken)
                .ConfigureAwait(false);
            return EvidencePackage.Empty(
                ChatQueryStatus.NoResults,
                MetricNoResults,
                searchResults,
                [],
                (int)retrievalStopwatch.ElapsedMilliseconds);
        }

        var selectedResults = searchResults
            .Where(result => result.Score >= _configuration.GraphEvidenceMinScore)
            .OrderByDescending(result => result.Score)
            .Take(_configuration.GraphMaxEvidenceCount)
            .ToList();

        LogRetrievalEvaluation(query, searchResults, selectedResults);
        retrievalStopwatch.Stop();
        var retrievalStatus = MetricSuccess;

        if (selectedResults.Count < _configuration.GraphMinEvidenceCount)
        {
            var fallbackSelectedResults = searchResults
                .OrderByDescending(result => result.Score)
                .Take(_configuration.GraphMaxEvidenceCount)
                .ToList();

            if (fallbackSelectedResults.Count >= _configuration.GraphMinEvidenceCount)
            {
                _logger.LogWarning(
                    "Graph retrieval results did not meet score threshold criteria. Required={Required} ThresholdSelected={Selected} UsingTopResults={FallbackSelected}",
                    _configuration.GraphMinEvidenceCount,
                    selectedResults.Count,
                    fallbackSelectedResults.Count);
                selectedResults = fallbackSelectedResults;
                retrievalStatus = MetricThresholdBypass;
            }
            else
            {
                _logger.LogInformation(
                    "Graph retrieval results did not meet minimum evidence count. Required={Required} Selected={Selected}",
                    _configuration.GraphMinEvidenceCount,
                    selectedResults.Count);
                await PersistRetrievalMetricAsync(
                        BuildRetrievalMetric(
                            query,
                            status: MetricInsufficientEvidence,
                            allResults: searchResults,
                            selectedResults: selectedResults,
                            retrievalDurationMs: (int)retrievalStopwatch.ElapsedMilliseconds),
                        cancellationToken)
                    .ConfigureAwait(false);
                return EvidencePackage.Empty(
                    ChatQueryStatus.InsufficientEvidence,
                    MetricInsufficientEvidence,
                    searchResults,
                    selectedResults,
                    (int)retrievalStopwatch.ElapsedMilliseconds);
            }
        }

        await PersistRetrievalMetricAsync(
                BuildRetrievalMetric(
                    query,
                    status: retrievalStatus,
                    allResults: searchResults,
                    selectedResults: selectedResults,
                    retrievalDurationMs: (int)retrievalStopwatch.ElapsedMilliseconds),
                cancellationToken)
            .ConfigureAwait(false);

        var sourceModels = new List<ChatEvidenceSource>(selectedResults.Count);
        var sources = new List<string>(selectedResults.Count);
        for (var i = 0; i < selectedResults.Count; i++)
        {
            var result = selectedResults[i];
            var documentName = string.IsNullOrWhiteSpace(result.DocumentName) ? "Unknown Document" : result.DocumentName;
            var sourceSystemId = string.IsNullOrWhiteSpace(result.SourceSystemDocId) ? "N/A" : result.SourceSystemDocId;
            var evidenceText = NormalizeEvidenceText(result.Text);
            var pmcId = InferPmcId(sourceSystemId, documentName);

            sourceModels.Add(new ChatEvidenceSource
            {
                Index = i + 1,
                DocumentName = documentName,
                SourceSystemDocId = sourceSystemId == "N/A" ? string.Empty : sourceSystemId,
                PmcId = pmcId,
                PageNumber = result.PageNumber,
                ChunkIndex = result.ChunkIndex,
                Score = result.Score,
                Evidence = evidenceText
            });

            var chunkIndex = result.ChunkIndex > 0 ? result.ChunkIndex.ToString() : "N/A";
            sources.Add(
                $"[Source {i + 1}] Document={documentName}; Page={result.PageNumber}; Chunk={chunkIndex}; Score={result.Score:F3}; SourceSystemDocId={sourceSystemId}; Evidence={evidenceText}");
        }

        return new EvidencePackage(
            string.Join("\n", sources),
            selectedResults.Count,
            ChatQueryStatus.Answered,
            retrievalStatus,
            searchResults,
            selectedResults,
            sourceModels,
            (int)retrievalStopwatch.ElapsedMilliseconds,
            errorMessage: string.Empty);
    }

    private ChatQueryResult BuildBaseResult(string query, EvidencePackage evidencePackage)
    {
        var (minScore, avgScore, maxScore) = CalculateScoreSummary(evidencePackage.AllResults);
        return new ChatQueryResult
        {
            Query = query,
            QueryHash = ComputeQueryHash(query),
            RetrievalMetricStatus = evidencePackage.RetrievalMetricStatus,
            Sources = evidencePackage.SourceModels.ToList(),
            Retrieval = new ChatRetrievalSummary
            {
                TotalResults = evidencePackage.AllResults.Count,
                SelectedResults = evidencePackage.SelectedResults.Count,
                MinScore = minScore,
                AvgScore = avgScore,
                MaxScore = maxScore,
                Threshold = _configuration.GraphEvidenceMinScore,
                TopK = _configuration.GraphTopK,
                MinEvidenceCount = _configuration.GraphMinEvidenceCount,
                MaxEvidenceCount = _configuration.GraphMaxEvidenceCount,
                RetrievalDurationMs = evidencePackage.RetrievalDurationMs
            }
        };
    }

    private async Task<string> QueryOpenAi(string query, string sources, CancellationToken cancellationToken = default)
    {
        var prompt = $"""
                      You are an experienced biological researcher with deep molecular biology knowledge.
                      Answer the query using only the source evidence provided below.
                      Requirements:
                      - Respond in concise bullet points.
                      - Every factual bullet must include at least one source citation in the format [Source n].
                      - Do not use knowledge outside the provided sources.
                      - If evidence is insufficient, respond exactly with: "I don't know based on the currently available graph evidence for that question."
                      Query: {query}
                      Sources:
                      {sources}
                      """;

        var options = new ChatCompletionsOptions
        {
            Model = _configuration.GptName,
            Temperature = 0.0f
        };
        options.AdditionalProperties.Add("max_completion_tokens", BinaryData.FromObjectAsJson(1000));
        options.Messages.Add(new ChatRequestUserMessage(prompt));

        var chatUpdates = await _chatClient.CompleteStreamingAsync(options, cancellationToken).ConfigureAwait(false);

        var response = new StringBuilder(500);
        await foreach (var chatUpdate in chatUpdates.WithCancellation(cancellationToken))
        {
            if (chatUpdate.ContentUpdate != null)
            {
                response.Append(chatUpdate.ContentUpdate);
            }
        }

        return response.ToString();
    }

    private string ApplyResponseGuardrails(string response, int sourceCount, string query)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            _logger.LogWarning("LLM returned an empty response for query: {Query}", query);
            return InsufficientEvidenceResponse;
        }

        var trimmedResponse = response.Trim();
        if (string.Equals(trimmedResponse, InsufficientEvidenceResponse, StringComparison.Ordinal))
        {
            return trimmedResponse;
        }

        if (_configuration.GraphRequireCitationsInResponse
            && sourceCount > 0
            && !HasCitationPerFactualBullet(trimmedResponse))
        {
            _logger.LogWarning(
                "LLM response for query {Query} did not include source citations in the expected format. Returning answer without blocking fallback.",
                query);
            return trimmedResponse;
        }

        return trimmedResponse;
    }

    private void LogRetrievalEvaluation(
        string query,
        IReadOnlyList<GraphQueryResultItem> allResults,
        IReadOnlyList<GraphQueryResultItem> selectedResults)
    {
        var averageScore = allResults.Average(result => result.Score);
        var maxScore = allResults.Max(result => result.Score);
        var minScore = allResults.Min(result => result.Score);

        _logger.LogInformation(
            "Graph retrieval evaluation for query {Query}: TotalResults={TotalResults}, SelectedResults={SelectedResults}, MinScore={MinScore:F3}, AvgScore={AverageScore:F3}, MaxScore={MaxScore:F3}, EvidenceThreshold={EvidenceThreshold:F3}",
            query,
            allResults.Count,
            selectedResults.Count,
            minScore,
            averageScore,
            maxScore,
            _configuration.GraphEvidenceMinScore);
    }

    private RetrievalEvaluationMetric BuildRetrievalMetric(
        string query,
        string status,
        IReadOnlyList<GraphQueryResultItem> allResults,
        IReadOnlyList<GraphQueryResultItem> selectedResults,
        int retrievalDurationMs,
        string errorMessage = "")
    {
        var (minScore, avgScore, maxScore) = CalculateScoreSummary(allResults);
        return new RetrievalEvaluationMetric
        {
            QueryHash = ComputeQueryHash(query),
            QueryPreview = query.Length > 220 ? $"{query[..220]}..." : query,
            Status = status,
            TotalResults = allResults.Count,
            SelectedResults = selectedResults.Count,
            MinScore = minScore,
            AvgScore = avgScore,
            MaxScore = maxScore,
            EvidenceThreshold = _configuration.GraphEvidenceMinScore,
            TopK = _configuration.GraphTopK,
            MinEvidenceCount = _configuration.GraphMinEvidenceCount,
            MaxEvidenceCount = _configuration.GraphMaxEvidenceCount,
            RetrievalDurationMs = retrievalDurationMs,
            ErrorMessage = errorMessage,
            EvaluatedUtc = DateTimeOffset.UtcNow
        };
    }

    private async Task PersistRetrievalMetricAsync(
        RetrievalEvaluationMetric metric,
        CancellationToken cancellationToken)
    {
        try
        {
            await _retrievalMetricsClient.SaveAsync(metric, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to persist retrieval evaluation metric for query hash: {QueryHash}",
                metric.QueryHash);
        }
    }

    private static (double MinScore, double AvgScore, double MaxScore) CalculateScoreSummary(
        IReadOnlyList<GraphQueryResultItem> allResults)
    {
        if (allResults.Count == 0)
        {
            return (0, 0, 0);
        }

        return (
            allResults.Min(result => result.Score),
            allResults.Average(result => result.Score),
            allResults.Max(result => result.Score));
    }

    private static string ComputeQueryHash(string query)
    {
        var bytes = Encoding.UTF8.GetBytes(query);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }

    private static string NormalizeEvidenceText(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalized = WhitespaceRegex.Replace(input, " ").Trim();
        return normalized.Length > 700 ? $"{normalized[..700]}..." : normalized;
    }

    private static string InferPmcId(string sourceSystemDocId, string documentName)
    {
        foreach (var candidate in new[] { sourceSystemDocId, documentName })
        {
            if (string.IsNullOrWhiteSpace(candidate) || candidate == "N/A")
            {
                continue;
            }

            var match = Regex.Match(candidate, @"PMC\d+", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Value.ToUpperInvariant();
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> ChunkForStreaming(string text, int chunkSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        for (var index = 0; index < text.Length; index += chunkSize)
        {
            var length = Math.Min(chunkSize, text.Length - index);
            yield return text.Substring(index, length);
        }
    }

    private sealed class EvidencePackage
    {
        public EvidencePackage(
            string sourcesText,
            int sourceCount,
            string chatStatus,
            string retrievalMetricStatus,
            IReadOnlyList<GraphQueryResultItem> allResults,
            IReadOnlyList<GraphQueryResultItem> selectedResults,
            IReadOnlyList<ChatEvidenceSource> sourceModels,
            int retrievalDurationMs,
            string errorMessage)
        {
            SourcesText = sourcesText;
            SourceCount = sourceCount;
            ChatStatus = chatStatus;
            RetrievalMetricStatus = retrievalMetricStatus;
            AllResults = allResults;
            SelectedResults = selectedResults;
            SourceModels = sourceModels;
            RetrievalDurationMs = retrievalDurationMs;
            ErrorMessage = errorMessage;
        }

        public string SourcesText { get; }
        public int SourceCount { get; }
        public string ChatStatus { get; }
        public string RetrievalMetricStatus { get; }
        public IReadOnlyList<GraphQueryResultItem> AllResults { get; }
        public IReadOnlyList<GraphQueryResultItem> SelectedResults { get; }
        public IReadOnlyList<ChatEvidenceSource> SourceModels { get; }
        public int RetrievalDurationMs { get; }
        public string ErrorMessage { get; }

        public static EvidencePackage Empty(
            string chatStatus,
            string retrievalMetricStatus,
            IReadOnlyList<GraphQueryResultItem> allResults,
            IReadOnlyList<GraphQueryResultItem> selectedResults,
            int retrievalDurationMs) =>
            new(
                string.Empty,
                0,
                chatStatus,
                retrievalMetricStatus,
                allResults,
                selectedResults,
                [],
                retrievalDurationMs,
                string.Empty);

        public static EvidencePackage Failed(
            string chatStatus,
            string retrievalMetricStatus,
            int retrievalDurationMs,
            string errorMessage) =>
            new(
                string.Empty,
                0,
                chatStatus,
                retrievalMetricStatus,
                [],
                [],
                [],
                retrievalDurationMs,
                errorMessage);
    }

    private bool HasCitationPerFactualBullet(string response)
    {
        var lines = response
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        var bulletLines = lines.Where(line => BulletLineRegex.IsMatch(line)).ToList();
        if (bulletLines.Count == 0)
        {
            return false;
        }

        foreach (var bulletLine in bulletLines)
        {
            var normalizedLine = bulletLine.ToLowerInvariant();
            var isNonFactual = normalizedLine.Contains("i don't know")
                               || normalizedLine.Contains("insufficient evidence");
            if (isNonFactual)
            {
                continue;
            }

            if (!SourceCitationRegex.IsMatch(bulletLine))
            {
                return false;
            }
        }

        return true;
    }
}
