using Google.Protobuf.Reflection;

namespace Temporal.Operations.Proxy.Configuration;

/// <summary>
/// Lightweight lookup table for determining if protobuf fields contain Temporal Payload data
/// Uses standard protobuf FieldDescriptor and MessageDescriptor for all field information
/// </summary>
public class PayloadFieldLookup
{
    // Key: "message.type.name:field_number", Value: FieldDescriptor for direct payload fields
    private readonly Dictionary<string, FieldDescriptor> _payloadFields = new();
    
    // Key: "message.type.name:field_number", Value: FieldDescriptor for fields with nested payload fields
    private readonly Dictionary<string, FieldDescriptor> _nestedPayloadFields = new();
    
    // Key: "message.type.name", Value: MessageDescriptor for message types that contain payload fields
    private readonly Dictionary<string, MessageDescriptor> _messageTypesWithPayloads = new();
   
    /// <summary>
    /// Gets the FieldDescriptor for a direct payload field
    /// </summary>
    public FieldDescriptor? GetPayloadField(string messageTypeName, int fieldNumber)
    {
        return _payloadFields.GetValueOrDefault($"{messageTypeName}:{fieldNumber}");
    }

    /// <summary>
    /// Gets the FieldDescriptor for a field that contains nested payload fields
    /// </summary>
    public FieldDescriptor? GetNestedPayloadField(string messageTypeName, int fieldNumber)
    {
        return _nestedPayloadFields.GetValueOrDefault($"{messageTypeName}:{fieldNumber}");
    }

    /// <summary>
    /// Gets the MessageDescriptor for a message type that contains payload fields
    /// </summary>
    public MessageDescriptor? GetMessageDescriptor(string messageTypeName)
    {
        return _messageTypesWithPayloads.GetValueOrDefault(messageTypeName);
    }

    /// <summary>
    /// Checks if a specific field number in a message type is a direct Payload field
    /// </summary>
    public bool IsPayloadField(string messageTypeName, int fieldNumber)
    {
        return _payloadFields.ContainsKey($"{messageTypeName}:{fieldNumber}");
    }

    /// <summary>
    /// Checks if a field contains nested messages with payload fields
    /// </summary>
    public bool HasNestedPayloadFields(string messageTypeName, int fieldNumber)
    {
        return _nestedPayloadFields.ContainsKey($"{messageTypeName}:{fieldNumber}");
    }

    /// <summary>
    /// Gets the nested message type name for a field using the FieldDescriptor
    /// </summary>
    public string? GetNestedMessageTypeName(string messageTypeName, int fieldNumber)
    {
        var fieldDescriptor = _nestedPayloadFields.GetValueOrDefault($"{messageTypeName}:{fieldNumber}");
        return fieldDescriptor?.MessageType?.FullName;
    }

    /// <summary>
    /// Checks if a message type contains any payload fields (directly or in nested messages)
    /// </summary>
    public bool MessageTypeHasPayloadFields(string messageTypeName)
    {
        return _messageTypesWithPayloads.ContainsKey(messageTypeName);
    }

    /// <summary>
    /// Determines if a field should be traversed for payload transformation
    /// </summary>
    public bool ShouldTransformField(string messageTypeName, int fieldNumber)
    {
        return IsPayloadField(messageTypeName, fieldNumber) || 
               HasNestedPayloadFields(messageTypeName, fieldNumber);
    }

    /// <summary>
    /// Gets all FieldDescriptors for fields that need transformation in a message type
    /// </summary>
    public IEnumerable<FieldDescriptor> GetTransformableFieldDescriptors(string messageTypeName)
    {
        var prefix = $"{messageTypeName}:";
        
        // Direct payload fields
        foreach (var kvp in _payloadFields)
        {
            if (kvp.Key.StartsWith(prefix))
            {
                yield return kvp.Value;
            }
        }
        
        // Fields with nested payload fields
        foreach (var kvp in _nestedPayloadFields)
        {
            if (kvp.Key.StartsWith(prefix))
            {
                yield return kvp.Value;
            }
        }
    }

    /// <summary>
    /// Gets all field numbers that need transformation in a message type
    /// </summary>
    public IEnumerable<int> GetTransformableFieldNumbers(string messageTypeName)
    {
        return GetTransformableFieldDescriptors(messageTypeName)
            .Select(fd => fd.FieldNumber)
            .Distinct();
    }

    // Internal methods for building the lookup table

    public void AddPayloadField(MessageDescriptor messageDescriptor, FieldDescriptor fieldDescriptor)
    {
        var key = $"{messageDescriptor.FullName}:{fieldDescriptor.FieldNumber}";
        _payloadFields[key] = fieldDescriptor;
        _messageTypesWithPayloads[messageDescriptor.FullName] = messageDescriptor;
    }

    public void AddNestedPayloadField(MessageDescriptor messageDescriptor, FieldDescriptor fieldDescriptor)
    {
        var key = $"{messageDescriptor.FullName}:{fieldDescriptor.FieldNumber}";
        _nestedPayloadFields[key] = fieldDescriptor;
        _messageTypesWithPayloads[messageDescriptor.FullName] = messageDescriptor;
    }

    public void MarkMessageTypeWithPayloads(MessageDescriptor messageDescriptor)
    {
        _messageTypesWithPayloads[messageDescriptor.FullName] = messageDescriptor;
    }

    // Debug/diagnostics methods

    public int PayloadFieldCount => _payloadFields.Count;
    public int NestedPayloadFieldCount => _nestedPayloadFields.Count;
    public int MessageTypesWithPayloadsCount => _messageTypesWithPayloads.Count;

    public IEnumerable<FieldDescriptor> GetAllPayloadFields()
    {
        return _payloadFields.Values;
    }

    public IEnumerable<FieldDescriptor> GetAllNestedPayloadFields()
    {
        return _nestedPayloadFields.Values;
    }

    public IEnumerable<MessageDescriptor> GetAllMessageTypesWithPayloads()
    {
        return _messageTypesWithPayloads.Values;
    }

    /// <summary>
    /// Gets diagnostic information about a specific field using FieldDescriptor
    /// </summary>
    public string GetFieldInfo(string messageTypeName, int fieldNumber)
    {
        var payloadField = GetPayloadField(messageTypeName, fieldNumber);
        if (payloadField != null)
        {
            return $"Direct Payload Field: {payloadField.FullName} ({payloadField.FieldType}, {payloadField.FieldNumber})";
        }

        var nestedField = GetNestedPayloadField(messageTypeName, fieldNumber);
        if (nestedField != null)
        {
            return $"Nested Payload Field: {nestedField.FullName} -> {nestedField.MessageType?.FullName} ({nestedField.FieldType}, {nestedField.FieldNumber})";
        }

        return $"Not a payload field: {messageTypeName}:{fieldNumber}";
    }
}