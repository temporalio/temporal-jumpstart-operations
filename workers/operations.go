package workers

import (
	"fmt"

	"temporal-jumpstart-operations/commands/operations"
	"temporal-jumpstart-operations/workflows"
	"temporal-jumpstart-operations/workflows/activities"

	cloudservicev1 "go.temporal.io/cloud-sdk/api/cloudservice/v1"
	"go.temporal.io/sdk/client"
	"go.temporal.io/sdk/worker"
)

// OperationsWorker manages the operations task queue worker
type OperationsWorker struct {
	// temporalClient is the Temporal SDK client
	temporalClient client.Client
	// cloudClient is the Temporal Cloud service client
	cloudClient cloudservicev1.CloudServiceClient
	// worker is the Temporal SDK worker instance
	worker worker.Worker
	// closer is used to close the cloud client connection
	closer func() error
}

// NewOperationsWorker creates a new operations worker with the provided Temporal client
func NewOperationsWorker(temporalClient client.Client) (*OperationsWorker, error) {
	// Create cloud client for activities
	cloudClient, closer, err := operations.NewCloudServiceClient()
	if err != nil {
		return nil, fmt.Errorf("failed to create cloud client: %w", err)
	}

	// Create worker on the "operations" task queue
	w := worker.New(temporalClient, "operations", worker.Options{})

	// Register the CreateOperationsServiceAccount workflow
	w.RegisterWorkflow(workflows.CreateOperationsServiceAccount)

	// Create activities instance using the factory method
	activitiesInstance := activities.NewActivities(cloudClient)

	// Register activities
	w.RegisterActivity(activitiesInstance)

	return &OperationsWorker{
		temporalClient: temporalClient,
		cloudClient:    cloudClient,
		worker:         w,
		closer: func() error {
			if closerInterface, ok := closer.(interface{ Close() error }); ok {
				return closerInterface.Close()
			}
			return nil
		},
	}, nil
}

// Start starts the operations worker
func (ow *OperationsWorker) Start() error {
	if ow.worker == nil {
		return fmt.Errorf("worker not initialized")
	}

	// Start the worker (this is blocking)
	return ow.worker.Run(worker.InterruptCh())
}

// Stop stops the operations worker and cleans up resources
func (ow *OperationsWorker) Stop() error {
	var errors []error

	// Stop the worker
	if ow.worker != nil {
		ow.worker.Stop()
	}

	// Close cloud client connection
	if ow.closer != nil {
		if err := ow.closer(); err != nil {
			errors = append(errors, fmt.Errorf("failed to close cloud client: %w", err))
		}
	}

	if len(errors) > 0 {
		return fmt.Errorf("errors during worker shutdown: %v", errors)
	}

	return nil
}
