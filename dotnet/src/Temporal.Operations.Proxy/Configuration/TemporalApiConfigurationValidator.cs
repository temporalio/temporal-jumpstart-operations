using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace Temporal.Operations.Proxy.Configuration;

public class TemporalApiConfigurationValidator(IWebHostEnvironment environment)
    : IValidateOptions<TemporalApiConfiguration>
{
    public ValidateOptionsResult Validate(string? name, TemporalApiConfiguration options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.DescriptorFilePath))
        {
            failures.Add("DescriptorFilePath is required");
        }
        else
        {
            var resolvedPath = options.GetResolvedDescriptorPath(environment.ContentRootPath);
            if (!File.Exists(resolvedPath))
            {
                failures.Add($"Descriptor file not found at: {resolvedPath}");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}