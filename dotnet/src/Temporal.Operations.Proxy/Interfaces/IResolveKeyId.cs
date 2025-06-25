namespace Temporal.Operations.Proxy.Interfaces;

public interface IResolveKeyId
{
    string ResolveKeyId(string keyId);
}

public interface IAddKeyId
{
    void AddKeyId(string @namespace, string keyId);
}