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
public class CosmosPayloadCodec : IScopedCodec<PayloadContext, byte[]>, 
    ICodec<PayloadContext, Temporalio.Api.Common.V1.Payload>
{
    private readonly IDataService _dataService;
    private readonly List<CosmosPayload> _encodingBuffer;
    private readonly IDictionary<string, PayloadProxy> _pendingDecodes;
    public CosmosPayloadCodec(IDataService dataService)
    {
        _dataService = dataService;
        _encodingBuffer = new List<CosmosPayload>();
        _pendingDecodes = new Dictionary<string, PayloadProxy>();
    }
    public const string CosmosDatabaseName = "temporal";
    public const string CosmosContainerName = "payloads";
    public const string CosmosPartitionKey = "/temporalNamespace";
    public const string CosmosIdMetadataKey = "cosmos-id";
    public const string EncodingMetadataOriginalKey = "encoding-original";
    public const string EncodingMetadataKey = "encoding";
    public const string EncodingMetadataValue = "claim/checked";
    private static readonly ByteString EncodingMetadataValueByteString = ByteString.CopyFromUtf8(EncodingMetadataValue);


    public async Task<Temporalio.Api.Common.V1.Payload> EncodeAsync(PayloadContext context, Temporalio.Api.Common.V1.Payload payload)
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

        var replacementPayload = new Temporalio.Api.Common.V1.Payload();
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

        var dec = new Temporalio.Api.Common.V1.Payload();
        foreach (var kvp in payload.Metadata)
        {
            switch (kvp.Key)
            {
                case EncodingMetadataKey:
                case CosmosIdMetadataKey:
                    // do not include these metadata keys
                    continue;
                case EncodingMetadataOriginalKey:
                    dec.Metadata[EncodingMetadataKey] = payload.Metadata[EncodingMetadataOriginalKey];
                    break;
                default:
                    // preserve custom metadata
                    dec.Metadata[kvp.Key] = kvp.Value;
                    break;
            }
        }

        var id = idBytes.ToStringUtf8();
       
        var cosmosPayload = await _dataService.GetItemAsync<CosmosPayload>(id, context.Namespace, CosmosContainerName);
        dec.Data = ByteString.CopyFrom(cosmosPayload.value);
        return payload;
    }

    private async Task<(bool Exists, string? Result)> TryPeekCosmosId(PayloadContext context, Temporalio.Api.Common.V1.Payload payload)
    {
        return payload.Metadata.TryGetValue(CosmosIdMetadataKey, out var idBytes) ? (true, idBytes.ToStringUtf8()) : (false, null);
    } 

    public async Task<byte[]> EncodeAsync(PayloadContext context, byte[] value)
    {
        var payload = Temporalio.Api.Common.V1.Payload.Parser.ParseFrom(value);
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
            await _dataService.CreateBatchAsync(group, group.Key, CosmosContainerName);
        }
        _encodingBuffer.Clear();
    }
}