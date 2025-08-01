using Temporal.Operations.Proxy.Models;

namespace Temporal.Operations.Proxy.Interfaces;

public interface IScopedCodec<in TContext, T> : ICodec<TContext, T>
{
    Task InitAsync(CodecDirection direction);
    Task FinishAsync(CodecDirection direction);
}