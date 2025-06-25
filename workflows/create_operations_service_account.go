package workflows

import (
	"temporal-jumpstart-operations/workflows/activities"

	"go.temporal.io/sdk/temporal"
	"go.temporal.io/sdk/workflow"
)

type CreateOperationsServiceAccountState struct {
	Args           *CreateServiceAccountRequest
	ServiceAccount *activities.CreateServiceAccountResponse
}

// CreateServiceAccountRequest represents the parameters for creating a service account
type CreateServiceAccountRequest struct {
	// OutputPath is the path where configuration files will be generated (required)
	OutputPath string `json:"outputPath"`

	// ServiceAccountName is the name of the service account to create (required)
	ServiceAccountName string `json:"serviceAccountName"`

	// APIKeyName is the name of the API key to create (optional, defaults to {service_account_name}_key)
	APIKeyName string `json:"apiKeyName"`

	// Duration is the duration for the API key (optional, defaults to '1y')
	Duration string `json:"duration"`
}

// CreateOperationsServiceAccount is a Temporal workflow that creates a service account
// and associated API key in Temporal Cloud for jumpstart operations
func CreateOperationsServiceAccount(ctx workflow.Context, args *CreateServiceAccountRequest) error {
	state := &CreateOperationsServiceAccountState{
		Args: args,
	}

	// Set default values if not provided
	if args.APIKeyName == "" {
		args.APIKeyName = args.ServiceAccountName + "_key"
	}
	if args.Duration == "" {
		args.Duration = "1y"
	}

	// Validate required fields
	if args.OutputPath == "" {
		return temporal.NewNonRetryableApplicationError("outputPath is required", "ValidationError", nil)
	}
	if args.ServiceAccountName == "" {
		return temporal.NewNonRetryableApplicationError("serviceAccountName is required", "ValidationError", nil)
	}

	// TODO: Implement workflow logic
	// This workflow should orchestrate the following activities:
	// 1. Create service account in Temporal Cloud
	// 2. Create API key for the service account
	// 3. Initialize local TemporalService
	// 4. Generate configuration files
	// 5. Save configuration to output path

	workflow.GetLogger(ctx).Info("CreateOperationsServiceAccount workflow started",
		"outputPath", args.OutputPath,
		"serviceAccountName", args.ServiceAccountName,
		"apiKeyName", args.APIKeyName,
		"duration", args.Duration,
	)

	// TODO: Execute activities to create service account
	if err := workflow.ExecuteActivity(ctx, activities.TypeActivities.CreateServiceAccount, &activities.CreateServiceAccountRequest{
		Name:        args.ServiceAccountName,
		Description: "Service account for operations",
	}).Get(ctx, &state.ServiceAccount); err != nil {
		return err
	}

	if err := workflow.ExecuteActivity(ctx, activities.TypeActivities.CreateAPIKey, &activities.CreateAPIKeyRequest{
		ServiceAccountId: state.ServiceAccount.ServiceAccountId,
		Name:             args.APIKeyName,
		Duration:         args.Duration,
		OutputPath:       args.OutputPath,
	}).Get(ctx, &state.APIKey); err != nil {
		return err
	}

	// Placeholder for workflow implementation
	workflow.GetLogger(ctx).Info("CreateOperationsServiceAccount workflow completed successfully")

	return nil
}
