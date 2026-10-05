namespace BioAnalyzer.Research.Api.Domain.Models;

public class LiteratureCandidatesRequest
{
    public string Query { get; set; } = string.Empty;

    /// <summary>Override configured MaxCandidates when &gt; 0.</summary>
    public int MaxCandidates { get; set; }

    public bool? RequirePmcId { get; set; }

    public bool? RequireOpenAccessLink { get; set; }
}

public class LiteratureCandidatesResult
{
    public string Query { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public int SearchHitCount { get; set; }

    public int SummariesConsidered { get; set; }

    public int LinkResolutionsAttempted { get; set; }

    public IList<LiteratureCandidate> Candidates { get; set; } = [];

    public IList<string> SkippedReasons { get; set; } = [];
}

public class LiteratureCandidate
{
    public string Pmid { get; set; } = string.Empty;

    public string PmcId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Doi { get; set; } = string.Empty;

    public string PdfLink { get; set; } = string.Empty;

    public string XmlLink { get; set; } = string.Empty;

    public bool AlreadyIngested { get; set; }

    public int Rank { get; set; }
}
