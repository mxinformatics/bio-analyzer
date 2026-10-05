namespace BioAnalyzer.AzureStorage.Contracts;

/// <summary>
/// Contract for a Blob Container.
/// </summary>
public interface IBlobContainer
{
    Task<bool> Exists();
    Task<bool> Exists(CancellationToken cancellationToken);
}