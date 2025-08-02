using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace Temporal.Operations.Proxy.Cosmos;

public class DataService : IDataService
{
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseName;

    public DataService(CosmosClient cosmosClient, IConfiguration configuration)
    {
        _cosmosClient = cosmosClient;
        _databaseName = configuration["CosmosDB:DatabaseName"] ?? "DefaultDatabase";
    }

    public async Task<T> GetItemAsync<T>(string id, string partitionKey, string containerName)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, containerName);
            var response = await container.ReadItemAsync<T>(id, new PartitionKey(partitionKey));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return default(T);
        }
    }

    public async Task<T> CreateItemAsync<T>(T item, string partitionKey, string containerName)
    {
        var container = _cosmosClient.GetContainer(_databaseName, containerName);
        var response = await container.CreateItemAsync(
            item,
            new PartitionKey(partitionKey), new ItemRequestOptions { }
            );
        return response.Resource;
    }

    public async Task<T> UpdateItemAsync<T>(string id, T item, string partitionKey, string containerName)
    {
        var container = _cosmosClient.GetContainer(_databaseName, containerName);
        var response = await container.ReplaceItemAsync(item, id, new PartitionKey(partitionKey));
        return response.Resource;
    }

    public async Task DeleteItemAsync(string id, string partitionKey, string containerName)
    {
        var container = _cosmosClient.GetContainer(_databaseName, containerName);
        await container.DeleteItemAsync<object>(id, new PartitionKey(partitionKey));
    }

    public async Task<IEnumerable<T>> QueryItemsAsync<T>(string query, string containerName)
    {
        var container = _cosmosClient.GetContainer(_databaseName, containerName);
        var queryDefinition = new QueryDefinition(query);

        var results = new List<T>();
        using var feedIterator = container.GetItemQueryIterator<T>(queryDefinition);

        while (feedIterator.HasMoreResults)
        {
            var response = await feedIterator.ReadNextAsync();
            results.AddRange(response.ToList());
        }

        return results;
    }

    public async Task CreateBatchAsync<T>(IEnumerable<T> items, string partitionKey, string containerName)
    {
        var container = _cosmosClient.GetContainer(_databaseName, containerName);
        var batch = container.CreateTransactionalBatch(new PartitionKey(partitionKey));
        
        foreach (var item in items)
        {
            batch.CreateItem(item);
        }
        
        var response = await batch.ExecuteAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            throw new CosmosException($"Batch operation failed with status code: {response.StatusCode}", response.StatusCode, 0, response.ActivityId, response.RequestCharge);
        }
    }

    public async Task<Dictionary<string, T>> GetBatchAsync<T>(IEnumerable<string> ids, string partitionKey, string containerName)
    {
        var container = _cosmosClient.GetContainer(_databaseName, containerName);
        var batch = container.CreateTransactionalBatch(new PartitionKey(partitionKey));
        
        foreach (var id in ids)
        {
            batch.ReadItem(id);
        }
        
        var response = await batch.ExecuteAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            throw new CosmosException($"Batch read operation failed with status code: {response.StatusCode}", response.StatusCode, 0, response.ActivityId, response.RequestCharge);
        }

        var result = new Dictionary<string, T>();
        var idList = ids.ToList();
        
        for (int i = 0; i < response.Count; i++)
        {
            var batchResponse = response[i];
            if (batchResponse.IsSuccessStatusCode)
            {
                var item = batchResponse.Resource<T>();
                result[idList[i]] = item;
            }
            // Note: Missing items are not included in the result dictionary
        }
        
        return result;
    }
}