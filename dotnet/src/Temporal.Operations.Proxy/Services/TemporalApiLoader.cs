using Microsoft.AspNetCore.Hosting;
using Temporal.Operations.Proxy.Interfaces;

namespace Temporal.Operations.Proxy.Services;

public class TemporalApiLoader : IHostedService
{
    private readonly IDescribeTemporalApi _describeTemporalApi;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<TemporalApiLoader> _logger;

    public TemporalApiLoader(IDescribeTemporalApi describeTemporalApi, IConfiguration configuration, IWebHostEnvironment environment, ILogger<TemporalApiLoader> logger)
    {
        _describeTemporalApi = describeTemporalApi;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var descriptorFilePath = _configuration.GetValue<string>("Protobuf:DescriptorFiles:TemporalApi");
        if (string.IsNullOrEmpty(descriptorFilePath))
        {
            throw new InvalidOperationException("Protobuf:DescriptorFiles:TemporalApi is not set in config");
        }

        if (!Path.IsPathRooted(descriptorFilePath))
        {
            descriptorFilePath = Path.Combine(_environment.ContentRootPath, descriptorFilePath);
        }
        _logger.LogInformation("Loading Temporal API descriptor from {descriptorFilePath}", descriptorFilePath);
        return _describeTemporalApi.LoadAsync(descriptorFilePath);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}