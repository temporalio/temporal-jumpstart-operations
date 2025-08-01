using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Temporal.Operations.Proxy.Interfaces;
using Temporal.Operations.Proxy.Models;
using Temporal.Operations.Proxy.Services;

namespace Temporal.Operations.Proxy.Middleware
{
    public class GrpcProxyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ICodec<MessageContext, byte[]> _messageCodec;
        private readonly IHandleRequest _requestHandler;
        private readonly IHandleResponse _responseHandler;
        private readonly IDescribeTemporalApi _temporalApi;
        private readonly ILogger<GrpcProxyMiddleware> _logger;
        private const string TemporalNamespaceHeaderKey = "temporal-namespace";

        public GrpcProxyMiddleware(
            RequestDelegate next,
            ILogger<GrpcProxyMiddleware> logger,
            ICodec<MessageContext, byte[]> messageCodec,
            IDescribeTemporalApi temporalApi,
            IHandleRequest requestHandler,
            IHandleResponse responseHandler)
        {
            _next = next;
            _logger = logger;
            _messageCodec = messageCodec;
            _temporalApi = temporalApi;
            _requestHandler = requestHandler;
            _responseHandler = responseHandler;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (IsGrpcUnaryCall(context.Request))
            {
                await HandleGrpcCall(context);
            }
            else
            {
                await _next(context);
            }
        }
        public bool IsGrpcUnaryCall(HttpRequest request)
        {
            return request.Method == "POST" &&
                   request.ContentType?.StartsWith("application/grpc") == true &&
                   request.Path.HasValue &&
                   request.Path.Value.Contains("/");
        }

        public bool IsGrpcUnaryResponse(HttpResponse response)
        {
            return response.ContentType?.StartsWith("application/grpc") == true;
        }
        private byte[] Combine(byte[] first, byte[] second)
        {
            byte[] result = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, result, 0, first.Length);
            Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
            return result;
        }

        private TemporalContext? CreateTemporalContext(HttpContext context)
        {
            if (!context.Request.Path.HasValue)
            {
                throw new BadHttpRequestException($"Request path is not set");
            }
            context.Request.Headers.TryGetValue(TemporalNamespaceHeaderKey, out var namespaceHeader);
            if (namespaceHeader.Count == 0)
            {
                _logger.LogWarning($"Received gRPC call without {TemporalNamespaceHeaderKey} header: {context.Request.Path}.");
                return null;
            }
        
            var serviceMethod = _temporalApi.GetServiceMethodInfo(context.Request.Path.Value);
            if (serviceMethod == null)
            {
                _logger.LogWarning($"Received gRPC call for unknown Temporal API service method: {context.Request.Path}.");
                return null;
            }

            return new TemporalContext
            {
                Namespace = namespaceHeader.ToString(),
                Path = context.Request.Path.Value,
                RequestMessageTypeName = serviceMethod.RequestType,
                ResponseMessageTypeName = serviceMethod.ResponseType,
            };
        }

        private async Task HandleGrpcCall(HttpContext context)
        {
            try
            {
                var temporalContext = CreateTemporalContext(context);
                if (temporalContext == null)
                {
                    // Passthrough
                    await _next(context);
                    return;
                }

                _logger.LogDebug("Processing Temporal gRPC call for path: {Path}", temporalContext.Path);
                await _requestHandler.TryHandleAsync(context, temporalContext);
                using var responseBodyStream = new MemoryStream();
                context.Response.Body = responseBodyStream;
                // Continue to downstream server
                await _next(context);
                if (!await _responseHandler.TryHandleAsync(context, temporalContext))
                {
                    _logger.LogDebug("Completed gRPC call {Path}", temporalContext.Path);
                    return;
                }
                _logger.LogDebug("Completed gRPC call {Path}", temporalContext.Path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing gRPC call for path: {Path}", context.Request.Path);
                throw;
            }
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
        private async Task TransformResponseBody(
            HttpContext context,
            TemporalContext temporalContext,
            Stream originalResponseStream,
            MemoryStream capturedResponseStream)
        {
            try
            {
                capturedResponseStream.Position = 0;
                var responseBodyBytes = capturedResponseStream.ToArray();

                if (responseBodyBytes.Length == 0)
                {
                    _logger.LogDebug("Empty response body, skipping transformation");
                    return;
                }

                // Decode the response
                var transformedBytes = await _messageCodec.DecodeAsync(
                    new MessageContext
                    {
                        MessageTypeName = temporalContext.ResponseMessageTypeName,
                        TemporalContext = temporalContext,
                    },
                    responseBodyBytes[5..]);
                // Write transformed response to the original stream
                var grpcResponseBytes = GrpcUtils.CreateGrpcFrame(transformedBytes);
                context.Response.ContentLength = grpcResponseBytes.Length; ;
                await originalResponseStream.WriteAsync(grpcResponseBytes);

                _logger.LogDebug("Response body transformed: {OriginalSize} -> {TransformedSize} bytes",
                    responseBodyBytes.Length, transformedBytes.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to transform response body");

                // On error, pass through the original response
                capturedResponseStream.Position = 0;
                await capturedResponseStream.CopyToAsync(originalResponseStream);
            }
            finally
            {
                // Restore original response stream
                context.Response.Body = originalResponseStream;
            }
        }


        private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
        {
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }
    }
}