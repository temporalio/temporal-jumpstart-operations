namespace Temporal.Operations.Proxy.Configuration;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

// Configuration class with validation attributes
public class TemporalApiConfiguration
{
    [Required(ErrorMessage = "DescriptorFilePath is required")]
    public string DescriptorFilePath { get; init; } = string.Empty;

    public bool EncodeSearchAttributes { get; init; } = false;

    // Custom validation method
    public ValidationResult? Validate()
    {
        if (string.IsNullOrWhiteSpace(DescriptorFilePath))
        {
            return new ValidationResult("DescriptorFilePath cannot be empty");
        }

        return ValidationResult.Success;
    }

    public string GetResolvedDescriptorPath(string contentRootPath)
    {
        if (Path.IsPathRooted(DescriptorFilePath))
        {
            return DescriptorFilePath;
        }

        return Path.Combine(contentRootPath, DescriptorFilePath);
    }
}
