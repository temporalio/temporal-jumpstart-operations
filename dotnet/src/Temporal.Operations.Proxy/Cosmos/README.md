# Cosmos

The Cosmos package contains an implementation of the [Claim Check Pattern](https://www.enterpriseintegrationpatterns.com/patterns/messaging/StoreInLibrary.html), typically
preferred for keeping business data in a customer's environment.
That is, the actual business data is only referenced in the Temporal Service storage by an identifier.

The storage for the payloads here is [CosmosDB](https://azure.microsoft.com/en-us/products/cosmos-db) so 
implements the `ICodec<PayloadContext, byte[]>` interface to store and restore the payloads by id.

**NOTE**: This implementation needs improvements:

1. The `Encode` and `Decode` interfaces need an `xAsync` equivalent to properly use the Data Service (non-blocking).
2. Ideally, we could make a single request to CosmosDB with the payloads instead of storing or restoring the payloads.
   1. This likely means some kind of `OnStartRequest` and `OnEndResponse` type lifecycle for the proxy.

