namespace BioAnalyzer.Research.Api.Domain.Models;

public class LiteratureAbstractBatchRequest
{
    public IList<string> PmcIds { get; set; } = [];
}

public class LiteratureAbstractBatchResult
{
    public IList<LiteratureAbstractBatchItem> Items { get; set; } = [];

    public IList<LiteratureAbstractBatchItem> Failures { get; set; } = [];
}

public class LiteratureAbstractBatchItem
{
    public string PmcId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}
