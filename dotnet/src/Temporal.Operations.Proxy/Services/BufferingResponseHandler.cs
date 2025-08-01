using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Temporal.Operations.Proxy.Interfaces;
using Temporal.Operations.Proxy.Models;

namespace Temporal.Operations.Proxy.Services;

/// <summary>
/// Example implementation of IHandleResponse that demonstrates response-scoped buffering.
/// This shows how lifecycle hooks can be used to collect data during response processing
/// and then flush it at the end of the response.
/// </summary>
public class BufferingResponseHandler : IHandleResponse
{
    private readonly ILogger<BufferingResponseHandler> _logger;
    private readonly List<string> _buffer = new();
    private DateTime _responseStartTime;

    public BufferingResponseHandler(ILogger<BufferingResponseHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Called at the beginning of response processing
    /// </summary>
    public Task OnResponseStartAsync(HttpContext context, TemporalContext temporalContext)
    {
        _responseStartTime = DateTime.UtcNow;
        _buffer.Add($"Response processing started for {temporalContext.Path} at {_responseStartTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
        _buffer.Add($"Response Type: {temporalContext.ResponseMessageTypeName}");
        _buffer.Add($"HTTP Status: {context.Response.StatusCode}");
        
        _logger.LogDebug("Response processing started: {Path}", temporalContext.Path);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called at the end of response processing
    /// </summary>
    public Task OnResponseEndAsync(HttpContext context, TemporalContext temporalContext)
    {
        var responseEndTime = DateTime.UtcNow;
        var duration = responseEndTime - _responseStartTime;
        
        _buffer.Add($"Response processing completed at {responseEndTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
        _buffer.Add($"Total response processing time: {duration.TotalMilliseconds:F2}ms");
        _buffer.Add($"Final HTTP Status: {context.Response.StatusCode}");
        
        // Flush buffer to log (this could be sent to metrics, saved to database, etc.)
        _logger.LogInformation("Response lifecycle summary for {Path}:\n{Summary}", 
            temporalContext.Path, 
            string.Join("\n", _buffer));
        
        // Clear buffer for next request (each instance is request-scoped)
        _buffer.Clear();
        
        return Task.CompletedTask;
    }
}