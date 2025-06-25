package activities

import (
	"context"
	"fmt"
	"slices"
	"strings"

	"time"

	cloudservicev1 "go.temporal.io/cloud-sdk/api/cloudservice/v1"
	identityv1 "go.temporal.io/cloud-sdk/api/identity/v1"
	operationv1 "go.temporal.io/cloud-sdk/api/operation/v1"
	"go.temporal.io/sdk/temporal"
	"google.golang.org/protobuf/types/known/timestamppb"
)

var TypeActivities *Activities

const ERR_ALREADY_EXISTS = "already exists"
const ERR_OPERATION_NOT_READY = "operation not ready"
const ERR_OPERATION_WILL_NOT_SUCCEED = "operation will not succeed"

type CreateServiceAccountRequest struct {
	Name             string `json:"name"`
	Description      string `json:"description"`
	AsyncOperationId string `json:"asyncOperationId"`
}
type CreateServiceAccountResponse struct {
	ServiceAccountId string `json:"serviceAccountId"`
}
type CreateAPIKeyRequest struct {
	ServiceAccountId string `json:"serviceAccountId"`
	Name             string `json:"name"`
	Description      string `json:"description"`
	AsyncOperationId string `json:"asyncOperationId"`
	Duration         string `json:"duration"`
}
type CreateAPIKeyResponse struct {
	ServiceAccountId string `json:"serviceAccountId"`
	ApiKeyId         string `json:"apiKeyId"`
}
type WriteApiKeyRequest struct {
	ApiKeyId         string `json:"apiKeyId"`
	ServiceAccountId string `json:"serviceAccountId"`
	OutputPath       string `json:"outputPath"`
}
type CheckOperationCompletionRequest struct {
	AsyncOperationId string `json:"asyncOperationId"`
}
type CheckOperationCompletionResponse struct {
	AsyncOperationId string `json:"operationId"`
	State            string `json:"status"`
}

// Activities contains all the activity implementations for the operations workflows
type Activities struct {
	// CloudClient is the Temporal Cloud service client for making API calls
	CloudClient cloudservicev1.CloudServiceClient
}

// NewActivities creates a new Activities instance with the provided cloud client
func NewActivities(cloudClient cloudservicev1.CloudServiceClient) *Activities {
	return &Activities{
		CloudClient: cloudClient,
	}
}

func (a *Activities) CreateServiceAccount(ctx context.Context, args *CreateServiceAccountRequest) (*CreateServiceAccountResponse, error) {
	sas, err := a.CloudClient.GetServiceAccounts(ctx, &cloudservicev1.GetServiceAccountsRequest{
		PageSize: 10000,
	})
	if err != nil {
		return nil, err
	}
	if sas != nil && len(sas.ServiceAccount) > 0 {
		return nil, temporal.NewNonRetryableApplicationError(ERR_ALREADY_EXISTS, "already exists", nil)
	}
	if slices.IndexFunc(sas.ServiceAccount, func(sa *identityv1.ServiceAccount) bool {
		return strings.EqualFold(sa.Spec.Name, args.Name)
	}) != -1 {
		return nil, temporal.NewNonRetryableApplicationError(ERR_ALREADY_EXISTS, "already exists", nil)
	}

	sa, err := a.CloudClient.CreateServiceAccount(ctx, &cloudservicev1.CreateServiceAccountRequest{
		Spec: &identityv1.ServiceAccountSpec{
			Name:        args.Name,
			Description: args.Description,
		},
		AsyncOperationId: args.AsyncOperationId,
	})
	if err != nil {
		return nil, err
	}

	return &CreateServiceAccountResponse{
		ServiceAccountId: sa.ServiceAccountId,
	}, nil
}
func (a *Activities) CreateAPIKey(ctx context.Context, args *CreateAPIKeyRequest) (*CreateAPIKeyResponse, error) {
	// we could filter this list by OwnerId to support duplicate ApiKey names (disambiguated by the ownerId)
	// but instead this is enforcing the global uniqueness of the ApiKey name
	keys, err := a.CloudClient.GetApiKeys(ctx, &cloudservicev1.GetApiKeysRequest{
		PageSize:  10000,
		OwnerType: identityv1.OwnerType_OWNER_TYPE_SERVICE_ACCOUNT,
	})
	if err != nil {
		return nil, err
	}
	if slices.IndexFunc(keys.ApiKeys, func(key *identityv1.ApiKey) bool {
		return strings.EqualFold(key.Spec.DisplayName, args.Name)
	}) != -1 {
		return nil, temporal.NewNonRetryableApplicationError(ERR_ALREADY_EXISTS, "already exists", nil)
	}

	xp, err := ParseDuration(args.Duration)
	if err != nil {
		return nil, err
	}
	ak, err := a.CloudClient.CreateApiKey(ctx, &cloudservicev1.CreateApiKeyRequest{
		Spec: &identityv1.ApiKeySpec{
			OwnerId:     args.ServiceAccountId,
			ExpiryTime:  timestamppb.New(time.Now().Add(xp)),
			DisplayName: args.Name,
			Description: args.Description,
		},
		AsyncOperationId: args.AsyncOperationId,
	})
	if err != nil {
		return nil, err
	}

	return &CreateAPIKeyResponse{
		ApiKeyId:         ak.KeyId,
		ServiceAccountId: args.ServiceAccountId,
	}, nil
}

func (a *Activities) WriteApiKey(ctx context.Context, args *WriteApiKeyRequest) error {
	return nil
}

func (a *Activities) CheckOperationCompletion(ctx context.Context, args *CheckOperationCompletionRequest) (*CheckOperationCompletionResponse, error) {
	op, err := a.CloudClient.GetAsyncOperation(ctx, &cloudservicev1.GetAsyncOperationRequest{
		AsyncOperationId: args.AsyncOperationId,
	})
	if err != nil {
		return nil, temporal.NewNonRetryableApplicationError(err.Error(), "failed to get async operation", err)
	}
	if op.AsyncOperation.State == operationv1.AsyncOperation_STATE_PENDING ||
		op.AsyncOperation.State == operationv1.AsyncOperation_STATE_IN_PROGRESS ||
		op.AsyncOperation.State == operationv1.AsyncOperation_STATE_UNSPECIFIED {
		return nil, fmt.Errorf("%s:%s", ERR_OPERATION_NOT_READY, op.AsyncOperation.State.String())
	}

	if op.AsyncOperation.State == operationv1.AsyncOperation_STATE_FULFILLED {
		return &CheckOperationCompletionResponse{
			AsyncOperationId: args.AsyncOperationId,
			State:            op.AsyncOperation.State.String(),
		}, nil
	}
	return nil, temporal.NewNonRetryableApplicationError(ERR_OPERATION_WILL_NOT_SUCCEED, "operation will not succeed: "+op.AsyncOperation.State.String(), nil)

}
