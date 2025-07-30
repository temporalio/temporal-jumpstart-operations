namespace Temporal.Operations.Proxy.Interfaces;

public interface ICodec<in TContext, T>
{
    T Encode(TContext context, T value);
    T Decode(TContext context, T value);
    Task<T> EncodeAsync(TContext context, T value);
    Task<T> DecodeAsync(TContext context, T value);
}