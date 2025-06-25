using Google.Protobuf.Reflection;

namespace Temporal.Operations.Proxy.Models;

/// <summary>
/// Context information for payload encoding/decoding operations
/// </summary>
public class PayloadContext
{
    /// <summary>
    /// The FieldDescriptor for the field containing this payload
    /// </summary>
    public FieldDescriptor? Field { get; init; }

    /// <summary>
    /// Temporal namespace this payload belongs to
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// Field path within the message (e.g., "input.payloads[0]")
    /// </summary>
    public string FieldPath { get; init; } = string.Empty;

    /// <summary>
    /// Additional context properties that may be useful for encoding decisions
    /// </summary>
    public Dictionary<string, object> Properties { get; init; } = new();
}