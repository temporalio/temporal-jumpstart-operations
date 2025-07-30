using System.Security.Cryptography;
using Temporal.Operations.Proxy.Interfaces;

namespace Temporal.Operations.Proxy.Services;

public class AesByteEncryptor : IEncrypt, IResolveEncryptionKey, IAddEncryptionKey
{
    private readonly IDictionary<string, byte[]> _keys = new Dictionary<string, byte[]>();
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public void AddKey(string keyId, byte[] key)
    {
        _keys.Add(keyId, key);
    }
    public byte[] Encrypt(string keyId, byte[] data)
    {
        var key = ResolveKey(keyId);
        // Our byte array will have a const-length nonce, the encrypted data, and
        // then a const-length tag. In real-world use, one may want to put nonce
        // and/or tag lengths in here.
        var bytes = new byte[NonceSize + TagSize + data.Length];

        // Generate random nonce
        var nonceSpan = bytes.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonceSpan);

        // Perform encryption
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonceSpan, data, bytes.AsSpan(NonceSize, data.Length), bytes.AsSpan(NonceSize + data.Length, TagSize));
        return bytes;
    }

    public byte[] Decrypt(string keyId, byte[] data)
    {
        var key = ResolveKey(keyId);
        var bytes = new byte[data.Length - NonceSize - TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(
            data.AsSpan(0, NonceSize),
            data.AsSpan(NonceSize, bytes.Length),
            data.AsSpan(NonceSize + bytes.Length, TagSize), bytes.AsSpan());
        return bytes;
    }

    public byte[] ResolveKey(string keyId)
    {
        if (!_keys.TryGetValue(keyId, out var key))
        {
            throw new InvalidOperationException($"Key {keyId} not found");
        }
        return key;
    }
}