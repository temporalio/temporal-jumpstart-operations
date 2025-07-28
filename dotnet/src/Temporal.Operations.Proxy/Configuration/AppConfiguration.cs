using System.ComponentModel.DataAnnotations;

namespace Temporal.Operations.Proxy.Configuration;

public class AppConfiguration
{
    public TemporalApiConfiguration TemporalApi { get; set; } = new();
    public EncodingConfiguration Encoding { get; set; } = new();
    public EncryptionConfiguration Encryption { get; set; } = new();
    public CosmosDbConfiguration CosmosDB { get; set; } = new();
    public ConnectionStringsConfiguration ConnectionStrings { get; set; } = new();
}

public class EncodingConfiguration
{
    public string Strategy { get; set; } = "Default";
}

public class EncryptionConfiguration
{
    public string DefaultKeyId { get; set; } = string.Empty;
    public string KeyIdPrefix { get; set; } = "temporal_payload_";
}

public class CosmosDbConfiguration
{
    public string Endpoint { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "temporal";
}

public class ConnectionStringsConfiguration
{
    public string CosmosDB { get; set; } = string.Empty;
}