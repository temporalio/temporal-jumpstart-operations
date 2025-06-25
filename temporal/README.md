# TemporalService

The `TemporalService` struct provides a simple way to programmatically start and manage a Temporal server with an associated client for production applications.

## Features

- **Automatic Port Selection**: Finds an available port automatically
- **Headless Mode**: Runs the Temporal server without UI by default
- **Integrated Client**: Provides a pre-configured Temporal client
- **Production Ready**: Full Temporal server functionality
- **Clean Lifecycle**: Proper startup and shutdown handling
- **Connection Verification**: Ensures server is ready before returning

## Usage

### Basic Usage

```go
package main

import (
    "fmt"
    "log"
    "temporal-jumpstart-operations/temporal"
)

func main() {
    // Create and start the TemporalService
    temporalService, err := temporal.NewTemporalService()
    if err != nil {
        log.Fatal(err)
    }
    defer temporalService.Stop()

    // Get server information
    fmt.Printf("Temporal server running on: %s\n", temporalService.GetFrontendHostPort())
    
    // Get the client for workflow operations
    client := temporalService.GetClient()
    
    // Use the client for workflow execution...
    // workflowRun, err := client.ExecuteWorkflow(ctx, options, workflow, args...)
}
```

### Production Application Pattern

```go
type App struct {
    temporalService *temporal.TemporalService
}

func (a *App) Start() error {
    // Start Temporal server
    temporalService, err := temporal.NewTemporalService()
    if err != nil {
        return fmt.Errorf("failed to start Temporal: %w", err)
    }
    a.temporalService = temporalService
    
    // Start your application logic
    return nil
}

func (a *App) Stop() error {
    if a.temporalService != nil {
        return a.temporalService.Stop()
    }
    return nil
}
```

## API Reference

### `NewTemporalService() (*TemporalService, error)`
Creates and starts a new Temporal server with a connected client.

**Returns:**
- `*TemporalService`: The TemporalService instance
- `error`: Any error that occurred during startup

### `Stop() error`
Gracefully stops the Temporal server and closes the client connection.

**Returns:**
- `error`: Any error that occurred during shutdown

### `GetClient() client.Client`
Returns the Temporal SDK client connected to the server.

**Returns:**
- `client.Client`: Ready-to-use Temporal client

### `GetDevServer() *testsuite.DevServer`
Returns the underlying Temporal dev server instance.

**Returns:**
- `*testsuite.DevServer`: The server instance

### `GetPort() int`
Returns the port number the server is running on.

**Returns:**
- `int`: Port number

### `GetFrontendHostPort() string`
Returns the frontend host:port string for the server.

**Returns:**
- `string`: Host:port address (e.g., "localhost:7233")

## Implementation Details

- Uses `go.temporal.io/sdk/testsuite.DevServer` for the server implementation
- Automatically selects an available port using `net.Listen`
- Verifies server connectivity before returning from `NewTemporalService()`
- Provides proper cleanup through the `Stop()` method
- Runs in headless mode (no UI) by default
- Uses in-memory SQLite for data persistence during server lifetime

## Production Notes

Despite the "dev" naming, the DevServer is a fully functional Temporal server suitable for production use when you need programmatic lifecycle management. It:

- Runs the complete Temporal server stack
- Supports all Temporal features (workflows, activities, schedules, etc.)
- Handles graceful startup and shutdown
- Manages its own data persistence
- Provides clean resource cleanup

## Dependencies

Required Go modules:
- `go.temporal.io/sdk/client` - Temporal Go SDK client
- `go.temporal.io/sdk/testsuite` - Temporal Go SDK test utilities
- `go.temporal.io/api/workflowservice/v1` - Temporal API definitions

Install with:
```bash
go get go.temporal.io/sdk
```

## Error Handling

All methods return detailed errors. Always check for errors, especially:
- Server startup failures in `NewTemporalService()`
- Connection issues during initialization
- Shutdown errors in `Stop()`

```go
temporalService, err := temporal.NewTemporalService()
if err != nil {
    log.Fatalf("Failed to start Temporal: %v", err)
}
defer func() {
    if err := temporalService.Stop(); err != nil {
        log.Printf("Error stopping Temporal: %v", err)
    }
}()
``` 