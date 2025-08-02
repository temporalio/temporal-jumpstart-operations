namespace Temporal.Operations.Proxy.Cosmos;

public interface IDataService
{
    Task<T> GetItemAsync<T>(string id, string partitionKey, string containerName);
    Task<T> CreateItemAsync<T>(T item, string partitionKey, string containerName);
    Task<T> UpdateItemAsync<T>(string id, T item, string partitionKey, string containerName);
    Task DeleteItemAsync(string id, string partitionKey, string containerName);
    Task<IEnumerable<T>> QueryItemsAsync<T>(string query, string containerName);
    Task CreateBatchAsync<T>(IEnumerable<T> items, string partitionKey, string containerName);
    Task<Dictionary<string, T>> GetBatchAsync<T>(IEnumerable<string> ids, string partitionKey, string containerName);
}