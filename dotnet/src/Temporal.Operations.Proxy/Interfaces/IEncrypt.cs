namespace Temporal.Operations.Proxy.Interfaces;

public interface IEncrypt
{
    byte[] Encrypt(string keyId, byte[] data);
    byte[] Decrypt(string keyId, byte[] data);
}