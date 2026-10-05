using BioAnalyzer.Research.Api.Domain.Clients;

namespace BioAnalyzer.Research.Api.Tests;

public class NcbiPmcIdNormalizationTests
{
    [Theory]
    [InlineData("PMC13901", "13901")]
    [InlineData("pmc13901", "13901")]
    [InlineData("13901", "13901")]
    [InlineData("PMC5334499.1", "5334499")]
    [InlineData("PMC123.pdf", "123")]
    public void ToOaiArticleId_StripsPrefixVersionAndExtension(string input, string expected)
    {
        Assert.Equal(expected, NcbiClient.ToOaiArticleId(input));
    }
}
