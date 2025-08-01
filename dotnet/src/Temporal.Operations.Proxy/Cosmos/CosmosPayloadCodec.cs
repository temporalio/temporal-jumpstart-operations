using Google.Protobuf;
using Microsoft.Azure.Cosmos;
using Temporal.Operations.Proxy.Interfaces;
using Temporal.Operations.Proxy.Models;

namespace Temporal.Operations.Proxy.Cosmos;

public class CosmosPayloadCodec : ICodec<PayloadContext, byte[]>, 
    ICodec<PayloadContext, Temporalio.Api.Common.V1.Payload>
{
    private IDataService _dataService;

    public CosmosPayloadCodec(IDataService dataService)
    {
        _dataService = dataService;
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

        await _dataService.CreateItemAsync(cp, cp.temporalNamespace, CosmosContainerName);

        var enc = new Temporalio.Api.Common.V1.Payload();
        var encodingSwapped = false;
        enc.Metadata.Add(CosmosIdMetadataKey, ByteString.CopyFromUtf8(id));
        foreach (var kvp in payload.Metadata)
        {
            if (kvp.Key == EncodingMetadataKey)
            {
                enc.Metadata[EncodingMetadataOriginalKey] = kvp.Value;
                enc.Metadata[EncodingMetadataKey] = EncodingMetadataValueByteString;
                encodingSwapped = true;
            }
            else
            {
                enc.Metadata[kvp.Key] = kvp.Value;
            }
        }

        if (!encodingSwapped)
        {
            enc.Metadata[EncodingMetadataKey] = EncodingMetadataValueByteString;
        }

        enc.Data = ByteString.CopyFromUtf8("who moved my cheese?");
        return enc;
    }

    public async Task<Temporalio.Api.Common.V1.Payload> DecodeAsync(PayloadContext context, Temporalio.Api.Common.V1.Payload payload)
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

        return dec;
    }

    public async Task<byte[]> EncodeAsync(PayloadContext context, byte[] value)
    {
        var payload = Temporalio.Api.Common.V1.Payload.Parser.ParseFrom(value);
        var encoded = await EncodeAsync(context, payload);
        return encoded.ToByteArray();
    }

    public async Task<byte[]> DecodeAsync(PayloadContext context, byte[] value)
    {
        var payload = Temporalio.Api.Common.V1.Payload.Parser.ParseFrom(value);
        var decoded = await DecodeAsync(context, payload);
        return decoded.ToByteArray();
    }
}