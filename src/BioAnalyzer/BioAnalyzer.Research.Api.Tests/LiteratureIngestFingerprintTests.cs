using BioAnalyzer.Research.Api.Domain.Models;
using BioAnalyzer.Research.Api.Domain.Services;

namespace BioAnalyzer.Research.Api.Tests;

public class LiteratureIngestFingerprintTests
{
    [Fact]
    public void BuildRequestFingerprint_IsStableForSamePmcSetRegardlessOfOrder()
    {
        var a = new List<LiteratureIngestItem>
        {
            new() { PmcId = "PMC2" },
            new() { PmcId = "PMC1" }
        };
        var b = new List<LiteratureIngestItem>
        {
            new() { PmcId = "pmc1" },
            new() { PmcId = "PMC2" }
        };

        var fa = LiteratureIngestService.BuildRequestFingerprint("oid:user", "q", a);
        var fb = LiteratureIngestService.BuildRequestFingerprint("oid:user", "q", b);
        Assert.Equal(fa, fb);
    }

    [Fact]
    public void BuildRequestFingerprint_ChangesWhenPmcSetDiffers()
    {
        var a = new List<LiteratureIngestItem> { new() { PmcId = "PMC1" } };
        var b = new List<LiteratureIngestItem> { new() { PmcId = "PMC1" }, new() { PmcId = "PMC2" } };
        var fa = LiteratureIngestService.BuildRequestFingerprint("oid:user", "q", a);
        var fb = LiteratureIngestService.BuildRequestFingerprint("oid:user", "q", b);
        Assert.NotEqual(fa, fb);
    }
}
