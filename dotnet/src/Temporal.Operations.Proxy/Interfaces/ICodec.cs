namespace Temporal.Operations.Proxy.Interfaces;

public interface ICodec<in TContext, T>
{
    Task<T> EncodeAsync(TContext context, T value);
    Task<T> DecodeAsync(TContext context, T value);
}