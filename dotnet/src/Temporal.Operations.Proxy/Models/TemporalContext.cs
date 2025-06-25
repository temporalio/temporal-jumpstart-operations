namespace Temporal.Operations.Proxy.Models;

public class TemporalContext
{
    // from `temporal-namespace` header
    public required string Namespace { get; init; } = string.Empty;
    public required string Path { get; init; } = string.Empty;
    public required string RequestMessageTypeName { get; init; } = string.Empty;
    public required string ResponseMessageTypeName { get; init; } = string.Empty;
}