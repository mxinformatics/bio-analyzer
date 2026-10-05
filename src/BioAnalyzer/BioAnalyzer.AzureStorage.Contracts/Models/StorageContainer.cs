namespace BioAnalyzer.AzureStorage.Contracts.Models;

/// <summary>
///  Model for a storage container.
/// </summary>
public class StorageContainer(string name)
{
    public string Name { get; } = name;
}