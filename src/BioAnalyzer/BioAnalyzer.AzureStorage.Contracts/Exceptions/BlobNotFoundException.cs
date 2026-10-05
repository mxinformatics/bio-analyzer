namespace BioAnalyzer.AzureStorage.Contracts.Exceptions;

/// <summary>
/// Custom exception to throw when a blob is not found.
/// </summary>
public class BlobNotFoundException : System.Exception
{
    public BlobNotFoundException(): base() {}
    public BlobNotFoundException(string message): base(message) {}
    public BlobNotFoundException(string message, System.Exception innerException): base(message, innerException) {}
}