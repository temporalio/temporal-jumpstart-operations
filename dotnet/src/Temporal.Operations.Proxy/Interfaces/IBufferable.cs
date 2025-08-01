using Temporal.Operations.Proxy.Models;

namespace Temporal.Operations.Proxy.Interfaces;

/// <summary>
/// Interface for codec implementations that support buffering operations.
/// This allows codec implementations to collect data in a buffer and then flush it to a third party service.
/// </summary>
/// <typeparam name="TContext">The context type used by the codec</typeparam>
public interface IBufferable<in TContext>
{
    /// <summary>
    /// Begins a new buffering session for the given context.
    /// This initializes any internal buffers or state needed for collecting data.
    /// </summary>
    /// <param name="context">The context for this buffering session</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task BeginBufferAsync(TContext context);

    /// <summary>
    /// Flushes any buffered data to the configured third party service.
    /// This should be called when all data for the current session has been collected.
    /// </summary>
    /// <param name="context">The context for this buffering session</param>
    /// <returns>A task representing the asynchronous flush operation</returns>
    Task FlushAsync(TContext context);
}