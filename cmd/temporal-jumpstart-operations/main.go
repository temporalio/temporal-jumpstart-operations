package main

import (
	"fmt"
	"os"

	"temporal-jumpstart-operations/commands/operations"

	"github.com/spf13/cobra"
)

var rootCmd = &cobra.Command{
	Use:   "temporal-jumpstart-operations",
	Short: "A CLI tool for temporal jumpstart operations",
	Long:  `A command-line tool built with Cobra for managing temporal jumpstart operations with service accounts and API keys.`,
}

func init() {
	// Add command groups
	rootCmd.AddCommand(operations.NewOperationsCommand())
}

func main() {
	if err := rootCmd.Execute(); err != nil {
		fmt.Fprintf(os.Stderr, "Error: %v\n", err)
		os.Exit(1)
	}
}
