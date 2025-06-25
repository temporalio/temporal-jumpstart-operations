using Temporal.Operations.Proxy.Interfaces;

namespace Temporal.Operations.Proxy.Services;

public class EchoEncryptor : IEncrypt
{
    public byte[] Encrypt(string keyId, byte[] data)
    {
        return data;
    }

    public byte[] Decrypt(string keyId, byte[] data)
    {
        return data;
    }
}