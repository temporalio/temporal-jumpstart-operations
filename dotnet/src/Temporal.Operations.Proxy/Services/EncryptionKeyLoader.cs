using Microsoft.AspNetCore.Hosting;
using Temporal.Operations.Proxy.Configuration;
using Temporal.Operations.Proxy.Interfaces;

namespace Temporal.Operations.Proxy.Services;

public class EncryptionKeyLoader(
    IAddEncryptionKey keyAdder,
    IAddKeyId keyIdAdder,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<TemporalApiLoader> logger)
    : IHostedService
{
    private readonly IConfiguration _configuration = configuration;
    private readonly IWebHostEnvironment _environment = environment;
    private readonly ILogger<TemporalApiLoader> _logger = logger;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        keyIdAdder.AddKeyId("default", "enc.default.key");
        keyAdder.AddKey("enc.default.key", "hskydmyftnxdxebeovxsekaewgcczsyp"u8.ToArray());
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;   
    }
}