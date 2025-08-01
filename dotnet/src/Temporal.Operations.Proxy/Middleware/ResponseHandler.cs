using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Http;
using Temporal.Operations.Proxy.Interfaces;
using Temporal.Operations.Proxy.Models;
using Temporal.Operations.Proxy.Services;

namespace Temporal.Operations.Proxy.Middleware;

public class ResponseHandler(
    ILogger<RequestHandler> logger,
    ICodec<MessageContext, byte[]> messageCodec,
    IDescribeTemporalApi temporalApi)
    : IHandleResponse
{
    public async Task<bool> TryHandleAsync(HttpContext context, TemporalContext temporalContext)
    {
        if (!temporalApi.MessageRequiresEncoding(temporalContext.ResponseMessageTypeName))
        {
            return false;
        }
        var direction = CodecDirection.Decode;;
        
        var scoped = messageCodec as IScopedCodec<MessageContext, byte[]>; 
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
                await TransformResponseBody(context, temporalContext);
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
    private async Task TransformResponseBody(
        HttpContext context,
        TemporalContext temporalContext)
    {
        try
        {
            byte[] responseBodyBytes;
            context.Response.Body.Position = 0;

            if (context.Response.Body is MemoryStream memoryStream)
            {
                responseBodyBytes = memoryStream.ToArray();
            }
            else
            {
                // defensively protect against some other stream type 
                // that may have been used upstream before calling this
                using var buffer = new MemoryStream();
                await context.Response.Body.CopyToAsync(buffer);
                responseBodyBytes = buffer.ToArray();
            }

            if (responseBodyBytes.Length == 0)
            {
                logger.LogDebug("Empty response body, skipping transformation");
                return;
            }

            // Decode the response (sans grpc prefix)
            var transformedBytes = await messageCodec.DecodeAsync(
                new MessageContext
                {
                    MessageTypeName = temporalContext.ResponseMessageTypeName,
                    TemporalContext = temporalContext,
                },
                responseBodyBytes[5..]);
            // Write transformed response to the original stream
            var grpcResponseBytes = GrpcUtils.CreateGrpcFrame(transformedBytes);
            context.Response.ContentLength = grpcResponseBytes.Length;
            var oldStream = context.Response.Body;
            // replace the stream with the transformed one
            // however this could be a performance opportunity...by resetting the previous stream
            context.Response.Body = new MemoryStream(grpcResponseBytes);
            context.Response.Body.Position = 0;
            await oldStream.DisposeAsync();

            logger.LogDebug("Response body transformed: {OriginalSize} -> {TransformedSize} bytes",
                responseBodyBytes.Length, transformedBytes.Length);
        }
        catch (Exception ex)
        {
            // fail hard on transform problems
            logger.LogError(ex, "Failed to transform response body");
            throw;
        }
    }
}