using Temporal.Operations.Proxy.Interfaces;

namespace Temporal.Operations.Proxy.Services;

public class InMemoryTemporalNamespaceKeyIdResolver: IResolveKeyId, IAddKeyId
{
    private readonly Dictionary<string, string> _keyIds = new();

    public void AddKeyId(string @namespace, string keyId)
    {
        _keyIds.Add(@namespace, keyId);   
    }
    public string ResolveKeyId(string keyId)
    {
        return _keyIds[keyId];
    }
}