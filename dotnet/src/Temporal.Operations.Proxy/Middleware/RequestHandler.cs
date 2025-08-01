using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Http;
using Temporal.Operations.Proxy.Interfaces;
using Temporal.Operations.Proxy.Models;
using Temporal.Operations.Proxy.Services;

namespace Temporal.Operations.Proxy.Middleware;

public class RequestHandler : IHandleRequest
{
    ILogger<RequestHandler> _logger;
    ICodec<MessageContext, byte[]> _messageCodec;
    IDescribeTemporalApi _temporalApi;
    public async Task<bool> TryHandleAsync(HttpContext context, TemporalContext temporalContext)
    {
        if (!_temporalApi.MessageRequiresEncoding(temporalContext.RequestMessageTypeName))
        {
            // No need to transform the request body
            // Don't call lifecycle hooks, either
            return false;
        }
        var direction = CodecDirection.Encode;
        
        var scoped = _messageCodec as IScopedCodec<MessageContext, byte[]>; 
        ExceptionDispatchInfo? initException = null;
        ExceptionDispatchInfo? mainException = null;
        ExceptionDispatchInfo? finishException = null;
        if (scoped != null) {
            try
            {
                await scoped.InitAsync(direction);
            }
            catch (Exception ex)
            {
                initException = ExceptionDispatchInfo.Capture(ex);
            }
        }

        if (initException == null)
        {
            try
            {
                await TransformRequestBody(context, temporalContext);
            }
            catch (Exception ex)
            {
                mainException = ExceptionDispatchInfo.Capture(ex);;
            }
        }

        if (scoped != null)
        {
            try
            {
                await scoped.FinishAsync(direction);
            }
            catch (Exception ex)
            {
                finishException = ExceptionDispatchInfo.Capture(ex);
            }
        }
        var exceptions = new[]
        {
            initException?.SourceException,
            mainException?.SourceException,
            finishException?.SourceException
        }.Where(ex => ex != null).ToList();

        if (exceptions.Count == 1)
        {
            // Preserve original stack trace
            (initException ?? mainException ?? finishException)?.Throw();
        }
        else if (exceptions.Count > 1)
        {
            throw new AggregateException(exceptions!);
        }

        return true;
    }
    private async Task TransformRequestBody(HttpContext context, TemporalContext temporalContext)
    {
        try
        {
            var originalBody = context.Request.Body;

            // Read all bytes from the original stream
            using var memoryStream = new MemoryStream();
            await originalBody.CopyToAsync(memoryStream);
            var requestBodyBytes = memoryStream.ToArray();
            if (requestBodyBytes.Length == 0)
            {
                _logger.LogDebug("Empty request body, skipping transformation");
                return;
            }

            _logger.LogDebug("TransformRequestBody for path: {Path} with {Length} bytes", temporalContext.Path,
                requestBodyBytes.Length);

            // Encode the request (your field-specific logic)
            var transformedBytes = await _messageCodec.EncodeAsync(
                new MessageContext
                {
                    MessageTypeName = temporalContext.RequestMessageTypeName,
                    TemporalContext = temporalContext,
                },
                requestBodyBytes[5..]);

            // Create a new stream with the transformed data
            var transformedStream = new MemoryStream(GrpcUtils.CreateGrpcFrame(transformedBytes));

            // Replace the request body stream
            context.Request.Body = transformedStream;
            context.Request.ContentLength = transformedStream.Length;

            _logger.LogDebug("Request body transformed: {OriginalSize} -> {TransformedSize} bytes",
                requestBodyBytes.Length, transformedBytes.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transform request body");
            throw;
        }

    }
}