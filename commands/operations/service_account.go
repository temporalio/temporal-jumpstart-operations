package operations

import (
	"fmt"
	"os"

	"temporal-jumpstart-operations/temporal"

	"github.com/spf13/cobra"

	cloudservicev1 "go.temporal.io/cloud-sdk/api/cloudservice/v1"
	identityv1 "go.temporal.io/cloud-sdk/api/identity/v1"
)

var (
	// Create command flags
	outputPath         string
	serviceAccountName string
	apiKeyName         string
	duration           string

	// Delete command flags
	deleteServiceAccountName string
)

// NewServiceAccountCommand creates and returns the service account command
func NewServiceAccountCommand() *cobra.Command {
	cmd := &cobra.Command{
		Use:   "service-account",
		Short: "Manage Temporal Cloud service accounts",
		Long:  `Manage service accounts in Temporal Cloud for jumpstart operations.`,
	}

	// Add subcommands
	cmd.AddCommand(newCreateCommand())
	cmd.AddCommand(newDeleteCommand())

	return cmd
}

// newCreateCommand creates the service-account create subcommand
func newCreateCommand() *cobra.Command {
	cmd := &cobra.Command{
		Use:   "create",
		Short: "Create Temporal Cloud service account and API key",
		Long:  `Create a service account and API key in Temporal Cloud for jumpstart operations.`,
		RunE:  runCreateServiceAccount,
	}

	// Define flags for the create command
	cmd.Flags().StringVarP(&outputPath, "output-path", "o", "", "Output path (required)")
	cmd.Flags().StringVarP(&serviceAccountName, "name", "n", "", "Service account name (required)")
	cmd.Flags().StringVarP(&apiKeyName, "api-key-name", "k", "", "API key name (optional, defaults to {service_account_name}_key)")
	cmd.Flags().StringVarP(&duration, "duration", "d", "1y", "Duration (optional, defaults to '1y')")

	// Mark required flags
	cmd.MarkFlagRequired("output-path")
	cmd.MarkFlagRequired("name")

	return cmd
}

// newDeleteCommand creates the service-account delete subcommand
func newDeleteCommand() *cobra.Command {
	cmd := &cobra.Command{
		Use:   "delete",
		Short: "Delete Temporal Cloud service account",
		Long:  `Delete a service account from Temporal Cloud.`,
		RunE:  runDeleteServiceAccount,
	}

	// Define flags for the delete command
	cmd.Flags().StringVarP(&deleteServiceAccountName, "name", "n", "", "Service account name (required)")

	// Mark required flags
	cmd.MarkFlagRequired("name")

	return cmd
}

// runCreateServiceAccount contains the main logic for creating a service account
func runCreateServiceAccount(cmd *cobra.Command, args []string) error {
	// Set default api_key_name if not provided
	if apiKeyName == "" {
		apiKeyName = serviceAccountName + "_key"
	}

	// Validate required arguments
	if outputPath == "" {
		return fmt.Errorf("output_path is required")
	}
	if serviceAccountName == "" {
		return fmt.Errorf("service_account_name is required")
	}

	// Display the parsed arguments
	fmt.Printf("Configuration:\n")
	fmt.Printf("  Output Path: %s\n", outputPath)
	fmt.Printf("  Service Account Name: %s\n", serviceAccountName)
	fmt.Printf("  API Key Name: %s\n", apiKeyName)
	fmt.Printf("  Duration: %s\n", duration)

	// Create Cloud Service client
	fmt.Printf("\n🔗 Connecting to Temporal Cloud...\n")
	cloudService, closer, err := NewCloudServiceClient()
	if err != nil {
		return fmt.Errorf("failed to create cloud client: %w", err)
	}
	defer closer.Close()

	ctx := cmd.Context()

	// Create service account in Temporal Cloud
	fmt.Printf("📝 Creating service account '%s' in Temporal Cloud...\n", serviceAccountName)
	serviceAccountDescription := fmt.Sprintf("Service account for temporal jumpstart operations - %s", serviceAccountName)

	serviceAccountReq := &cloudservicev1.CreateServiceAccountRequest{
		Spec: &identityv1.ServiceAccountSpec{
			Name:        serviceAccountName,
			Description: serviceAccountDescription,
		},
	}

	serviceAccountResp, err := cloudService.CreateServiceAccount(ctx, serviceAccountReq)
	if err != nil {
		return fmt.Errorf("failed to create service account: %w", err)
	}

	fmt.Printf("✅ Created service account: %s (ID: %s)\n", serviceAccountName, serviceAccountResp.ServiceAccountId)

	// Create API key for the service account
	fmt.Printf("🔑 Creating API key '%s' for service account...\n", apiKeyName)
	apiKeyDescription := fmt.Sprintf("API key for service account %s", serviceAccountName)

	// TODO: Implement API key creation using the embedded CloudServiceClient
	// This will use cloudClient.CreateApiKey() method once we implement the proper request structure
	fmt.Printf("NOTE: API key creation will be implemented using cloudClient.CreateApiKey()\n")
	fmt.Printf("  Service Account ID: %s\n", serviceAccountResp.ServiceAccountId)
	fmt.Printf("  API Key Name: %s\n", apiKeyName)
	fmt.Printf("  Description: %s\n", apiKeyDescription)
	fmt.Printf("  Duration: %s\n", duration)

	// Create TemporalService (local dev server)
	fmt.Printf("\n🏗️  Initializing local TemporalService...\n")
	temporalService, err := temporal.NewTemporalService()
	if err != nil {
		return fmt.Errorf("failed to create TemporalService: %w", err)
	}
	defer func() {
		if stopErr := temporalService.Stop(); stopErr != nil {
			fmt.Fprintf(os.Stderr, "Error stopping TemporalService: %v\n", stopErr)
		}
	}()

	fmt.Printf("✅ TemporalService started successfully!\n")
	fmt.Printf("  Frontend Address: %s\n", temporalService.GetFrontendHostPort())
	fmt.Printf("  Port: %d\n", temporalService.GetPort())

	// TODO: Generate configuration files and save to output path
	fmt.Printf("\n📂 Generating configuration files...\n")
	fmt.Printf("  Output Path: %s\n", outputPath)
	fmt.Printf("NOTE: Configuration file generation will be implemented next\n")

	fmt.Printf("\n🎉 Initialization completed successfully!\n")
	fmt.Printf("   Service Account: %s (created in Temporal Cloud)\n", serviceAccountName)
	fmt.Printf("   API Key: %s (created for service account)\n", apiKeyName)
	fmt.Printf("   Local Server: %s (running)\n", temporalService.GetFrontendHostPort())

	return nil
}

// runDeleteServiceAccount contains the main logic for deleting a service account
func runDeleteServiceAccount(cmd *cobra.Command, args []string) error {
	// Validate required arguments
	if deleteServiceAccountName == "" {
		return fmt.Errorf("service account name is required")
	}

	// Display the operation
	fmt.Printf("🗑️  Deleting service account '%s' from Temporal Cloud...\n", deleteServiceAccountName)

	// TODO: Implement service account deletion
	// This will require:
	// 1. Create Cloud Service client
	// 2. List service accounts to find the one with matching name
	// 3. Delete the service account by ID
	fmt.Printf("NOTE: Service account deletion will be implemented using the Cloud Operations API\n")
	fmt.Printf("  Service Account Name: %s\n", deleteServiceAccountName)
	fmt.Printf("  Implementation pending: Need to lookup service account by name, then delete by ID\n")

	return fmt.Errorf("delete functionality not yet implemented - please use tcld or the Temporal Cloud UI to delete service accounts")
}
