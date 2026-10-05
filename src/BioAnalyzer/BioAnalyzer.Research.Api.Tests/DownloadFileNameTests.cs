using BioAnalyzer.Research.Api.Infrastructure;

namespace BioAnalyzer.Research.Api.Tests;

public class DownloadFileNameTests
{
    [Theory]
    [InlineData("PMC123.pdf")]
    [InlineData("PMC123.xml")]
    [InlineData("manual-202601011200000.pdf")]
    [InlineData("a_b-c.d")]
    public void IsValid_AcceptsSafeNames(string name)
    {
        Assert.True(DownloadFileName.IsValid(name, out var normalized, out var error));
        Assert.Equal(name, normalized);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("../etc/passwd")]
    [InlineData("..\\windows")]
    [InlineData("folder/file.pdf")]
    [InlineData("folder\\file.pdf")]
    [InlineData("file name.pdf")]
    [InlineData("file@name.pdf")]
    [InlineData("/abs/path.pdf")]
    public void IsValid_RejectsUnsafeNames(string? name)
    {
        Assert.False(DownloadFileName.IsValid(name, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void IsValid_DecodesUrlEncodedSafeName()
    {
        Assert.True(DownloadFileName.IsValid("PMC123%2Epdf", out var normalized, out _));
        Assert.Equal("PMC123.pdf", normalized);
    }
}
