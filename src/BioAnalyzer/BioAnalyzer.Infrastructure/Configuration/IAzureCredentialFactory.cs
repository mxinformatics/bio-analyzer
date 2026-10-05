using Azure.Core;

namespace BioAnalyzer.Infrastructure.Configuration;

public interface IAzureCredentialFactory
{
    TokenCredential Create();
}