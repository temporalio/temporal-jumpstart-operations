package operations

import (
	"fmt"
	"io"

	cloudservicev1 "go.temporal.io/cloud-sdk/api/cloudservice/v1"
	"go.temporal.io/cloud-sdk/cloudclient"
)

// NewCloudServiceClient creates a CloudService client using tcld credentials
func NewCloudServiceClient() (cloudservicev1.CloudServiceClient, io.Closer, error) {
	// Get token from tcld credentials
	tcldAuth, err := NewTcldAuth()
	if err != nil {
		return nil, nil, fmt.Errorf("failed to create tcld auth handler: %w", err)
	}

	token, err := tcldAuth.GetToken()
	if err != nil {
		return nil, nil, fmt.Errorf("no tcld credentials found. Please run 'tcld login' first: %w", err)
	}

	// Create client using the official Cloud SDK
	client, err := cloudclient.New(cloudclient.Options{
		APIKey: token,
	})
	if err != nil {
		return nil, nil, fmt.Errorf("failed to create cloud client: %w", err)
	}

	return client.CloudService(), client, nil
}
