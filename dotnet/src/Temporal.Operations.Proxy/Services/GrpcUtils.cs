using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Temporal.Operations.Proxy.Services;

public class GrpcUtils
{
    public static byte[] CreateGrpcFrame(byte[] messageBytes, bool compressed = false)
    {
        var frame = new byte[5 + messageBytes.Length];
        
        // Byte 0: Compression flag (0 = not compressed, 1 = compressed)
        frame[0] = compressed ? (byte)1 : (byte)0;
        
        // Bytes 1-4: Message length in big-endian format
        var lengthBytes = BitConverter.GetBytes((uint)messageBytes.Length);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(lengthBytes);
        }
        Array.Copy(lengthBytes, 0, frame, 1, 4);
        
        // Bytes 5+: The actual protobuf message
        Array.Copy(messageBytes, 0, frame, 5, messageBytes.Length);
        
        return frame;
    }
    // Helper to create the complete HTTP/2 request that would hit your proxy
    public static HttpContext CreateGrpcHttpContext(string methodPath, byte[] body) 
    {   
        var ctx = new DefaultHttpContext();
        
        var headers =new Dictionary<string, string>
        {
            [":method"] = "POST",
            [":path"] = methodPath,
            [":authority"] = "localhost:7233",
            [":scheme"] = "https",
            ["content-type"] = "application/grpc",
            ["grpc-encoding"] = "identity",
            ["grpc-accept-encoding"] = "identity,deflate,gzip",
            ["te"] = "trailers",
            ["user-agent"] = "grpc-csharp/2.57.0",
            ["grpc-timeout"] = "30S"
        };

        foreach (var header in headers)
        {
            ctx.HttpContext.Request.Headers.Add(new KeyValuePair<string, StringValues>(header.Key, new StringValues(header.Value)));
        }

        return ctx;
    }
}