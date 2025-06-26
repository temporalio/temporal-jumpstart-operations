namespace Temporal.Operations.Proxy.Cosmos;

public class CosmosPayload()
{
    public string id { get; set; }
    public byte[] value { get; set; }
    public string temporalNamespace { get; set; }
}