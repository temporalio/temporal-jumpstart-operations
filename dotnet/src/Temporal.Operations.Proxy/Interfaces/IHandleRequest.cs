using Microsoft.AspNetCore.Http;
using Temporal.Operations.Proxy.Models;

namespace Temporal.Operations.Proxy.Interfaces;


public interface IHandleRequest
{
    
    Task<bool> TryHandleAsync(HttpContext context, TemporalContext temporalContext);
    
}