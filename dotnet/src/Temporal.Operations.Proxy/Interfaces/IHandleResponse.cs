using Microsoft.AspNetCore.Http;
using Temporal.Operations.Proxy.Models;

namespace Temporal.Operations.Proxy.Interfaces;

/// <summary>
/// Interface for handling response lifecycle events in the gRPC proxy pipeline.
/// Implementations can use these hooks to buffer data, collect metrics, or perform other operations
/// that span the entire response processing lifecycle.
/// </summary>
public interface IHandleResponse  
{
    
    Task<bool> TryHandleAsync(HttpContext context, TemporalContext temporalContext);
}