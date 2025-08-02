using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Temporal.Operations.Proxy.Interfaces;
using Temporal.Operations.Proxy.Models;

namespace Temporal.Operations.Proxy.Middleware
{
    public class GrpcProxyMiddleware(
        ILogger<GrpcProxyMiddleware> logger,
        IDescribeTemporalApi temporalApi,
        IHandleRequest requestHandler,
        IHandleResponse responseHandler) : IMiddleware
    {
        private const string TemporalNamespaceHeaderKey = "temporal-namespace";

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (IsGrpcUnaryCall(context.Request))
            {
                await HandleGrpcCall(context, next);
            }
            else
            {
                await next(context);
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

        private TemporalContext? CreateTemporalContext(HttpContext context)
        {
            if (!context.Request.Path.HasValue)
            {
                throw new BadHttpRequestException($"Request path is not set");
            }
            context.Request.Headers.TryGetValue(TemporalNamespaceHeaderKey, out var namespaceHeader);
            if (namespaceHeader.Count == 0)
            {
                logger.LogWarning($"Received gRPC call without {TemporalNamespaceHeaderKey} header: {context.Request.Path}.");
                return null;
            }
        
            var serviceMethod = temporalApi.GetServiceMethodInfo(context.Request.Path.Value);
            if (serviceMethod == null)
            {
                logger.LogWarning($"Received gRPC call for unknown Temporal API service method: {context.Request.Path}.");
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

        private async Task HandleGrpcCall(HttpContext context, RequestDelegate next)
        {
            try
            {
                var temporalContext = CreateTemporalContext(context);
                if (temporalContext == null)
                {
                    // Passthrough
                    await next(context);
                    return;
                }

                logger.LogDebug("Processing Temporal gRPC call for path: {Path}", temporalContext.Path);
                await requestHandler.TryHandleAsync(context, temporalContext);
                using var responseBodyStream = new MemoryStream();
                context.Response.Body = responseBodyStream;
                // Continue to downstream server
                await next(context);
                if (!await responseHandler.TryHandleAsync(context, temporalContext))
                {
                    logger.LogDebug("Completed gRPC call {Path}", temporalContext.Path);
                    return;
                }
                logger.LogDebug("Completed gRPC call {Path}", temporalContext.Path);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing gRPC call for path: {Path}", context.Request.Path);
                throw;
            }
        }
    }
}