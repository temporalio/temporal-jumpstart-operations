# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET 8.0 solution that implements a **gRPC proxy for Temporal workflow operations** with **transparent payload encryption**. The proxy sits between Temporal clients and servers, intercepting and encrypting sensitive data in workflow payloads without requiring client code changes.

## Architecture

### Core Projects
- **`Temporal.Operations.Proxy`** - Main ASP.NET Core service using YARP reverse proxy with custom gRPC middleware
- **`Temporal.Grpc.Proxy`** - Encryption/decryption library for Temporal payloads using protobuf wire format parsing

### Key Technologies
- ASP.NET Core 8.0 with gRPC/HTTP2
- YARP Reverse Proxy for traffic forwarding  
- Google Protobuf for serialization
- Temporal .NET SDK (1.7.0)
- AES-256 encryption for payload security
- xUnit for testing

### Architectural Patterns
- **Middleware Pipeline**: `GrpcProxyMiddleware` intercepts and transforms gRPC requests/responses
- **Strategy Pattern**: `IHandlePayloadEncryption` and `IHandleByteEncryption` interfaces for pluggable encryption
- **Protobuf Wire Format Processing**: Deep protobuf parsing to identify and transform specific payload fields
- **Field Mapping**: `TemporalFieldMaps` configures which message fields contain Temporal payloads

## Development Commands

### Build and Test
```bash
# Build entire solution
dotnet build

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Temporal.Grpc.Proxy.Tests
dotnet test tests/Temporal.Operations.Proxy.Tests

# Run single test
dotnet test tests/Temporal.Grpc.Proxy.Tests --filter "TestName"
```

### Running the Proxy
```bash
# Run main proxy service (runs on https://localhost:5001 and http://localhost:5000)
dotnet run --project src/Temporal.Operations.Proxy

# Run encryption library (console app for testing)
dotnet run --project src/Temporal.Grpc.Proxy
```

### Manual Testing
```bash
# Comprehensive manual testing suite using grpcurl
./src/Temporal.Operations.Proxy/Scripts/test-proxy-manually.sh

# Install test dependencies
./src/Temporal.Operations.Proxy/Scripts/test-proxy-manually.sh install-deps

# Test proxy connectivity only
./src/Temporal.Operations.Proxy/Scripts/test-proxy-manually.sh proxy-only

# Performance testing
./src/Temporal.Operations.Proxy/Scripts/test-proxy-manually.sh performance
```

## Key Implementation Details

### Proxy Configuration
- Configured in `appsettings.json` using YARP reverse proxy settings
- Default upstream: `http://localhost:7233` (standard Temporal server port)
- Supports health checks at `/health` endpoint

### Encryption Flow
1. **Request Path**: Client → Proxy (encrypt payloads) → Temporal Server
2. **Response Path**: Temporal Server → Proxy (decrypt payloads) → Client
3. **Field Targeting**: Only encrypts configured fields (currently `StartWorkflowExecutionRequest` field 5)

### Protobuf Processing
- Skips first 5 bytes of gRPC frames (gRPC framing header)
- Uses `ProtobufWireFormatHelper` for low-level wire format parsing
- `TemporalPayloadHelper` handles Temporal-specific payload structures

### Key Management
- Configurable key IDs with prefix support (`temporal_payload_` default)
- Supports key rotation through `TemporalContextProvider`
- AES-256 encryption via `AesByteDecryption` service

## Current Limitations

- **`TemporalFieldMaps.GetFieldMap()`** throws `NotImplementedException` - field mapping needs completion
- Limited to `StartWorkflowExecutionRequest` message type currently
- Missing `.proto` definition files (uses runtime protobuf parsing)

## Testing Strategy

### Unit Tests
- Payload encryption/decryption round-trips
- Protobuf wire format parsing validation
- Key management and rotation scenarios

### Integration Tests  
- End-to-end gRPC proxy functionality
- Request transformation pipelines
- Metadata handling (simple and nested formats)

### Manual Testing
- Uses `grpcurl` for real gRPC calls
- Tests both direct Temporal connections and proxied connections
- Performance benchmarking capabilities
- Payload size comparison (pre/post encryption)

## Development Notes

- The proxy transparently handles HTTP/2 gRPC traffic
- All transformations are bidirectional (encrypt outbound, decrypt inbound)
- Maintains full compatibility with existing Temporal clients
- Supports multiple encryption keys for different contexts
- Logging includes encryption activity for debugging