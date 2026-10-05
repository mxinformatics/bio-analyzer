using BioAnalyzer.Research.Api.Domain.Clients;
using BioAnalyzer.Research.Api.Domain.Models;
using BioAnalyzer.Research.Api.Domain.Services;
using BioAnalyzer.Research.Api.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BioAnalyzer.Research.Api.Tests;

public class LiteratureExpansionServiceTests
{
    private static LiteratureExpansionService CreateSut(
        Mock<ILiteratureSearchService> search,
        Mock<IStorageClient> storage,
        LiteratureExpansionConfiguration? config = null)
    {
        var options = Options.Create(config ?? new LiteratureExpansionConfiguration
        {
            Enabled = true,
            MaxCandidates = 3,
            SearchRetMax = 20,
            MaxConcurrentLinkResolutions = 4,
            RequirePmcId = true,
            RequireOpenAccessLink = true
        });

        return new LiteratureExpansionService(
            search.Object,
            storage.Object,
            options,
            NullLogger<LiteratureExpansionService>.Instance);
    }

    private static Mock<ILiteratureSearchService> CreateSearchMock(
        IEnumerable<string> pubmedIds,
        IList<EntrezSummaryResult> summaries,
        Func<string, LiteratureDownloadLinkResult>? linkFactory = null)
    {
        var search = new Mock<ILiteratureSearchService>(MockBehavior.Strict);
        search
            .Setup(s => s.SearchLiteratureAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntrezSearchResult
            {
                Count = pubmedIds.Count().ToString(),
                IdList = pubmedIds.ToList()
            });

        search
            .Setup(s => s.GetLiteratureSummaries(It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(summaries);

        search
            .Setup(s => s.GetLiteratureDownloadLinkAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string pmcId, CancellationToken _) =>
                linkFactory?.Invoke(pmcId)
                ?? new LiteratureDownloadLinkResult
                {
                    PmcId = pmcId,
                    PdfLink = string.Empty,
                    ArchiveLink = string.Empty
                });

        return search;
    }

    private static Mock<IStorageClient> CreateStorageMock(params LiteratureDownload[] downloads)
    {
        var storage = new Mock<IStorageClient>(MockBehavior.Strict);
        storage
            .Setup(s => s.GetDownloadsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LiteratureDownloadList
            {
                Downloads = downloads.ToList()
            });
        return storage;
    }

    [Fact]
    public async Task FindCandidates_ReturnsOaCandidates_WithPmcAndLinks()
    {
        var summaries = new List<EntrezSummaryResult>
        {
            new() { Uid = "1", Title = "No PMC paper", PmcId = "", Doi = "10.1/a" },
            new() { Uid = "2", Title = "OA paper A", PmcId = "5334499", Doi = "10.1/b" },
            new() { Uid = "3", Title = "OA paper B", PmcId = "PMC10009416", Doi = "10.1/c" },
            new() { Uid = "4", Title = "No link paper", PmcId = "9999999", Doi = "10.1/d" }
        };

        var search = CreateSearchMock(["1", "2", "3", "4"], summaries, pmcId =>
        {
            var normalized = pmcId.StartsWith("PMC", StringComparison.OrdinalIgnoreCase) ? pmcId : $"PMC{pmcId}";
            return normalized switch
            {
                "PMC5334499" => new LiteratureDownloadLinkResult
                {
                    PmcId = "PMC5334499",
                    PdfLink = "https://pmc-oa-opendata.s3.amazonaws.com/PMC5334499.1/PMC5334499.1.pdf",
                    ArchiveLink = "https://pmc-oa-opendata.s3.amazonaws.com/PMC5334499.1/PMC5334499.1.xml"
                },
                "PMC10009416" => new LiteratureDownloadLinkResult
                {
                    PmcId = "PMC10009416",
                    PdfLink = "https://pmc-oa-opendata.s3.amazonaws.com/PMC10009416.1/PMC10009416.1.pdf",
                    ArchiveLink = ""
                },
                _ => new LiteratureDownloadLinkResult
                {
                    PmcId = normalized,
                    PdfLink = "",
                    ArchiveLink = ""
                }
            };
        });

        var storage = CreateStorageMock();
        var sut = CreateSut(search, storage);

        var result = await sut.FindCandidatesAsync(new LiteratureCandidatesRequest
        {
            Query = "sodium ion battery",
            MaxCandidates = 3
        });

        Assert.True(result.Enabled);
        Assert.Equal("sodium ion battery", result.Query);
        Assert.Equal(4, result.SearchHitCount);
        Assert.Equal(4, result.SummariesConsidered);
        Assert.Equal(2, result.Candidates.Count);
        Assert.All(result.Candidates, c =>
        {
            Assert.StartsWith("PMC", c.PmcId, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(c.PdfLink) && string.IsNullOrWhiteSpace(c.XmlLink));
            Assert.False(c.AlreadyIngested);
        });
        Assert.Equal(1, result.Candidates[0].Rank);
        Assert.Equal("PMC5334499", result.Candidates[0].PmcId);
        Assert.Equal("OA paper A", result.Candidates[0].Title);
        Assert.Contains("pdf", result.Candidates[0].PdfLink, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("PMC10009416", result.Candidates[1].PmcId);
    }

    [Fact]
    public async Task FindCandidates_ExcludesNonOa_WhenRequireOpenAccessLink()
    {
        var summaries = new List<EntrezSummaryResult>
        {
            new() { Uid = "1", Title = "Closed", PmcId = "111", Doi = "" },
            new() { Uid = "2", Title = "Open", PmcId = "222", Doi = "" }
        };

        var search = CreateSearchMock(["1", "2"], summaries, pmcId =>
            pmcId is "222" or "PMC222"
                ? new LiteratureDownloadLinkResult
                {
                    PmcId = "PMC222",
                    PdfLink = "https://example.com/a.pdf",
                    ArchiveLink = ""
                }
                : new LiteratureDownloadLinkResult { PmcId = pmcId });

        var sut = CreateSut(search, CreateStorageMock());
        var result = await sut.FindCandidatesAsync(new LiteratureCandidatesRequest { Query = "topic" });

        Assert.Single(result.Candidates);
        Assert.Equal("PMC222", result.Candidates[0].PmcId);
    }

    [Fact]
    public async Task FindCandidates_MarksAlreadyIngested_FromDownloadTable()
    {
        var summaries = new List<EntrezSummaryResult>
        {
            new() { Uid = "1", Title = "Existing", PmcId = "5334499", Doi = "" },
            new() { Uid = "2", Title = "New", PmcId = "10009416", Doi = "" }
        };

        var search = CreateSearchMock(["1", "2"], summaries, pmcId => new LiteratureDownloadLinkResult
        {
            PmcId = pmcId,
            PdfLink = $"https://example.com/{pmcId}.pdf",
            ArchiveLink = ""
        });

        var storage = CreateStorageMock(new LiteratureDownload
        {
            PmcId = "PMC5334499",
            Title = "Existing",
            FileName = "PMC5334499.pdf"
        });

        var sut = CreateSut(search, storage);
        var result = await sut.FindCandidatesAsync(new LiteratureCandidatesRequest { Query = "topic" });

        Assert.Equal(2, result.Candidates.Count);
        var existing = Assert.Single(result.Candidates, c => c.PmcId == "PMC5334499");
        Assert.True(existing.AlreadyIngested);
        var fresh = Assert.Single(result.Candidates, c => c.PmcId == "PMC10009416");
        Assert.False(fresh.AlreadyIngested);
    }

    [Fact]
    public async Task FindCandidates_RespectsMaxCandidatesCap()
    {
        var summaries = Enumerable.Range(1, 5)
            .Select(i => new EntrezSummaryResult
            {
                Uid = i.ToString(),
                Title = $"Paper {i}",
                PmcId = $"{1000 + i}",
                Doi = ""
            })
            .ToList();

        var search = CreateSearchMock(
            summaries.Select(s => s.Uid),
            summaries,
            pmcId => new LiteratureDownloadLinkResult
            {
                PmcId = pmcId,
                PdfLink = $"https://example.com/{pmcId}.pdf"
            });

        var config = new LiteratureExpansionConfiguration
        {
            Enabled = true,
            MaxCandidates = 3,
            SearchRetMax = 20,
            MaxConcurrentLinkResolutions = 4,
            RequirePmcId = true,
            RequireOpenAccessLink = true
        };

        var sut = CreateSut(search, CreateStorageMock(), config);
        var result = await sut.FindCandidatesAsync(new LiteratureCandidatesRequest
        {
            Query = "topic",
            MaxCandidates = 10 // request higher than config; server enforces cap
        });

        Assert.Equal(3, result.Candidates.Count);
        Assert.Equal(new[] { 1, 2, 3 }, result.Candidates.Select(c => c.Rank).ToArray());
    }

    [Fact]
    public async Task FindCandidates_WhenDisabled_ReturnsEmptyWithReason()
    {
        var search = new Mock<ILiteratureSearchService>(MockBehavior.Strict);
        var storage = new Mock<IStorageClient>(MockBehavior.Strict);
        var sut = CreateSut(search, storage, new LiteratureExpansionConfiguration { Enabled = false });

        var result = await sut.FindCandidatesAsync(new LiteratureCandidatesRequest { Query = "topic" });

        Assert.False(result.Enabled);
        Assert.Empty(result.Candidates);
        Assert.Contains(result.SkippedReasons, r => r.Contains("disabled", StringComparison.OrdinalIgnoreCase));
        search.VerifyNoOtherCalls();
        storage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FindCandidates_WhenNoSearchHits_ReturnsReason()
    {
        var search = CreateSearchMock([], []);
        // No summary/link calls expected when empty search — loosen setups by using loose mock for empty path
        search.Reset();
        search
            .Setup(s => s.SearchLiteratureAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntrezSearchResult { Count = "0", IdList = [] });

        var sut = CreateSut(search, CreateStorageMock());
        var result = await sut.FindCandidatesAsync(new LiteratureCandidatesRequest { Query = "zzzz-unlikely" });

        Assert.Empty(result.Candidates);
        Assert.Contains(result.SkippedReasons, r => r.Contains("no PubMed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FindCandidates_WhenNoPmcIds_ReturnsReason()
    {
        var summaries = new List<EntrezSummaryResult>
        {
            new() { Uid = "1", Title = "No PMC", PmcId = "", Doi = "" }
        };
        var search = CreateSearchMock(["1"], summaries);
        var sut = CreateSut(search, CreateStorageMock());

        var result = await sut.FindCandidatesAsync(new LiteratureCandidatesRequest { Query = "topic" });

        Assert.Empty(result.Candidates);
        Assert.Contains(result.SkippedReasons, r => r.Contains("PMCID", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FindCandidates_Throws_OnEmptyQuery()
    {
        var sut = CreateSut(
            new Mock<ILiteratureSearchService>(MockBehavior.Strict),
            new Mock<IStorageClient>(MockBehavior.Strict));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.FindCandidatesAsync(new LiteratureCandidatesRequest { Query = "  " }));
    }

    [Fact]
    public async Task FindCandidates_Continues_WhenDownloadTableFails()
    {
        var summaries = new List<EntrezSummaryResult>
        {
            new() { Uid = "1", Title = "Paper", PmcId = "5334499", Doi = "" }
        };
        var search = CreateSearchMock(["1"], summaries, _ => new LiteratureDownloadLinkResult
        {
            PmcId = "PMC5334499",
            PdfLink = "https://example.com/a.pdf"
        });

        var storage = new Mock<IStorageClient>(MockBehavior.Strict);
        storage
            .Setup(s => s.GetDownloadsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("table unavailable"));

        var sut = CreateSut(search, storage);
        var result = await sut.FindCandidatesAsync(new LiteratureCandidatesRequest { Query = "topic" });

        Assert.Single(result.Candidates);
        Assert.False(result.Candidates[0].AlreadyIngested);
        Assert.Contains(result.SkippedReasons, r => r.Contains("alreadyIngested", StringComparison.OrdinalIgnoreCase));
    }
}
