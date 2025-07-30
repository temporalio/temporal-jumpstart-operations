using Google.Protobuf;
using Temporal.Operations.Proxy.Interfaces;
using Temporal.Operations.Proxy.Models;

namespace Temporal.Operations.Proxy.Services;

/// <summary>
/// CryptPayloadCodec resolves the EncryptionKeyId to use for encryption by Temporal Namespace.
/// It then performs encryption ops with this KeyId using the passed-in IHandleByteEncryption.
/// </summary>
public class CryptPayloadCodec(IEncrypt byteEncryption, IResolveKeyId keyIdResolver)
    : ICodec<PayloadContext, Temporalio.Api.Common.V1.Payload>, ICodec<PayloadContext, byte[]>
{
    public const string EncryptionKeyMetadataKey = "encryption-key-id";
    public const string EncodingMetadataOriginalKey = "encoding-original";
    public const string EncodingMetadataKey = "encoding";
    public const string EncodingMetadataValue = "binary/encrypted";
    private static readonly ByteString EncodingMetadataValueByteString = ByteString.CopyFromUtf8(EncodingMetadataValue);


    public Task<Temporalio.Api.Common.V1.Payload> EncodeAsync(PayloadContext context, Temporalio.Api.Common.V1.Payload payload)
    {
        var keyId = keyIdResolver.ResolveKeyId(context.Namespace);

        var enc = new Temporalio.Api.Common.V1.Payload();
        var encodingSwapped = false;
        enc.Metadata.Add(EncryptionKeyMetadataKey, ByteString.CopyFromUtf8(keyId));
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

        enc.Data = ByteString.CopyFrom(byteEncryption.Encrypt(keyId, payload.Data.ToByteArray()));
        return Task.FromResult(enc);
    }

    public Task<Temporalio.Api.Common.V1.Payload> DecodeAsync(PayloadContext context, Temporalio.Api.Common.V1.Payload payload)
    {
        // Remove encryption metadata and restore original encoding
        if (!payload.Metadata[EncodingMetadataKey].Equals(EncodingMetadataValueByteString))
        {
            return Task.FromResult(payload);
        }

        if (!payload.Metadata.TryGetValue(EncryptionKeyMetadataKey, out var keyIdBytes))
        {
            throw new InvalidOperationException($"Missing {EncryptionKeyMetadataKey} metadata");
        }
        var dec = new Temporalio.Api.Common.V1.Payload();
        foreach (var kvp in payload.Metadata)
        {
            switch (kvp.Key)
            {
                case EncodingMetadataKey:
                case EncryptionKeyMetadataKey:
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

        var keyId = keyIdBytes.ToStringUtf8();
        dec.Data = ByteString.CopyFrom(byteEncryption.Decrypt(keyId, payload.Data.ToByteArray()));

        return Task.FromResult(dec);
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