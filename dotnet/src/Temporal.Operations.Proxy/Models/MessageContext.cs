namespace Temporal.Operations.Proxy.Models;

public class MessageContext
{
    public required TemporalContext TemporalContext { get; init; } 
    public required string MessageTypeName { get; init; } 
    
}