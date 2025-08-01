using Microsoft.AspNetCore.Http;
using Temporal.Operations.Proxy.Interfaces;
using Temporal.Operations.Proxy.Models;

namespace Temporal.Operations.Proxy.Services;

/// <summary>
/// Request-scoped wrapper around MessageCodec that provides lifecycle hooks for request and response processing.
/// This allows codec implementations to perform buffering, metrics collection, or other operations
/// that span the entire request/response lifecycle.
/// </summary>
public class RequestScopedMessageCodec : ICodec<MessageContext, byte[]>, IHandleRequest, IHandleResponse
{
    private readonly ICodec<MessageContext, byte[]> _messageCodec;
    private readonly IEnumerable<IHandleRequest> _requestHandlers;
    private readonly IEnumerable<IHandleResponse> _responseHandlers;

    public RequestScopedMessageCodec(
        ICodec<MessageContext, byte[]> messageCodec,
        IEnumerable<IHandleRequest> requestHandlers,
        IEnumerable<IHandleResponse> responseHandlers)
    {
        _messageCodec = messageCodec ?? throw new ArgumentNullException(nameof(messageCodec));
        _requestHandlers = requestHandlers ?? throw new ArgumentNullException(nameof(requestHandlers));
        _responseHandlers = responseHandlers ?? throw new ArgumentNullException(nameof(responseHandlers));
    }

    /// <summary>
    /// Encodes a message asynchronously using the underlying MessageCodec
    /// </summary>
    public async Task<byte[]> EncodeAsync(MessageContext context, byte[] value)
    {
        return await _messageCodec.EncodeAsync(context, value);
    }

    /// <summary>
    /// Decodes a message asynchronously using the underlying MessageCodec
    /// </summary>
    public async Task<byte[]> DecodeAsync(MessageContext context, byte[] value)
    {
        return await _messageCodec.DecodeAsync(context, value);
    }

    /// <summary>
    /// Called at the beginning of request processing
    /// </summary>
    public async Task TryHandleAsync(HttpContext context, TemporalContext temporalContext)
    {
        foreach (var handler in _requestHandlers)
        {
            await handler.TryHandleAsync(context, temporalContext);
        }
    }

    /// <summary>
    /// Called at the end of request processing
    /// </summary>
    public async Task OnRequestEndAsync(HttpContext context, TemporalContext temporalContext)
    {
        foreach (var handler in _requestHandlers)
        {
            await handler.OnRequestEndAsync(context, temporalContext);
        }
    }

    /// <summary>
    /// Called at the beginning of response processing
    /// </summary>
    public async Task OnResponseStartAsync(HttpContext context, TemporalContext temporalContext)
    {
        foreach (var handler in _responseHandlers)
        {
            await handler.OnResponseStartAsync(context, temporalContext);
        }
    }

    /// <summary>
    /// Called at the end of response processing
    /// </summary>
    public async Task OnResponseEndAsync(HttpContext context, TemporalContext temporalContext)
    {
        foreach (var handler in _responseHandlers)
        {
            await handler.OnResponseEndAsync(context, temporalContext);
        }
    }
}