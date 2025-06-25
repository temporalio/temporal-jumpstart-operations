package temporal

import (
	"context"
	"fmt"
	"time"

	"go.temporal.io/api/workflowservice/v1"
	"go.temporal.io/sdk/client"
	"go.temporal.io/sdk/testsuite"
)

// TemporalService manages a Temporal dev server and client
type TemporalService struct {
	devServer *testsuite.DevServer
	client    client.Client
	port      int
}

// NewTemporalService creates and starts a new Temporal dev server with client
func NewTemporalService() (*TemporalService, error) {
	// Pick an available port
	port, err := getAvailablePort()
	if err != nil {
		return nil, fmt.Errorf("failed to find available port: %w", err)
	}

	// Create and start Temporal dev server
	devServer, err := testsuite.StartDevServer(context.Background(), testsuite.DevServerOptions{
		ClientOptions: &client.Options{
			HostPort: fmt.Sprintf("localhost:%d", port),
		},
		EnableUI: false, // headless mode
	})
	if err != nil {
		return nil, fmt.Errorf("failed to start Temporal dev server: %w", err)
	}

	// Get the client from the dev server
	temporalClient := devServer.Client()

	// Verify connection by getting system info
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	_, err = temporalClient.WorkflowService().GetSystemInfo(ctx, &workflowservice.GetSystemInfoRequest{})
	if err != nil {
		devServer.Stop()
		return nil, fmt.Errorf("failed to connect to Temporal server: %w", err)
	}

	return &TemporalService{
		devServer: devServer,
		client:    temporalClient,
		port:      port,
	}, nil
}

// Stop gracefully stops the Temporal server and closes the client connection
func (t *TemporalService) Stop() error {
	var errors []error

	// Close client connection
	if t.client != nil {
		t.client.Close()
	}

	// Stop dev server
	if t.devServer != nil {
		if err := t.devServer.Stop(); err != nil {
			errors = append(errors, fmt.Errorf("failed to stop dev server: %w", err))
		}
	}

	if len(errors) > 0 {
		return fmt.Errorf("errors during shutdown: %v", errors)
	}

	return nil
}

// GetClient returns the Temporal client
func (t *TemporalService) GetClient() client.Client {
	return t.client
}

// GetDevServer returns the Temporal dev server
func (t *TemporalService) GetDevServer() *testsuite.DevServer {
	return t.devServer
}

// GetPort returns the port the server is running on
func (t *TemporalService) GetPort() int {
	return t.port
}

// GetFrontendHostPort returns the frontend host:port string
func (t *TemporalService) GetFrontendHostPort() string {
	if t.devServer != nil {
		return t.devServer.FrontendHostPort()
	}
	return fmt.Sprintf("localhost:%d", t.port)
}
