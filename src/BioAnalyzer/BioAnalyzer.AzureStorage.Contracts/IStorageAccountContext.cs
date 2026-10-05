using BioAnalyzer.AzureStorage.Contracts.Models;

namespace BioAnalyzer.AzureStorage.Contracts;

/// <summary>
/// Contracts for interacting with storage accounts
/// </summary>
public interface IStorageAccountContext
{
    public Task<IBlobContainer> CreateContainer(StorageContainer storageContainer);
}