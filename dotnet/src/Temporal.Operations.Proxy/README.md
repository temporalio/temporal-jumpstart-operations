# Proxy

#### Troubleshooting

```text
Google.Protobuf.InvalidProtocolBufferException
Protocol message contained an invalid tag (zero).
```

This likely means  you are trying to do some parsing with the grpc prefix (5 bytes)
still in the `byte[]`.