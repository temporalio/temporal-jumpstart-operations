using System.Diagnostics;
using System.Security.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Temporal.Operations.Proxy.Configuration;
using Temporal.Operations.Proxy.Cosmos;
using Temporal.Operations.Proxy.Interfaces;
using Temporal.Operations.Proxy.Middleware;
using Temporal.Operations.Proxy.Models;
using Temporal.Operations.Proxy.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for HTTP/2
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5000, listenOptions =>
    {
        //         listenOptions.UseHttps(); // This will use the dev certificate
        // // If you're using HTTPS
        //         options.ConfigureHttpsDefaults(httpsOptions =>
        //         {
        //             httpsOptions.SslProtocols =  SslProtocols.Tls13;
        //             httpsOptions.ClientCertificateMode = ClientCertificateMode.NoCertificate;
        //         });
        listenOptions.Protocols = HttpProtocols.Http2;
    });

    options.Limits.MinRequestBodyDataRate = null;
    options.Limits.MaxRequestBodySize = null;
    options.Limits.MinResponseDataRate = null;  // ← Add this for responses
    options.Limits.MaxResponseBufferSize = null; // ← Add this for responses

    options.Limits.Http2.MaxStreamsPerConnection = 100;
    options.Limits.Http2.InitialConnectionWindowSize = 131072;
    options.Limits.Http2.InitialStreamWindowSize = 98304;
});

// Configure HTTP client for Temporal backend
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        MaxConnectionsPerServer = 1024
    });
});

// Add unified configuration
builder.Services.Configure<AppConfiguration>(builder.Configuration);
builder.Services.AddSingleton<IValidateOptions<TemporalApiConfiguration>, TemporalApiConfigurationValidator>();

// Enable configuration monitoring (reloads when appsettings.json changes)
// builder.Services.AddSingleton<IOptionsMonitor<TemporalApiConfiguration>>();

// Add services to the container
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));


// Register transformation services

builder.Services.AddSingleton<InMemoryTemporalNamespaceKeyIdResolver>(_ => new InMemoryTemporalNamespaceKeyIdResolver());
// interface forwarding
builder.Services.AddSingleton<IResolveKeyId>(p => p.GetRequiredService<InMemoryTemporalNamespaceKeyIdResolver>());
builder.Services.AddSingleton<IAddKeyId>(p => p.GetRequiredService<InMemoryTemporalNamespaceKeyIdResolver>());

builder.Services.AddSingleton<AesByteEncryptor>(_ => new AesByteEncryptor());
// interface forwarding
builder.Services.AddSingleton<IEncrypt>(p => p.GetRequiredService<AesByteEncryptor>());
builder.Services.AddSingleton<IAddEncryptionKey>(p => p.GetRequiredService<AesByteEncryptor>());

// encryption direction
// builder.Services.AddSingleton<ICodec<PayloadContext, byte[]>, CryptPayloadCodec>();
builder.Services.AddSingleton<ICodec<MessageContext, byte[]>, MessageCodec>();

// Register Temporal API descriptor services
builder.Services.AddSingleton<IDescribeTemporalApi, TemporalApiDescriptor>();

// Services that load things at startup
builder.Services.AddHostedService<EncryptionKeyLoader>();
builder.Services.AddHostedService<TemporalApiLoader>();


// Add logging
builder.Services.AddLogging();
builder.Services.AddGrpc();
// // Add gRPC services
// builder.Services.AddGrpcReflection();
// app.MapGrpcReflectionService();
builder.Logging.AddFilter("Microsoft.AspNetCore.Server.Kestrel", LogLevel.Information);
builder.Logging.AddFilter("Yarp.ReverseProxy.Forwarder.HttpForwarder", LogLevel.Error);


// Cosmos configuration
// Add CosmosDB client
builder.Services.AddSingleton<CosmosClient>(serviceProvider =>
{
    var configuration = serviceProvider.GetService<IConfiguration>();
    Debug.Assert(configuration != null, nameof(configuration) + " != null");
    var connectionString = configuration.GetConnectionString("CosmosDB");

    // Alternative: Use account endpoint and key
    // var endpoint = configuration["CosmosDB:Endpoint"];
    // var key = configuration["CosmosDB:Key"];
    // return new CosmosClient(endpoint, key);

    return new CosmosClient(connectionString);
});

builder.Services.AddSingleton<IDataService, DataService>();

// Conditional codec registration based on encoding strategy
builder.Services.AddSingleton<ICodec<PayloadContext, byte[]>>(serviceProvider =>
{
    var appConfig = serviceProvider.GetRequiredService<IOptions<AppConfiguration>>().Value;
    return appConfig.Encoding.Strategy.ToLowerInvariant() switch
    {
        "cosmosdb" => serviceProvider.GetRequiredService<CosmosPayloadCodec>(),
        "default" or _ => serviceProvider.GetRequiredService<CryptPayloadCodec>()
    };
});

// Register both implementations
builder.Services.AddSingleton<CosmosPayloadCodec>();
builder.Services.AddSingleton<CryptPayloadCodec>();

var app = builder.Build();
// Validate configuration on startup
var appConfig = app.Services.GetRequiredService<IOptions<AppConfiguration>>();
try
{
    var config = appConfig.Value; // This will trigger validation
    app.Logger.LogInformation("Configuration validated successfully. Using encoding strategy: {Strategy}",
        config.Encoding.Strategy);
}
catch (OptionsValidationException ex)
{
    app.Logger.LogError("Configuration validation failed: {Errors}",
        string.Join(", ", ex.Failures));
    throw;
}
// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Add custom middleware for gRPC payload transformation
app.UseMiddleware<GrpcProxyMiddleware>();

// Map reverse proxy
app.MapReverseProxy();

app.Run();