package operations

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
)

// TcldAuth handles authentication by reading tcld stored credentials
type TcldAuth struct {
	configDir string
}

// NewTcldAuth creates a new tcld authentication handler
func NewTcldAuth() (*TcldAuth, error) {
	homeDir, err := os.UserHomeDir()
	if err != nil {
		return nil, fmt.Errorf("failed to get home directory: %w", err)
	}

	configDir := filepath.Join(homeDir, ".config", "tcld")
	return &TcldAuth{
		configDir: configDir,
	}, nil
}

// GetToken attempts to read token from tcld's stored credentials
func (t *TcldAuth) GetToken() (string, error) {
	// Try to read from tcld's config
	configFile := filepath.Join(t.configDir, "config.json")

	data, err := os.ReadFile(configFile)
	if err != nil {
		return "", fmt.Errorf("tcld config not found. Please run 'tcld login' first: %w", err)
	}

	var config map[string]interface{}
	if err := json.Unmarshal(data, &config); err != nil {
		return "", fmt.Errorf("failed to parse tcld config: %w", err)
	}

	// Extract token (the exact structure may vary)
	if token, ok := config["access_token"].(string); ok && token != "" {
		return token, nil
	}

	if auth, ok := config["auth"].(map[string]interface{}); ok {
		if token, ok := auth["access_token"].(string); ok && token != "" {
			return token, nil
		}
	}

	return "", fmt.Errorf("no access token found in tcld config. Please run 'tcld login' first")
}

// IsAuthenticated checks if tcld credentials are available
func (t *TcldAuth) IsAuthenticated() bool {
	_, err := t.GetToken()
	return err == nil
}
