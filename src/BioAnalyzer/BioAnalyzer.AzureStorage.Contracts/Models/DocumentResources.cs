namespace BioAnalyzer.AzureStorage.Contracts.Models;

/// <summary>
/// Messages used when throwing an exception
/// </summary>
public static class DocumentResources
{
    public static readonly string DocumentNotFound = "The document with identifier {0} was not found.";
    public static readonly string ContainerNotFound = "The storage container {0} was not found.";
}
