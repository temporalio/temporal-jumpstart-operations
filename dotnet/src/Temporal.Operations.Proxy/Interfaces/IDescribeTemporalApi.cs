using Temporal.Operations.Proxy.Configuration;

namespace Temporal.Operations.Proxy.Interfaces;

/// <summary>
/// Interface for describing and querying Temporal API protobuf definitions
/// Provides lookup capabilities for payload fields and service methods
/// </summary>
public interface IDescribeTemporalApi
{
    /// <summary>
    /// Loads Temporal API descriptors from a protobuf descriptor file
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// Gets the payload field lookup table for efficient field transformation decisions
    /// </summary>
    PayloadFieldLookup PayloadFields { get; }

    /// <summary>
    /// Gets service method information for a specific gRPC method
    /// Returns request/response type names for the method
    /// </summary>
    /// <param name="serviceMethodName">Service method name in format "ServiceName/MethodName"</param>
    /// <returns>Tuple of (requestType, responseType) or null if not found</returns>
    ServiceMethodInfo? GetServiceMethodInfo(string serviceMethodName);

    /// <summary>
    /// Diagnostic method to get statistics about loaded payload fields
    /// </summary>
    PayloadFieldStats GetStats();
    
    /// <summary>
    /// Checks if a message type contains any payload fields that need transformation
    /// </summary>
    /// <param name="messageTypeName">Full type name of the message</param>
    /// <returns>True if the message needs transformation</returns>
    bool MessageRequiresEncoding(string messageTypeName);
}