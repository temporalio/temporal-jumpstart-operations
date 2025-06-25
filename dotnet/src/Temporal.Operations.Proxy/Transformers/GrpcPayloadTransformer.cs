// using Temporal.Operations.Proxy.Interfaces;
// using Yarp.ReverseProxy.Configuration;
// using Yarp.ReverseProxy.Transforms;
// using Yarp.ReverseProxy.Transforms.Builder;
//
// namespace Temporal.Operations.Proxy.Transformers
// {
//     public class GrpcPayloadTransformer : ITransformProvider
//     {
//         private readonly ILogger<GrpcPayloadTransformer> _logger;
//         private readonly IMessageTransformer _messageTransformer;
//
//         public GrpcPayloadTransformer(ILogger<GrpcPayloadTransformer> logger, 
//             IMessageTransformer messageTransformer)
//         {
//             _logger = logger;
//             _messageTransformer = messageTransformer;
//         }
//
//         public void ValidateRoute(RouteConfig routeConfig) { }
//         public void ValidateCluster(ClusterConfig clusterConfig) { }
//
//         public void ValidateRoute(TransformRouteValidationContext context)
//         {
//             
//             // TODO implement
//         }
//
//         public void ValidateCluster(TransformClusterValidationContext context)
//         {
//             // TODO implement
//         }
//
//         public void Apply(TransformBuilderContext context)
//         {
//             // Add request transformation
//             // context.AddRequestTransform(async transformContext =>
//             // {
//             //     if (_messageTypeResolver.IsGrpcUnaryCall(transformContext.HttpContext.Request))
//             //     {
//             //         await TransformRequest(transformContext);
//             //     }
//             // });
//             //
//             // // Add response transformation
//             // context.AddResponseTransform(async transformContext =>
//             // {
//             //     if (_messageTypeResolver.IsGrpcUnaryResponse(transformContext.HttpContext.Response))
//             //     {
//             //         await TransformResponse(transformContext);
//             //     }
//             // });
//         }
//
//         private async Task TransformRequest(RequestTransformContext context)
//         {
//             try
//             {
//                 _logger.LogDebug("Transforming gRPC request for path: {Path}", context.HttpContext.Request.Path);
//
//                 var originalBody = context.HttpContext.Request.Body;
//                 using var memoryStream = new MemoryStream();
//                 await originalBody.CopyToAsync(memoryStream);
//                 var requestBytes = memoryStream.ToArray();
//
//                 if (requestBytes.Length == 0)
//                 {
//                     _logger.LogDebug("Empty request body, skipping transformation");
//                     return;
//                 }
//                 var transformedBytes = await _messageTransformer.TransformMessage(
//                     requestBytes,
//                     "REPLACE WITH MESSAGE TYPE NAME",
//                     PayloadDirection.Request,
//                     "REPLACE WITH NAMESPACE");
//                 // var transformedBytes = await _payloadProcessor.ProcessRequest(requestBytes, context.HttpContext);
//
//                 // Replace the request body with transformed content
//                 context.HttpContext.Request.Body = new MemoryStream(transformedBytes);
//                 context.HttpContext.Request.ContentLength = transformedBytes.Length;
//
//                 _logger.LogDebug("Request transformation completed. Original: {OriginalSize} bytes, Transformed: {TransformedSize} bytes",
//                     requestBytes.Length, transformedBytes.Length);
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogError(ex, "Failed to transform gRPC request for path: {Path}", context.HttpContext.Request.Path);
//                 throw;
//             }
//         }
//
//         private async Task TransformResponse(ResponseTransformContext context)
//         {
//             try
//             {
//                 _logger.LogDebug("Transforming gRPC response for path: {Path}", context.HttpContext.Request.Path);
//
//                 // Read the response body
//                 var originalBody = context.HttpContext.Response.Body;
//                 using var responseStream = new MemoryStream();
//                 
//                 // Temporarily replace the response body to capture the content
//                 context.HttpContext.Response.Body = responseStream;
//                 
//                 // The response has already been written by the downstream server
//                 // We need to handle this in the middleware instead
//                 _logger.LogDebug("Response transformation setup completed");
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogError(ex, "Failed to transform gRPC response for path: {Path}", context.HttpContext.Request.Path);
//                 throw;
//             }
//         }
//     }
// }