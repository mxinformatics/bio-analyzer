using Azure;
using Azure.Data.Tables;
using BioAnalyzer.AzureStorage.Contracts;
using BioAnalyzer.Infrastructure.Configuration;

namespace BioAnalyzer.AzureStorage.Contexts;

public class AzureTableContext(IAzureStorageConfiguration storageConfiguration, IAzureCredentialFactory credentialFactory) : ITableContext
{
    public async Task<IList<TEntityType>> GetAllAsync<TEntityType>(string tableName) where TEntityType : class, ITableEntity, new()
    {
        var tableClient = CreateTableClient(tableName);
        var queryResults = tableClient.QueryAsync<TEntityType>(filter: "", maxPerPage: 20);

        var results = new List<TEntityType>();
        await foreach (var result in queryResults.AsPages().ConfigureAwait(false))
        {
            if (result.Values.Count > 0)
            {
                foreach (var entity in result.Values)
                {
                    results.Add(entity);
                }
            }
        }
        return results;
    }

    public async Task<TEntityType?> GetEntityAsync<TEntityType>(string tableName, string partitionKey, string rowKey)
        where TEntityType : class, ITableEntity, new()
    {
        var tableClient = CreateTableClient(tableName);
        try
        {
            var response = await tableClient
                .GetEntityAsync<TEntityType>(partitionKey, rowKey)
                .ConfigureAwait(false);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task UpsertEntityAsync<TEntityType>(string tableName, TEntityType entity)
        where TEntityType : class, ITableEntity, new()
    {
        var tableClient = CreateTableClient(tableName);
        await tableClient.CreateIfNotExistsAsync().ConfigureAwait(false);
        await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace).ConfigureAwait(false);
    }

    public async Task ReplaceEntityAsync<TEntityType>(string tableName, TEntityType entity, ETag ifMatch)
        where TEntityType : class, ITableEntity, new()
    {
        var tableClient = CreateTableClient(tableName);
        await tableClient.CreateIfNotExistsAsync().ConfigureAwait(false);
        await tableClient
            .UpdateEntityAsync(entity, ifMatch, TableUpdateMode.Replace)
            .ConfigureAwait(false);
    }

    public async Task AddEntityAsync<TEntityType>(string tableName, TEntityType entity)
        where TEntityType : class, ITableEntity, new()
    {
        var tableClient = CreateTableClient(tableName);
        await tableClient.CreateIfNotExistsAsync().ConfigureAwait(false);
        await tableClient.AddEntityAsync(entity).ConfigureAwait(false);
    }

    private TableClient CreateTableClient(string tableName)
    {
        var tableUrl = new Uri($"{storageConfiguration.TableStorageUrl}/{tableName}");
        var serviceClient = new TableServiceClient(tableUrl, credentialFactory.Create());
        return serviceClient.GetTableClient(tableName);
    }
}
