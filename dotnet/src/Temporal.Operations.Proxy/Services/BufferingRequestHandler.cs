using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Temporal.Operations.Proxy.Interfaces;
using Temporal.Operations.Proxy.Models;

namespace Temporal.Operations.Proxy.Services;

/// <summary>
/// Example implementation of IHandleRequest that demonstrates request-scoped buffering.
/// This shows how lifecycle hooks can be used to collect data during request processing
/// and then flush it at the end of the request.
/// </summary>
public class BufferingRequestHandler : IHandleRequest
{
    private readonly ILogger<BufferingRequestHandler> _logger;
    private readonly List<string> _buffer = new();
    private DateTime _requestStartTime;

    public BufferingRequestHandler(ILogger<BufferingRequestHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Called at the beginning of request processing
    /// </summary>
    public Task TryHandleAsync(HttpContext context, TemporalContext temporalContext)
    {
        _requestStartTime = DateTime.UtcNow;
        _buffer.Add($"Request started for {temporalContext.Path} at {_requestStartTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
        _buffer.Add($"Namespace: {temporalContext.Namespace}");
        _buffer.Add($"Request Type: {temporalContext.RequestMessageTypeName}");
        
        _logger.LogDebug("Request started: {Path}", temporalContext.Path);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called at the end of request processing
    /// </summary>
    public Task OnRequestEndAsync(HttpContext context, TemporalContext temporalContext)
    {
        var requestEndTime = DateTime.UtcNow;
        var duration = requestEndTime - _requestStartTime;
        
        _buffer.Add($"Request processing completed at {requestEndTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
        _buffer.Add($"Total request processing time: {duration.TotalMilliseconds:F2}ms");
        
        // Flush buffer to log (this could be sent to metrics, saved to database, etc.)
        _logger.LogInformation("Request lifecycle summary for {Path}:\n{Summary}", 
            temporalContext.Path, 
            string.Join("\n", _buffer));
        
        // Clear buffer for next request (each instance is request-scoped)
        _buffer.Clear();
        
        return Task.CompletedTask;
    }
}