# Proxy

## Usage

If you want to support [Claim Check pattern](https://www.enterpriseintegrationpatterns.com/patterns/messaging/StoreInLibrary.html)
then you can check the [Cosmos](/src/Cosmos) package for a sample implementation.

## WIP

This proxy is a Work in Progress. 

**Improvements needed**
1. Registration of a codec implementation based on Temporal Namespace
2. Support for Async `Encode` and `Decode` calls in the request/response.
3. Support for Before and After hooks for advanced scenarios.

#### Troubleshooting

```text
Google.Protobuf.InvalidProtocolBufferException
Protocol message contained an invalid tag (zero).
```

This likely means  you are trying to do some parsing with the grpc prefix (5 bytes)
still in the `byte[]`.