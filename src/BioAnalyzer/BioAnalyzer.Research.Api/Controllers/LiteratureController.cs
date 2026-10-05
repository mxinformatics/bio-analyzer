using BioAnalyzer.Research.Api.Domain.Models;
using BioAnalyzer.Research.Api.Domain.Services;
using BioAnalyzer.Research.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioAnalyzer.Research.Api.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize(Policy = ResearchApiAuthentication.DefaultPolicyName)]
public class LiteratureController(
    ILiteratureSearchService literatureSearchService,
    ILiteratureExpansionService literatureExpansionService,
    ILiteratureIngestService literatureIngestService,
    ILiteratureService literatureService) : ControllerBase
{
    private const string DefaultContentType = "application/json";
    
    [HttpGet(Name = "SearchLiterature")]
    [ProducesResponseType(typeof(EntrezSearchResult), 200, contentType: DefaultContentType)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<EntrezSearchResult>> Search([FromQuery] string query, [FromQuery] int startIndex, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Query cannot be null or empty.");
        }
var result = await literatureSearchService.SearchLiteratureAsync(query, startIndex, cancellationToken: cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
    
    [HttpGet("summary", Name = "GetLiteratureSummary")]
    [ProducesResponseType(typeof(IList<EntrezSummaryResult>), 200, contentType: DefaultContentType)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IList<EntrezSummaryResult>>> GetSummary([FromQuery] IList<string> ids, CancellationToken cancellationToken)
    {
        if (!ids.Any())
        {
            return Ok(new List<EntrezSummaryResult>());
        }
        
        var result = await literatureSearchService.GetLiteratureSummaries(ids, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
    
    [HttpGet("metadata", Name = "GetArticleMetadata")]
    [ProducesResponseType(typeof(ArticleMetadataResult), 200, contentType: DefaultContentType)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ArticleMetadataResult>> GetMetadata([FromQuery] string pmcId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pmcId))
        {
            return BadRequest("pmcId cannot be null or empty.");
        }
        var result = await literatureSearchService.GetArticleMetadataAsync(pmcId, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

[HttpGet("abstract", Name = "GetLiteratureAbstract")]
    [ProducesResponseType(typeof(ArticleAbstract), 200, contentType: DefaultContentType)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ArticleAbstract>> GetAbstract([FromQuery] string pmcId, CancellationToken cancellationToken)
    {
        var articleAbstract = await literatureSearchService.GetArticleAbstractAsync(pmcId, cancellationToken).ConfigureAwait(false); 
        return Ok(articleAbstract);
    }

    /// <summary>
    /// Batch abstract fetch for interim agent answers while full-text ingest runs (Phase 5).
    /// </summary>
    [HttpPost("abstracts/batch", Name = "GetLiteratureAbstractBatch")]
    [ProducesResponseType(typeof(LiteratureAbstractBatchResult), StatusCodes.Status200OK, DefaultContentType)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LiteratureAbstractBatchResult>> GetAbstractBatch(
        [FromBody] LiteratureAbstractBatchRequest request,
        CancellationToken cancellationToken)
    {
        if (request?.PmcIds is null || request.PmcIds.Count == 0)
        {
            return BadRequest("At least one pmcId is required.");
        }

        var distinctIds = request.PmcIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        if (distinctIds.Count == 0)
        {
            return BadRequest("At least one pmcId is required.");
        }

        var result = new LiteratureAbstractBatchResult();
        foreach (var pmcId in distinctIds)
        {
            try
            {
                var articleAbstract = await literatureSearchService
                    .GetArticleAbstractAsync(pmcId, cancellationToken)
                    .ConfigureAwait(false);
                result.Items.Add(new LiteratureAbstractBatchItem
                {
                    PmcId = pmcId,
                    Title = articleAbstract.Title ?? string.Empty,
                    Description = articleAbstract.Description ?? string.Empty
                });
            }
            catch (Exception ex)
            {
                result.Failures.Add(new LiteratureAbstractBatchItem
                {
                    PmcId = pmcId,
                    ErrorMessage = ex.Message
                });
            }
        }

        return Ok(result);
    }
    
[HttpGet("download", Name = "DownloadLiteratureReference")]
    [ProducesResponseType(typeof(LiteratureDownloadLinkResult), 200, contentType: DefaultContentType)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<LiteratureDownloadLinkResult>> DownloadReference([FromQuery] string pmcId, CancellationToken cancellationToken)
    {
        var downloadLinkResponse = await literatureSearchService.GetLiteratureDownloadLinkAsync(pmcId, cancellationToken).ConfigureAwait(false);
        return Ok(downloadLinkResponse);
    }

    /// <summary>
    /// Ranked OA-ingestible literature candidates for agentic expansion (Phase 2).
    /// </summary>
    [HttpPost("candidates", Name = "FindLiteratureCandidates")]
    [ProducesResponseType(typeof(LiteratureCandidatesResult), StatusCodes.Status200OK, DefaultContentType)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LiteratureCandidatesResult>> FindCandidates(
        [FromBody] LiteratureCandidatesRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest("Query cannot be null or empty.");
        }

var result = await literatureExpansionService
            .FindCandidatesAsync(request, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Enqueue OA literature downloads into the existing document pipeline (Phase 3).
    /// </summary>
    [HttpPost("ingest", Name = "RequestLiteratureIngest")]
    [ProducesResponseType(typeof(LiteratureIngestResponse), StatusCodes.Status200OK, DefaultContentType)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
public async Task<ActionResult<LiteratureIngestResponse>> RequestIngest(
        [FromBody] LiteratureIngestRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || request.Items is null || request.Items.Count == 0)
        {
            return BadRequest("At least one ingest item is required.");
        }

        // Phase C1: never trust body.requestedBy or X-Requester-Id. Bind from Entra/API-key principal only.
        request.RequestedBy = RequesterIdentity.Resolve(User) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey)
            && Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyHeader))
        {
            request.IdempotencyKey = idempotencyHeader.ToString();
        }

        try
        {
            var result = await literatureIngestService
                .RequestIngestAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Get agentic literature ingest job status.
    /// </summary>
    [HttpGet("ingest/{jobId}", Name = "GetLiteratureIngestJobStatus")]
    [ProducesResponseType(typeof(LiteratureIngestJobStatusResult), StatusCodes.Status200OK, DefaultContentType)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LiteratureIngestJobStatusResult>> GetIngestJobStatus(
        [FromRoute] string jobId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return BadRequest("jobId is required.");
        }

        // Phase C1: requester from authenticated principal only (ignore X-Requester-Id).
        var requester = RequesterIdentity.Resolve(User);

        try
        {
            var result = await literatureIngestService
                .GetJobStatusAsync(jobId, requester, cancellationToken)
                .ConfigureAwait(false);
            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
    
    [HttpGet("downloads/view", Name = "ViewDownloads")]
    [ProducesResponseType(typeof(LiteratureDownloadList), 200, contentType: DefaultContentType)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<LiteratureDownloadList>> ViewDownloads(CancellationToken cancellationToken)
    {
        var downloadList = await literatureService.GetDownloadsAsync(cancellationToken).ConfigureAwait(false);
        return Ok(downloadList);
    }

    [HttpGet("downloads/{fileName}", Name = "DownloadFile")]
    [ProducesResponseType(typeof(FileContentResult), 200, contentType: DefaultContentType)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> DownloadFile([FromRoute] string fileName, CancellationToken cancellationToken)
    {
        try
        {
            var fileStream = await literatureService
                .DownloadFileAsync(fileName, cancellationToken)
                .ConfigureAwait(false);

            // Re-validate for Content-Disposition filename (safe display name only).
            if (!DownloadFileName.IsValid(fileName, out var safeName, out _))
            {
                safeName = "download.bin";
            }

            return File(fileStream, ResolveContentType(safeName), safeName, enableRangeProcessing: true);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("uploads/manual", Name = "UploadManualLiterature")]
    [ProducesResponseType(typeof(LiteratureDownload), 200, contentType: DefaultContentType)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<ActionResult<LiteratureDownload>> UploadManual(
        [FromForm] ManualLiteratureUploadRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.Length <= 0)
        {
            return BadRequest("A file is required.");
        }

        var extension = Path.GetExtension(request.File.FileName);
        if (!AllowedManualUploadExtensions.Contains(extension))
        {
            return BadRequest("Unsupported file type. Allowed: .pdf, .xml, .nxml");
        }

        var result = await literatureService.UploadManualAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    private static readonly HashSet<string> AllowedManualUploadExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".xml",
        ".nxml"
    };

    private static string ResolveContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return extension.ToLowerInvariant() switch
        {
            ".xml" => "application/xml",
            ".nxml" => "application/xml",
            _ => System.Net.Mime.MediaTypeNames.Application.Pdf
        };
    }
}