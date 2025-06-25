using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Temporal.Operations.Proxy.Configuration;

namespace Temporal.Operations.Proxy.Tests.Services;

public class TemporalApiDescriptorFixture : IDisposable
{
    
    public TemporalApiDescriptorFixture()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
        var config = new TemporalApiConfiguration();
        configuration.GetSection("TemporalApi").Bind(config);
        
        TemporalApiDescriptor = new TemporalApiDescriptor(
            new Logger<TemporalApiDescriptor>(new LoggerFactory()), new OptionsWrapper<TemporalApiConfiguration>(config));
        TemporalApiDescriptor.LoadAsync().Wait();
    }

    public TemporalApiDescriptor TemporalApiDescriptor { get; private set; }
    public void Dispose()
    {
        
    }

}