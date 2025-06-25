namespace Temporal.Operations.Proxy.Interfaces;

public interface IAddEncryptionKey
{
    void AddKey(string keyId, byte[] key);   
}

public interface IResolveEncryptionKey
{
    byte[] ResolveKey(string keyId);
}