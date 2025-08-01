using Google.Protobuf;
using Google.Protobuf.Reflection;
using Temporal.Operations.Proxy.Interfaces;
using Temporal.Operations.Proxy.Models;

namespace Temporal.Operations.Proxy.Services;

/// <summary>
/// Fast protobuf message transformer that handles individual Payload transformation
/// within Payloads collections while preserving message structure
/// </summary>
public class MessageCodec(
    IDescribeTemporalApi temporalApiDescriptor,
    ICodec<PayloadContext, byte[]> payloadCodec,
    ILogger<MessageCodec> logger) : IScopedCodec<MessageContext, byte[]>
{
    private readonly IDescribeTemporalApi _temporalApiDescriptor = temporalApiDescriptor ?? throw new ArgumentNullException(nameof(temporalApiDescriptor));
    private readonly ICodec<PayloadContext, byte[]> _payloadCodec = payloadCodec ?? throw new ArgumentNullException(nameof(payloadCodec));
    private readonly ILogger<MessageCodec> _logger = logger;

    /// <summary>
    /// Transforms payload fields in a protobuf message asynchronously
    /// </summary>
    private async Task<byte[]> EncodeDecodeMessageAsync(byte[] messageBytes, string messageTypeName, CodecDirection direction, MessageContext context)
    {
        // Quick check - if this message type has no payload fields, return unchanged
        if (!_temporalApiDescriptor.PayloadFields.MessageTypeHasPayloadFields(messageTypeName))
        {
            return messageBytes;
        }

        using var output = new MemoryStream();
        var pos = 0;

        while (pos < messageBytes.Length)
        {
            // Read tag
            var (tag, tagSize) = ReadVarint32(messageBytes, pos);
            pos += tagSize;

            if (tag == 0) break;

            var fieldNumber = (int)(tag >> 3);
            var wireType = (WireFormat.WireType)(tag & 7);

            // Write the tag to output
            WriteVarint32(output, tag);

            if (wireType == WireFormat.WireType.LengthDelimited &&
                _temporalApiDescriptor.PayloadFields.IsPayloadField(messageTypeName, fieldNumber))
            {
                // This is a payload field - handle Payloads vs single Payload
                var (fieldData, fieldSize) = ReadLengthDelimitedField(messageBytes, pos);
                pos += fieldSize;

                var fieldDescriptor = _temporalApiDescriptor.PayloadFields.GetPayloadField(messageTypeName, fieldNumber);
                var fieldTypeName = fieldDescriptor?.MessageType?.FullName;

                if (fieldTypeName == "temporal.api.common.v1.Payloads")
                {
                    var transformedData = await TransformPayloadsCollectionAsync(fieldData, direction, fieldDescriptor, context);
                    WriteLengthDelimitedField(output, transformedData);
                }
                else if (fieldTypeName == "temporal.api.common.v1.Payload")
                {
                    var transformedData = await TransformSinglePayloadAsync(fieldData, direction, fieldDescriptor, context);
                    WriteLengthDelimitedField(output, transformedData);
                }
                else
                {
                    WriteLengthDelimitedField(output, fieldData);
                }
            }
            else if (wireType == WireFormat.WireType.LengthDelimited &&
                     _temporalApiDescriptor.PayloadFields.HasNestedPayloadFields(messageTypeName, fieldNumber))
            {
                // This field contains nested messages with payload fields
                var (fieldData, fieldSize) = ReadLengthDelimitedField(messageBytes, pos);
                pos += fieldSize;

                var nestedMessageType = _temporalApiDescriptor.PayloadFields.GetNestedMessageTypeName(messageTypeName, fieldNumber);
                if (nestedMessageType != null)
                {
                    // Recursively transform the nested message
                    var transformedData = await EncodeDecodeMessageAsync(fieldData, nestedMessageType, direction, context);
                    WriteLengthDelimitedField(output, transformedData);
                }
                else
                {
                    WriteLengthDelimitedField(output, fieldData);
                }
            }
            else
            {
                // Regular field - copy as-is
                CopyFieldData(messageBytes, pos, output, wireType, out var fieldSize);
                pos += fieldSize;
            }
        }

        return output.ToArray();
    }


    /// <summary>
    /// Transforms a Payloads collection by transforming each individual Payload at byte level asynchronously
    /// More efficient - doesn't parse/serialize the entire Payloads collection
    /// </summary>
    private async Task<byte[]> TransformPayloadsCollectionAsync(byte[] payloadsBytes, CodecDirection direction, FieldDescriptor? fieldDescriptor, MessageContext context)
    {
        using var output = new MemoryStream();
        var pos = 0;

        // Parse through the Payloads message byte by byte
        while (pos < payloadsBytes.Length)
        {
            var (tag, tagSize) = ReadVarint32(payloadsBytes, pos);
            pos += tagSize;

            if (tag == 0) break;

            var fieldNumber = (int)(tag >> 3);
            var wireType = (WireFormat.WireType)(tag & 7);

            // Write the tag to output
            WriteVarint32(output, tag);

            if (wireType == WireFormat.WireType.LengthDelimited && fieldNumber == 1) // payloads field in Payloads message
            {
                // This is an individual Payload within the Payloads collection
                var (payloadBytes, fieldSize) = ReadLengthDelimitedField(payloadsBytes, pos);
                pos += fieldSize;

                // Transform this individual Payload
                var payloadContext = new PayloadContext
                {
                    Field = fieldDescriptor,
                    Namespace = context.TemporalContext.Namespace,
                    FieldPath = $"{fieldDescriptor?.Name ?? "payloads"}[]",
                };

                var transformedPayloadBytes = direction == CodecDirection.Encode
                    ? await _payloadCodec.EncodeAsync(payloadContext, payloadBytes)
                    : await _payloadCodec.DecodeAsync(payloadContext, payloadBytes);

                // Write the transformed Payload back
                WriteLengthDelimitedField(output, transformedPayloadBytes);
            }
            else
            {
                // Other fields in Payloads message - copy as-is
                CopyFieldData(payloadsBytes, pos, output, wireType, out var fieldSize);
                pos += fieldSize;
            }
        }

        var result = output.ToArray();
        return result;
    }

    /// <summary>
    /// Transforms a single Payload asynchronously
    /// </summary>
    private async Task<byte[]> TransformSinglePayloadAsync(byte[] payloadBytes,
        CodecDirection direction,
        FieldDescriptor? fieldDescriptor, MessageContext context)
    {

        var payloadContext = new PayloadContext
        {
            Field = fieldDescriptor,
            Namespace = context.TemporalContext.Namespace,
            FieldPath = fieldDescriptor?.Name ?? "payload",
        };

        return direction == CodecDirection.Encode
            ? await _payloadCodec.EncodeAsync(payloadContext, payloadBytes)
            : await _payloadCodec.DecodeAsync(payloadContext, payloadBytes);
    }

    /// <summary>
    /// Reads a varint32 from the byte array
    /// </summary>
    private static (uint value, int size) ReadVarint32(byte[] data, int pos)
    {
        uint result = 0;
        int shift = 0;
        int size = 0;

        while (pos + size < data.Length)
        {
            var b = data[pos + size];
            size++;

            result |= (uint)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
                return (result, size);

            shift += 7;
            if (shift >= 32)
                throw new InvalidOperationException("Varint32 too long");
        }

        throw new InvalidOperationException("Unexpected end of data reading varint32");
    }

    /// <summary>
    /// Reads a length-delimited field from the byte array
    /// </summary>
    private static (byte[] data, int totalSize) ReadLengthDelimitedField(byte[] messageBytes, int pos)
    {
        var (length, lengthSize) = ReadVarint32(messageBytes, pos);

        if (pos + lengthSize + length > messageBytes.Length)
            throw new InvalidOperationException("Length-delimited field extends beyond message boundary");

        var data = new byte[length];
        Array.Copy(messageBytes, pos + lengthSize, data, 0, (int)length);

        return (data, lengthSize + (int)length);
    }

    /// <summary>
    /// Copies field data based on wire type
    /// </summary>
    private static void CopyFieldData(byte[] messageBytes, int pos, MemoryStream output, WireFormat.WireType wireType, out int fieldSize)
    {
        switch (wireType)
        {
            case WireFormat.WireType.Varint:
                var (varintValue, varintSize) = ReadVarint64(messageBytes, pos);
                WriteVarint64(output, varintValue);
                fieldSize = varintSize;
                break;

            case WireFormat.WireType.Fixed64:
                if (pos + 8 > messageBytes.Length)
                    throw new InvalidOperationException("Fixed64 field extends beyond message boundary");
                output.Write(messageBytes, pos, 8);
                fieldSize = 8;
                break;

            case WireFormat.WireType.LengthDelimited:
                var (fieldData, totalSize) = ReadLengthDelimitedField(messageBytes, pos);
                WriteLengthDelimitedField(output, fieldData);
                fieldSize = totalSize;
                break;

            case WireFormat.WireType.Fixed32:
                if (pos + 4 > messageBytes.Length)
                    throw new InvalidOperationException("Fixed32 field extends beyond message boundary");
                output.Write(messageBytes, pos, 4);
                fieldSize = 4;
                break;

            default:
                throw new NotSupportedException($"Wire type {wireType} is not supported");
        }
    }

    /// <summary>
    /// Reads a varint64 from the byte array
    /// </summary>
    private static (ulong value, int size) ReadVarint64(byte[] data, int pos)
    {
        ulong result = 0;
        int shift = 0;
        int size = 0;

        while (pos + size < data.Length)
        {
            var b = data[pos + size];
            size++;

            result |= (ulong)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
                return (result, size);

            shift += 7;
            if (shift >= 64)
                throw new InvalidOperationException("Varint64 too long");
        }

        throw new InvalidOperationException("Unexpected end of data reading varint64");
    }

    /// <summary>
    /// Writes a varint32 to the output stream
    /// </summary>
    private static void WriteVarint32(MemoryStream output, uint value)
    {
        while (value >= 0x80)
        {
            output.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }
        output.WriteByte((byte)value);
    }

    /// <summary>
    /// Writes a varint64 to the output stream
    /// </summary>
    private static void WriteVarint64(MemoryStream output, ulong value)
    {
        while (value >= 0x80)
        {
            output.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }
        output.WriteByte((byte)value);
    }

    /// <summary>
    /// Writes a length-delimited field to the output stream
    /// </summary>
    private static void WriteLengthDelimitedField(MemoryStream output, byte[] data)
    {
        WriteVarint32(output, (uint)data.Length);
        output.Write(data);
    }

    /// <summary>
    /// Gets all field numbers that need transformation for a message type
    /// </summary>
    public IEnumerable<int> GetTransformableFieldNumbers(string messageTypeName)
    {
        return _temporalApiDescriptor.PayloadFields.GetTransformableFieldNumbers(messageTypeName);
    }

    /// <summary>
    /// Gets all FieldDescriptors that need transformation for a message type
    /// </summary>
    public IEnumerable<FieldDescriptor> GetTransformableFieldDescriptors(string messageTypeName)
    {
        return _temporalApiDescriptor.PayloadFields.GetTransformableFieldDescriptors(messageTypeName);
    }

    public async Task<byte[]> EncodeAsync(MessageContext context, byte[] value)
    {
        return await EncodeDecodeMessageAsync(value, context.MessageTypeName, CodecDirection.Encode, context);
    }

    public async Task<byte[]> DecodeAsync(MessageContext context, byte[] value)
    {
        return await EncodeDecodeMessageAsync(value, context.MessageTypeName, CodecDirection.Decode, context);
    }

    public async Task InitAsync(CodecDirection direction)
    {
        if (_payloadCodec is IScopedCodec<PayloadContext, byte[]> payloadCodec)
        {
            await payloadCodec.InitAsync(direction);
        }
    }

    public async Task FinishAsync(CodecDirection direction)
    {
        if (_payloadCodec is IScopedCodec<PayloadContext, byte[]> payloadCodec)
        {
            await payloadCodec.FinishAsync(direction);
        }
    }
}