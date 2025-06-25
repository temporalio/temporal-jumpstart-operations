namespace Temporal.Operations.Proxy.Interfaces;

public interface ICodec<in TContext, T>
{
    T Encode(TContext context,T value);
    T Decode(TContext context, T value);   
}