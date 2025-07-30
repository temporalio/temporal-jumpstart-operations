using Google.Protobuf;
using Temporal.Operations.Proxy.Models;
using Temporal.Operations.Proxy.Services;
using Temporalio.Api.WorkflowService.V1;

namespace Temporal.Operations.Proxy.Tests.Services;

public class CryptPayloadCodecTests
{

    [Fact]
    public void Encode_Decode_GivenEncodingMeta_ShouldSwapOutMetadataAndDataBytes()
    {
        string keyId = Guid.NewGuid().ToString();
        string @namespace = Guid.NewGuid().ToString();
        var metadata = new Dictionary<string, byte[]>
        {
            { "encoding", "text/json"u8.ToArray() },
            { "custom", "special"u8.ToArray() },
        };
        var json = """
                   {
                       "foo":"bar
                   }
                   """;
        var payload = new Temporalio.Api.Common.V1.Payload();
        payload.Metadata["encoding"] = ByteString.CopyFromUtf8("text/json");
        payload.Metadata["custom"] = ByteString.CopyFromUtf8("special");
        payload.Data = ByteString.CopyFromUtf8(json);

        var dataBytes = payload.Data.ToByteArray();

        var keys = new InMemoryTemporalNamespaceKeyIdResolver();
        keys.AddKeyId(@namespace, keyId);

        var encryptor = new EchoEncryptor();
        var sut = new CryptPayloadCodec(
            encryptor,
            keys);

        var encoded = sut.Encode(new PayloadContext
        {
            Namespace = @namespace,
            Field = null,
            FieldPath = ".payload"
        }, payload);
        var encodedBytes = encoded.ToByteArray();
        Assert.NotEmpty(encodedBytes);

        var actual = Temporalio.Api.Common.V1.Payload.Parser.ParseFrom(encodedBytes);

        Assert.Equal(4, actual.Metadata.Count);
        Assert.Equal(CryptPayloadCodec.EncodingMetadataValue, actual.Metadata[CryptPayloadCodec.EncodingMetadataKey].ToStringUtf8());
        Assert.Equal("special", actual.Metadata["custom"].ToStringUtf8());
        Assert.Equal("text/json", actual.Metadata[CryptPayloadCodec.EncodingMetadataOriginalKey].ToStringUtf8());
    }
    [Fact]
    public void SimplePayload_RoundTrip_ShouldWork()
    {
        string keyId = Guid.NewGuid().ToString();
        string @namespace = Guid.NewGuid().ToString();
        // Known good sample
        // data: { "doo": "dah" }
        // metadata: { "encoding": "json/plain" }
        var base64 = "ChYKCGVuY29kaW5nEgpqc29uL3BsYWluEg17ImRvbyI6ImRhaCJ9MgA=";
        var originalPayloadBytes = Convert.FromBase64String(base64);
        var originalPayload = Temporalio.Api.Common.V1.Payload.Parser.ParseFrom(originalPayloadBytes);
        var request = new StartWorkflowExecutionRequest();
        var context = new PayloadContext
        {
            Namespace = @namespace,
            Field = null,
            FieldPath = ".Payload"
        };
        var keys = new InMemoryTemporalNamespaceKeyIdResolver();
        keys.AddKeyId(@namespace, keyId);

        var encryptor = new EchoEncryptor();

        var sut = new CryptPayloadCodec(
            encryptor,
            keys);
        // Encrypt and decrypt
        var encrypted = sut.Encode(context, originalPayloadBytes);
        var decrypted = sut.Decode(context, encrypted);
        var actual = Temporalio.Api.Common.V1.Payload.Parser.ParseFrom(decrypted);
        Assert.Equal(originalPayload.Metadata, actual.Metadata);
        Assert.Equal("{\"doo\":\"dah\"}", actual.Data.ToStringUtf8());

    }
}