using Google.Protobuf;
using Microsoft.Azure.Cosmos;
using Temporal.Operations.Proxy.Interfaces;
using Temporal.Operations.Proxy.Models;
using Temporalio.Api.Common.V1;

namespace Temporal.Operations.Proxy.Cosmos;

struct PayloadProxy
{
    public PayloadContext Context;
    public Payload Payload;
    public TaskCompletionSource<byte[]> Completion;
}
// CosmosPayloadCodec
// Expects to interact with partition keys by namespace
// (do not provide path for value, eg '/myNamespace', just 'myNamespace').
// This claim check pattern buffers writes, flushing in a batch in a transaction.
// For reads, it will delay resolution of Task until all payloads have been enqueued
// so that one call can fetch all the payloads.
public class CosmosPayloadCodec(IDataService dataService) : 
    IScopedCodec<PayloadContext, byte[]>,
    ICodec<PayloadContext, Payload>
{
    private readonly List<CosmosPayload> _encodingBuffer = new();
    private readonly IDictionary<string, PayloadProxy> _pendingDecodes = new Dictionary<string, PayloadProxy>();

    // TODO inject this value from config
    public const string CosmosContainerName = "payloads";
    public const string CosmosIdMetadataKey = "cosmos-id";
    public const string EncodingMetadataOriginalKey = "encoding-original";
    public const string EncodingMetadataKey = "encoding";
    public const string EncodingMetadataValue = "claim/checked";
    private static readonly ByteString EncodingMetadataValueByteString = ByteString.CopyFromUtf8(EncodingMetadataValue);


    public async Task<Payload> EncodeAsync(PayloadContext context, Payload payload)
    {
        // Cosmos requires a unique id for each item
        var id = Guid.NewGuid().ToString();
        var value = payload.Data.ToByteArray();
        var cp = new CosmosPayload
        {
            id = id,
            value = value,
            temporalNamespace = context.Namespace,
            ttl = 60 * 60 * 24 * 180 // 180 days TTL
        };

        // Buffer the payload instead of immediately writing to Cosmos
        _encodingBuffer.Add(cp);

        var replacementPayload = new Payload();
        var encodingSwapped = false;
        replacementPayload.Metadata.Add(CosmosIdMetadataKey, ByteString.CopyFromUtf8(id));
        foreach (var kvp in payload.Metadata)
        {
            if (kvp.Key == EncodingMetadataKey)
            {
                replacementPayload.Metadata[EncodingMetadataOriginalKey] = kvp.Value;
                replacementPayload.Metadata[EncodingMetadataKey] = EncodingMetadataValueByteString;
                encodingSwapped = true;
            }
            else
            {
                replacementPayload.Metadata[kvp.Key] = kvp.Value;
            }
        }

        if (!encodingSwapped)
        {
            replacementPayload.Metadata[EncodingMetadataKey] = EncodingMetadataValueByteString;
        }

        replacementPayload.Data = ByteString.CopyFromUtf8("who moved my cheese?");
        return replacementPayload;
    }

    public async Task<Payload> DecodeAsync(PayloadContext context, Payload payload)
    {
        // Remove encryption metadata and restore original encoding
        if (!payload.Metadata[EncodingMetadataKey].Equals(EncodingMetadataValueByteString))
        {
            return payload;
        }

        if (!payload.Metadata.TryGetValue(CosmosIdMetadataKey, out var idBytes))
        {
            throw new InvalidOperationException($"Missing {CosmosIdMetadataKey} metadata");
        }
        var id = idBytes.ToStringUtf8();
        var cosmosPayload = await dataService.GetItemAsync<CosmosPayload>(id, context.Namespace, CosmosContainerName);

        var restoredPayload = BuildDecodedPayload(payload, cosmosPayload);
        return restoredPayload;
    }

    private async Task<(bool Exists, string? Result)> TryPeekCosmosId(PayloadContext context, Temporalio.Api.Common.V1.Payload payload)
    {
        return payload.Metadata.TryGetValue(CosmosIdMetadataKey, out var idBytes) ? (true, idBytes.ToStringUtf8()) : (false, null);
    } 

    public async Task<byte[]> EncodeAsync(PayloadContext context, byte[] value)
    {
        var payload = Payload.Parser.ParseFrom(value);
        var encoded = await EncodeAsync(context, payload);
        return encoded.ToByteArray();
    }

    public async Task<byte[]> DecodeAsync(PayloadContext context, byte[] value)
    {
        var payload = Payload.Parser.ParseFrom(value);
        var (exists, id) = await TryPeekCosmosId(context, payload);
        if (!exists || id==null)
        {
            throw new InvalidOperationException("Missing id from payload");
        }

        var proxy = new PayloadProxy
        {
            Payload = payload,
            Completion = new TaskCompletionSource<byte[]>(),
            Context = context,
        };

        _pendingDecodes.Add(id, proxy);
        var decoded = await DecodeAsync(context, payload);
        return await proxy.Completion.Task;
    }

    public Task InitAsync(CodecDirection direction)
    {
        // Clear the appropriate buffer
        if (direction == CodecDirection.Encode)
        {
            _encodingBuffer.Clear();
        }
        else if(direction == CodecDirection.Decode)
        {
            _pendingDecodes.Clear();
        }
        return Task.CompletedTask;
    }
    

    public async Task FinishAsync(CodecDirection direction)
    {
        if (direction == CodecDirection.Encode && _encodingBuffer.Count > 0)
        {
            await FlushEncodeBuffer();
        } else if (direction == CodecDirection.Decode && _pendingDecodes.Count > 0)
        {
            await ResolvePendingDecodeBuffer();
        }
        
    }

    private async Task FlushEncodeBuffer()
    {
        // Flush all buffered payloads to Cosmos in a batch
        var grouped = _encodingBuffer.GroupBy(p => p.temporalNamespace);
        foreach (var group in grouped)
        {
            await dataService.CreateBatchAsync(group, group.Key, CosmosContainerName);
        }
        _encodingBuffer.Clear();
    }

    private async Task ResolvePendingDecodeBuffer()
    {
        // Group by namespace since Cosmos batch operations require same partition key
        var groupedByNamespace = _pendingDecodes
            .GroupBy(kvp => kvp.Value.Context.Namespace)
            .ToList();

        foreach (var namespaceGroup in groupedByNamespace)
        {
            var ids = namespaceGroup.Select(kvp => kvp.Key).ToList();
            var cosmosPayloads = await dataService.GetBatchAsync<CosmosPayload>(ids, namespaceGroup.Key, CosmosContainerName);

            // Resolve each pending decode task
            foreach (var kvp in namespaceGroup)
            {
                var cosmosId = kvp.Key;
                var proxy = kvp.Value;

                if (cosmosPayloads.TryGetValue(cosmosId, out var cosmosPayload))
                {
                    // Build the decoded payload using the original payload structure
                    var decodedPayload = BuildDecodedPayload(proxy.Payload, cosmosPayload);
                    var decodedBytes = decodedPayload.ToByteArray();
                    proxy.Completion.SetResult(decodedBytes);
                }
                else
                {
                    // Cosmos item not found - set exception
                    proxy.Completion.SetException(new InvalidOperationException($"Cosmos payload with ID {cosmosId} not found"));
                }
            }
        }

        _pendingDecodes.Clear();
    }

    private Payload BuildDecodedPayload(Payload originalPayload, CosmosPayload cosmosPayload)
    {
        var decodedPayload = new Payload();
        
        // Copy all metadata except Cosmos-specific keys, restore original encoding
        foreach (var kvp in originalPayload.Metadata)
        {
            switch (kvp.Key)
            {
                case EncodingMetadataKey:
                case CosmosIdMetadataKey:
                    // Skip these metadata keys
                    continue;
                case EncodingMetadataOriginalKey:
                    // Restore original encoding
                    decodedPayload.Metadata[EncodingMetadataKey] = originalPayload.Metadata[EncodingMetadataOriginalKey];
                    break;
                default:
                    // Preserve other metadata
                    decodedPayload.Metadata[kvp.Key] = kvp.Value;
                    break;
            }
        }

        // Set the real payload data from Cosmos
        decodedPayload.Data = ByteString.CopyFrom(cosmosPayload.value);
        
        return decodedPayload;
    }
}