using BioAnalyzer.Research.Api.Infrastructure;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace BioAnalyzer.Research.Api.Tests;

public class ApiAuthenticationTests
{
    [Fact]
    public void ShouldEnforce_True_WhenAzureAdConfigured()
    {
        var apiKey = new ApiAuthenticationOptions { Enabled = true, ApiKey = "" };
        var azureAd = new AzureAdOptions
        {
            TenantId = "tenant",
            ClientId = "api-client"
        };

        Assert.True(ResearchApiAuthentication.ShouldEnforce(apiKey, azureAd, new TestHostEnvironment("Production")));
        Assert.True(ResearchApiAuthentication.ShouldEnforce(apiKey, azureAd, new TestHostEnvironment("Development")));
    }

    [Fact]
    public void ShouldEnforce_True_WhenApiKeyConfigured()
    {
        var apiKey = new ApiAuthenticationOptions { Enabled = true, ApiKey = "secret" };
        var azureAd = new AzureAdOptions();

        Assert.True(ResearchApiAuthentication.ShouldEnforce(apiKey, azureAd, new TestHostEnvironment("Development")));
    }

    [Fact]
    public void ShouldEnforce_False_InDevelopment_WhenNothingConfigured()
    {
        var apiKey = new ApiAuthenticationOptions
        {
            Enabled = true,
            ApiKey = "",
            BypassWhenApiKeyMissingInDevelopment = true
        };
        var azureAd = new AzureAdOptions();

        Assert.False(ResearchApiAuthentication.ShouldEnforce(apiKey, azureAd, new TestHostEnvironment("Development")));
    }

    [Fact]
    public void ApiKeyShouldBeAvailable_False_WhenDisabled()
    {
        var options = new ApiAuthenticationOptions { Enabled = false, ApiKey = "secret" };
        Assert.False(ResearchApiAuthentication.ApiKeyShouldBeAvailable(options, new TestHostEnvironment("Production"), jwtEnabled: true));
    }

    [Fact]
    public void AzureAdOptions_IsConfigured_RequiresTenantAndClient()
    {
        Assert.False(new AzureAdOptions().IsConfigured);
        Assert.True(new AzureAdOptions { TenantId = "t", ClientId = "c" }.IsConfigured);
    }

    [Fact]
    public void ThrowIfInvalid_RejectsEmptyHeaderName()
    {
        var options = new ApiAuthenticationOptions { HeaderName = " " };
        Assert.Throws<InvalidOperationException>(() => options.ThrowIfInvalid());
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
