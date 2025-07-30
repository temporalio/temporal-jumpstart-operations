using Google.Protobuf;
using Google.Protobuf.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Temporal.Operations.Proxy.Interfaces;

namespace Temporal.Operations.Proxy.Configuration;

/// <summary>
/// Implementation of IDescribeTemporalApi that loads and provides lookup tables 
/// for Temporal API payload fields using FileDescriptorSet
/// Optimized for reverse byte traversal during protobuf field transformation
/// </summary>
public class TemporalApiDescriptor : IDescribeTemporalApi
{
    private FileDescriptorSet _temporalDescriptorSet = new();
    private IReadOnlyList<FileDescriptor> _fileDescriptors = new List<FileDescriptor>();
    private PayloadFieldLookup _payloadFieldLookup = new();
    private readonly ILogger<TemporalApiDescriptor> _logger;
    private readonly IOptions<TemporalApiConfiguration> _temporalApiConfiguration;
    private const string PayloadFullName = "temporal.api.common.v1.Payload";
    private const string PayloadsFullName = "temporal.api.common.v1.Payloads";
    private const string SearchAttributesFullName = "temporal.api.common.v1.SearchAttributes";

    public TemporalApiDescriptor(ILogger<TemporalApiDescriptor> logger, IOptions<TemporalApiConfiguration> temporalApiConfiguration)
    {
        _logger = logger;
        _temporalApiConfiguration = temporalApiConfiguration;
    }

    /// <summary>
    /// Gets the payload field lookup table for efficient field transformation decisions
    /// </summary>
    public PayloadFieldLookup PayloadFields => _payloadFieldLookup;

    /// <summary>
    /// Loads Temporal API descriptors from a protobuf descriptor file
    /// </summary>
    public async Task LoadAsync()
    {
        var config = _temporalApiConfiguration.Value;
        try
        {
            // Load the descriptor file
            _temporalDescriptorSet = await LoadTemporalDescriptorsAsync(config.DescriptorFilePath);

            // Build file descriptors
            _fileDescriptors = BuildFileDescriptors(_temporalDescriptorSet);

            // Build payload field lookup
            _payloadFieldLookup = BuildPayloadFieldLookup(_fileDescriptors);
            Print();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load Temporal API descriptors from {config.DescriptorFilePath}", ex);
        }
    }

    public void Print()
    {
        try
        {
            Console.WriteLine("Loading Temporal API descriptors...");

            var descriptor = this;

            var stats = descriptor.GetStats();

            Console.WriteLine($"\nAnalysis Results:");
            Console.WriteLine($"================");
            Console.WriteLine($"Total message types with Payload fields: {stats.MessageTypesWithPayloadsCount}");
            Console.WriteLine($"Total direct Payload fields found: {stats.PayloadFieldCount}");

            Console.WriteLine($"\nMessage Types Containing Payload Fields:");
            Console.WriteLine($"========================================");

            var messageTypes = stats.MessageTypesWithPayloads
                .Select(m => m.FullName)
                .OrderBy(name => name)
                .ToList();

            // Group by service for better organization
            var serviceGroups = messageTypes
                .GroupBy(name => GetServiceName(name))
                .OrderBy(g => g.Key);

            foreach (var serviceGroup in serviceGroups)
            {
                Console.WriteLine($"\n{serviceGroup.Key}:");
                foreach (var messageType in serviceGroup.OrderBy(n => n))
                {
                    Console.WriteLine($"  - {messageType}");
                }
            }

            Console.WriteLine($"\nComplete List (for copy/paste):");
            Console.WriteLine($"===============================");
            foreach (var messageType in messageTypes)
            {
                Console.WriteLine(messageType);
            }

            // Additional analysis: Show which fields contain payloads
            Console.WriteLine($"\nDetailed Field Analysis (first 10 message types):");
            Console.WriteLine($"=================================================");

            foreach (var messageType in stats.MessageTypesWithPayloads.Take(10))
            {
                Console.WriteLine($"\n{messageType.FullName}:");

                var payloadFields = stats.PayloadFields
                    .Where(f => f.ContainingType.FullName == messageType.FullName)
                    .ToList();

                foreach (var field in payloadFields)
                {
                    Console.WriteLine($"  - Field {field.FieldNumber}: {field.Name} -> {field.MessageType?.FullName}");
                }
            }

            if (stats.MessageTypesWithPayloads.Count > 10)
            {
                Console.WriteLine($"\n... and {stats.MessageTypesWithPayloads.Count - 10} more message types");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error analyzing payload fields: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
    /// <summary>
    /// Gets service method information for a specific gRPC method
    /// Returns request/response type names for the method
    /// </summary>
    public ServiceMethodInfo? GetServiceMethodInfo(string serviceMethodName)
    {
        try
        {
            if (serviceMethodName.StartsWith('/'))
            {
                // Path from HTTPContext is reported with leading slash, remove it
                serviceMethodName = serviceMethodName[1..];
            }
            var parts = serviceMethodName.Split('/');
            if (parts.Length != 2) return null;

            var serviceName = parts[0];
            var methodName = parts[1];

            // Use the cached file descriptors
            foreach (var fileDescriptor in _fileDescriptors)
            {
                foreach (var service in fileDescriptor.Services)
                {
                    if (service.FullName == serviceName)
                    {
                        var method = service.FindMethodByName(methodName);
                        if (method != null)
                        {
                            return new ServiceMethodInfo
                            {
                                RequestType = method.InputType.FullName,
                                ResponseType = method.OutputType.FullName
                            };
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get service method info for {serviceMethodName}", serviceMethodName);
        }

        return null;
    }

    /// <summary>
    /// Diagnostic method to get statistics about loaded payload fields
    /// </summary>
    public PayloadFieldStats GetStats()
    {
        return new PayloadFieldStats
        {
            PayloadFieldCount = _payloadFieldLookup.PayloadFieldCount,
            MessageTypesWithPayloadsCount = _payloadFieldLookup.MessageTypesWithPayloadsCount,
            PayloadFields = _payloadFieldLookup.GetAllPayloadFields().ToList(),
            MessageTypesWithPayloads = _payloadFieldLookup.GetAllMessageTypesWithPayloads().ToList()
        };
    }
    /// <summary>
    /// Checks if a message type contains any payload fields that need transformation
    /// </summary>
    public bool MessageRequiresEncoding(string messageTypeName)
    {
        if (!_temporalApiConfiguration.Value.EncodeSearchAttributes && messageTypeName == SearchAttributesFullName)
        {
            return false;
        }
        return PayloadFields.MessageTypeHasPayloadFields(messageTypeName);
    }

    /// <summary>
    /// Loads Temporal API descriptors from the specified descriptor file
    /// </summary>
    private async Task<FileDescriptorSet> LoadTemporalDescriptorsAsync(string descriptorFilePath)
    {
        if (!File.Exists(descriptorFilePath))
        {
            throw new FileNotFoundException(
                $"Temporal API descriptor file not found at {descriptorFilePath}. Please generate temporal-api.pb using 'buf build --as-file-descriptor-set'");
        }

        var descriptorBytes = await File.ReadAllBytesAsync(descriptorFilePath);
        return FileDescriptorSet.Parser.ParseFrom(descriptorBytes);
    }

    /// <summary>
    /// Builds FileDescriptor objects from the loaded FileDescriptorSet using public API
    /// </summary>
    /// <param name="temporalDescriptorSet"></param>
    private IReadOnlyList<FileDescriptor> BuildFileDescriptors(FileDescriptorSet temporalDescriptorSet)
    {
        try
        {
            // Convert FileDescriptorProto objects to properly ordered ByteStrings
            var orderedByteStrings = OrderFileDescriptorsByDependencies(temporalDescriptorSet.File);

            // Build all file descriptors using public API
            return FileDescriptor.BuildFromByteStrings(orderedByteStrings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build file descriptors");
            throw;

        }
    }

    /// <summary>
    /// Builds the payload field lookup table from the loaded descriptors
    /// </summary>
    /// <param name="fileDescriptors"></param>
    private PayloadFieldLookup BuildPayloadFieldLookup(IReadOnlyList<FileDescriptor> fileDescriptors)
    {
        var lookup = new PayloadFieldLookup();

        try
        {
            // Process each file descriptor for payload fields
            foreach (var fileDescriptor in fileDescriptors)
            {
                // Only process Temporal API files
                if (fileDescriptor.Package.StartsWith("temporal.api."))
                {
                    ProcessFileDescriptor(lookup, fileDescriptor);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build payload field lookup");
            throw;

        }

        return lookup;
    }

    /// <summary>
    /// Orders FileDescriptorProto objects by their dependencies and converts to ByteStrings
    /// This ensures that dependencies come before the files that depend on them
    /// </summary>
    private List<ByteString> OrderFileDescriptorsByDependencies(IEnumerable<FileDescriptorProto> fileDescriptorProtos)
    {
        var resolved = new HashSet<string>();
        var orderedList = new List<ByteString>();
        var unorderedList = new List<FileDescriptorProto>(fileDescriptorProtos);

        while (unorderedList.Count > 0)
        {
            // Find a proto where all dependencies are already resolved
            var proto = unorderedList.FirstOrDefault(x => x.Dependency.All(dep => resolved.Contains(dep)));

            if (proto == null)
            {
                // If we can't resolve any more dependencies, we have a circular dependency or missing dependency
                var remainingFiles = string.Join(", ", unorderedList.Select(x => x.Name));
                throw new InvalidOperationException($"Unable to resolve dependencies for remaining proto files: {remainingFiles}");
            }

            // Mark this file as resolved and add it to the ordered list
            resolved.Add(proto.Name);
            unorderedList.Remove(proto);
            orderedList.Add(proto.ToByteString());
        }

        return orderedList;
    }

    /// <summary>
    /// Processes a file descriptor to find all payload fields
    /// </summary>
    private void ProcessFileDescriptor(PayloadFieldLookup lookup, FileDescriptor fileDescriptor)
    {
        // Process all message types in this file
        foreach (var messageType in fileDescriptor.MessageTypes)
        {
            ProcessMessageType(lookup, messageType);
        }

        // Process nested types in message types
        foreach (var messageType in fileDescriptor.MessageTypes)
        {
            ProcessNestedMessageTypes(lookup, messageType);
        }
    }

    /// <summary>
    /// Recursively processes nested message types
    /// </summary>
    private void ProcessNestedMessageTypes(PayloadFieldLookup lookup, MessageDescriptor messageType)
    {
        foreach (var nestedType in messageType.NestedTypes)
        {
            ProcessMessageType(lookup, nestedType);
            ProcessNestedMessageTypes(lookup, nestedType);
        }
    }

    private bool MessageSupportsEncoding(MessageDescriptor messageType)
    {
        return _temporalApiConfiguration.Value.EncodeSearchAttributes || messageType.FullName != SearchAttributesFullName;
    }

    /// <summary>
    /// Processes a single message type to identify payload fields
    /// </summary>
    private void ProcessMessageType(PayloadFieldLookup lookup, MessageDescriptor messageType)
    {
        bool messageHasPayloadFields = false;

        foreach (var field in messageType.Fields.InDeclarationOrder())
        {
            try
            {
                if (IsDirectPayloadField(field) && MessageSupportsEncoding(messageType))
                {
                    // This field directly contains Payload or Payloads
                    lookup.AddPayloadField(messageType, field);
                    messageHasPayloadFields = true;
                }
                else if (IsMessageOrGroupField(field) &&
                         MessageTypeContainsPayloadFields(field.MessageType) && MessageSupportsEncoding(messageType))
                {
                    // This field contains nested messages with payload fields
                    lookup.AddNestedPayloadField(messageType, field);
                    messageHasPayloadFields = true;
                }
            }
            catch (Exception ex)
            {
                // Log specific field that caused the issue for debugging
                _logger.LogError(ex, "Failed to process field {field.FullName} in message {messageType.FullName}", field.FullName, messageType.FullName);
                throw;
            }
        }

        if (messageHasPayloadFields)
        {
            lookup.MarkMessageTypeWithPayloads(messageType);
        }
    }

    /// <summary>
    /// Checks if a field is a message or group type (safe to access MessageType property)
    /// </summary>
    private bool IsMessageOrGroupField(FieldDescriptor field)
    {
        return field.FieldType == FieldType.Message || field.FieldType == FieldType.Group;
    }

    /// <summary>
    /// Checks if a field directly contains Temporal Payload data
    /// </summary>
    private bool IsDirectPayloadField(FieldDescriptor field)
    {
        // Only check MessageType if it's a message or group field
        if (!IsMessageOrGroupField(field))
        {
            return false;
        }

        try
        {
            return field.MessageType?.FullName is PayloadFullName or PayloadsFullName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if field {field.FullName} is a direct payload field", field.FullName);
            return false;
        }
    }

    /// <summary>
    /// Recursively checks if a message type contains payload fields
    /// </summary>
    private bool MessageTypeContainsPayloadFields(MessageDescriptor messageType)
    {
        // Avoid infinite recursion by maintaining a visited set
        var visited = new HashSet<string>();
        return MessageTypeContainsPayloadFieldsRecursive(messageType, visited);
    }

    private bool MessageTypeContainsPayloadFieldsRecursive(MessageDescriptor messageType, HashSet<string> visited)
    {
        if (visited.Contains(messageType.FullName))
        {
            return false;
        }

        visited.Add(messageType.FullName);

        foreach (var field in messageType.Fields.InDeclarationOrder())
        {
            try
            {
                if (IsDirectPayloadField(field))
                {
                    return true;
                }

                if (IsMessageOrGroupField(field) &&
                    MessageTypeContainsPayloadFieldsRecursive(field.MessageType, visited))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check if message type {messageType.FullName} contains payload fields", messageType.FullName);
                throw;
            }
        }

        return false;
    }
    private string GetServiceName(string fullTypeName)
    {
        // Extract service name from full type name
        // e.g., "temporal.api.workflowservice.v1.StartWorkflowExecutionRequest" -> "WorkflowService"
        var parts = fullTypeName.Split('.');
        if (parts.Length >= 4 && parts[0] == "temporal" && parts[1] == "api")
        {
            var servicePart = parts[2];
            // Convert "workflowservice" to "WorkflowService"
            if (servicePart.EndsWith("service"))
            {
                servicePart = servicePart.Substring(0, servicePart.Length - 7); // Remove "service"
                return char.ToUpper(servicePart[0]) + servicePart.Substring(1) + "Service";
            }
            return servicePart;
        }
        return "Other";
    }


}

public class ServiceMethodInfo
{
    public required string RequestType { get; set; }
    public required string ResponseType { get; set; }
}

public class PayloadFieldStats
{
    public int PayloadFieldCount { get; set; }
    public int MessageTypesWithPayloadsCount { get; set; }
    public List<FieldDescriptor> PayloadFields { get; set; } = new();
    public List<MessageDescriptor> MessageTypesWithPayloads { get; set; } = new();
}