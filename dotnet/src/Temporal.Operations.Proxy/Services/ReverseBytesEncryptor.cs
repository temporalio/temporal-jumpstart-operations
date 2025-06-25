using Temporal.Operations.Proxy.Interfaces;

namespace Temporal.Operations.Proxy.Services;

public class ReverseBytesEncryptor : IEncrypt
{
    public byte[] Encrypt(string keyId, byte[] data)
    {
        return data.Reverse().ToArray();
    }

    public byte[] Decrypt(string keyId, byte[] data)
    {
        return data.Reverse().ToArray();   
    }
}