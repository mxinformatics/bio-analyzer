using BioAnalyzer.EventHandlers.Domain.Clients;

namespace BioAnalyzer.EventHandlers.Tests;

public class IngestJobStatusDerivationTests
{
    [Fact]
    public void DeriveJobStatus_AllGraphReady()
    {
        var items = new List<IngestJobStatusClient.IngestItemStatus>
        {
            new() { PmcId = "PMC1", Status = "GraphReady" },
            new() { PmcId = "PMC2", Status = "GraphReady" }
        };
        Assert.Equal("GraphReady", IngestJobStatusClient.DeriveJobStatus(items));
    }

    [Fact]
    public void DeriveJobStatus_PartialWhenMixed()
    {
        var items = new List<IngestJobStatusClient.IngestItemStatus>
        {
            new() { PmcId = "PMC1", Status = "GraphReady" },
            new() { PmcId = "PMC2", Status = "Failed" }
        };
        Assert.Equal("Partial", IngestJobStatusClient.DeriveJobStatus(items));
    }

    [Fact]
    public void DeriveJobStatus_AllFailed()
    {
        var items = new List<IngestJobStatusClient.IngestItemStatus>
        {
            new() { PmcId = "PMC1", Status = "Failed" },
            new() { PmcId = "PMC2", Status = "Skipped" }
        };
        Assert.Equal("Failed", IngestJobStatusClient.DeriveJobStatus(items));
    }
}
