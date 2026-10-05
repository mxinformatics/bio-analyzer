using Azure;
using Azure.Data.Tables;

namespace BioAnalyzer.AzureStorage.Contracts;

public interface ITableContext
{
    Task<IList<TEntityType>> GetAllAsync<TEntityType>(string tableName)
        where TEntityType : class, ITableEntity, new();

    Task<TEntityType?> GetEntityAsync<TEntityType>(string tableName, string partitionKey, string rowKey)
        where TEntityType : class, ITableEntity, new();

    Task UpsertEntityAsync<TEntityType>(string tableName, TEntityType entity)
        where TEntityType : class, ITableEntity, new();

    /// <summary>
    /// Conditional replace using If-Match ETag. Throws RequestFailedException (412) on conflict.
    /// </summary>
    Task ReplaceEntityAsync<TEntityType>(string tableName, TEntityType entity, ETag ifMatch)
        where TEntityType : class, ITableEntity, new();

    /// <summary>
    /// Insert-only. Throws RequestFailedException (409) if the entity already exists.
    /// </summary>
    Task AddEntityAsync<TEntityType>(string tableName, TEntityType entity)
        where TEntityType : class, ITableEntity, new();
}
