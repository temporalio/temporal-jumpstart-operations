using Microsoft.Azure.Cosmos;

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
            new PartitionKey(partitionKey), new ItemRequestOptions{}
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
}