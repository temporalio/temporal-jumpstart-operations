using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Temporal.Operations.Proxy.Configuration;

namespace Temporal.Operations.Proxy.Tests.Services;

public class TemporalApiDescriptorFixture : IDisposable
{
    public TemporalApiDescriptorFixture()
    {
        var key = "Protobuf:DescriptorFiles:TemporalApi";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
        
        var descriptorPath = Path.Combine(Directory.GetCurrentDirectory(), configuration[key] ?? throw new InvalidOperationException());
        TemporalApiDescriptor = new TemporalApiDescriptor(new Logger<TemporalApiDescriptor>(new LoggerFactory()));
        TemporalApiDescriptor.LoadAsync(descriptorPath).Wait();
    }

    public TemporalApiDescriptor TemporalApiDescriptor { get; private set; }
    public void Dispose()
    {
        
    }
}