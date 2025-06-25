using Temporal.Operations.Proxy.Interfaces;

namespace Temporal.Operations.Proxy.Configuration;

public class TemporalApiLoader(IDescribeTemporalApi describeTemporalApi, ILogger<TemporalApiLoader> logger)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Loading Temporal API descriptor");
        return describeTemporalApi.LoadAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}