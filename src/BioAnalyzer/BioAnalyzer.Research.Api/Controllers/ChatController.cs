using BioAnalyzer.Research.Api.Domain.Models;
using BioAnalyzer.Research.Api.Domain.Services;
using BioAnalyzer.Research.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioAnalyzer.Research.Api.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize(Policy = ResearchApiAuthentication.DefaultPolicyName)]
public class ChatController(IAIQueryService queryService) : ControllerBase
{
    private const string DefaultContentType = "application/json";

    [HttpGet]
    public async Task<ActionResult<string>> Query([FromQuery] string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Query cannot be null or empty.");
        }

        var response = await queryService.QueryAsync(query, cancellationToken).ConfigureAwait(false);
        return Ok(response);
    }

    /// <summary>
    /// Structured graph-RAG result for agent tools (machine-readable status).
    /// </summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(ChatQueryResult), StatusCodes.Status200OK, DefaultContentType)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatQueryResult>> QueryStructured(
        [FromBody] ChatQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest("Query cannot be null or empty.");
        }

        var response = await queryService
            .QueryStructuredAsync(request.Query.Trim(), cancellationToken)
            .ConfigureAwait(false);
        return Ok(response);
    }

    /// <summary>
    /// GET convenience endpoint for structured graph-RAG results.
    /// </summary>
    [HttpGet("evidence")]
    [ProducesResponseType(typeof(ChatQueryResult), StatusCodes.Status200OK, DefaultContentType)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatQueryResult>> QueryEvidence(
        [FromQuery] string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Query cannot be null or empty.");
        }

        var response = await queryService
            .QueryStructuredAsync(query.Trim(), cancellationToken)
            .ConfigureAwait(false);
        return Ok(response);
    }

    [HttpGet("stream")]
    public async Task StreamQuery([FromQuery] string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new { error = "Query cannot be null or empty." }, cancellationToken).ConfigureAwait(false);
            return;
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers.Append("X-Accel-Buffering", "no");
        await foreach (var responsePart in queryService.StreamQueryAsync(query, cancellationToken).WithCancellation(cancellationToken))
        {
            if (string.IsNullOrEmpty(responsePart))
            {
                continue;
            }
            var normalizedPayload = responsePart.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
            var ssePayload = normalizedPayload.Replace("\n", "\ndata: ", StringComparison.Ordinal);
            await Response.WriteAsync($"data: {ssePayload}\n\n", cancellationToken).ConfigureAwait(false);
            await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        await Response.WriteAsync("event: done\\ndata: [DONE]\\n\\n", cancellationToken).ConfigureAwait(false);
        await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
