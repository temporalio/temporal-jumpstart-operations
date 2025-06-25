package operations

import "github.com/spf13/cobra"

// NewOperationsCommand creates and returns the operations command with its subcommands
func NewOperationsCommand() *cobra.Command {
	cmd := &cobra.Command{
		Use:   "operations",
		Short: "Operations related commands",
		Long:  `Commands for managing temporal jumpstart operations.`,
	}

	// Add subcommands
	cmd.AddCommand(NewServiceAccountCommand())

	return cmd
}
