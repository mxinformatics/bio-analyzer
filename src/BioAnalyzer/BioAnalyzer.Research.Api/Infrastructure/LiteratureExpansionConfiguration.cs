namespace BioAnalyzer.Research.Api.Infrastructure;

public class LiteratureExpansionConfiguration
{
    /// <summary>Master switch for candidate discovery (POST /Literature/candidates).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>When false, POST /Literature/ingest rejects new enqueue requests.</summary>
    public bool AutoIngestEnabled { get; set; } = true;

    /// <summary>Maximum OA candidates returned to the agent.</summary>
    public int MaxCandidates { get; set; } = 3;

    /// <summary>How many PubMed IDs to request from Entrez before filtering.</summary>
    public int SearchRetMax { get; set; } = 20;

    /// <summary>Max concurrent PMC OA link resolution calls.</summary>
    public int MaxConcurrentLinkResolutions { get; set; } = 4;

    /// <summary>When true, only rows with a PMCID are considered.</summary>
    public bool RequirePmcId { get; set; } = true;

    /// <summary>When true, require a resolvable PDF or XML link from PMC Open Data.</summary>
    public bool RequireOpenAccessLink { get; set; } = true;

    /// <summary>
    /// Max ingest jobs per requester identity per UTC day (0 = unlimited).
    /// Identity is derived from the authenticated Entra principal (oid/appid) or API-key subject.
    /// </summary>
    public int MaxIngestJobsPerRequesterPerDay { get; set; } = 20;

    /// <summary>
    /// When true, ingest and restricted status reads require a resolved principal identity.
    /// Production default: true. Local Aspire without Entra may set false only with care.
    /// </summary>
    public bool RequireRequesterIdentity { get; set; } = true;

    /// <summary>
    /// When true (dev only recommended), allow missing identity when auth is not enforced.
    /// Never enable in shared/prod environments.
    /// </summary>
    public bool AllowAnonymousRequester { get; set; } = false;

    /// <summary>When true, Idempotency-Key (or body.idempotencyKey) is required on ingest.</summary>
    public bool RequireIdempotencyKey { get; set; } = true;

    /// <summary>
    /// When true, status reads require the caller's verified identity to match job.RequestedBy.
    /// Meaningful only after Entra (or API-key subject) binding — do not rely on client headers.
    /// </summary>
    public bool RestrictJobStatusToRequester { get; set; } = true;

    /// <summary>Max characters stored in QueryPreview (PII minimization).</summary>
    public int QueryPreviewMaxLength { get; set; } = 220;

    public void ThrowIfInvalid()
    {
        if (MaxCandidates <= 0)
        {
            throw new InvalidOperationException("LiteratureExpansion:MaxCandidates must be greater than zero");
        }

        if (SearchRetMax <= 0)
        {
            throw new InvalidOperationException("LiteratureExpansion:SearchRetMax must be greater than zero");
        }

        if (MaxConcurrentLinkResolutions <= 0)
        {
            throw new InvalidOperationException("LiteratureExpansion:MaxConcurrentLinkResolutions must be greater than zero");
        }

        if (MaxIngestJobsPerRequesterPerDay < 0)
        {
            throw new InvalidOperationException("LiteratureExpansion:MaxIngestJobsPerRequesterPerDay cannot be negative");
        }

        if (QueryPreviewMaxLength <= 0)
        {
            throw new InvalidOperationException("LiteratureExpansion:QueryPreviewMaxLength must be greater than zero");
        }
    }
}
