#!/bin/bash

# Manual testing script for Temporal gRPC Proxy
# This script helps you test the proxy manually with real Temporal server

set -e

# Configuration
PROXY_URL="http://localhost:5000"
TEMPORAL_SERVER="localhost:7233"
NAMESPACE="default"
PROTOSET="/Users/mnichols/dev/temporal-jumpstart-operations/dotnet/src/Temporal.Operations.Proxy/temporal.pb"

echo "🚀 Starting Temporal gRPC Proxy Manual Test"

# Check if proxy is running
check_proxy() {
    echo "📡 Checking if proxy is running on $PROXY_URL..."
    if curl -s -f "$PROXY_URL/health" > /dev/null 2>&1; then
        echo "✅ Proxy is running"
    else
        echo "❌ Proxy is not running. Please start it first:"
        echo "   dotnet run --project Temporal.Operations.Proxy"
        exit 1
    fi
}

# Test 1: Direct gRPC call to Temporal (without proxy)
test_direct_temporal() {
    echo "🔍 Testing direct connection to Temporal server..."
    
    # Using grpcurl to test direct connection
    if command -v grpcurl &> /dev/null; then
        echo "Testing ListNamespaces directly..."
        grpcurl -plaintext "$TEMPORAL_SERVER" \
            temporal.api.workflowservice.v1.WorkflowService/ListNamespaces \
            || echo "⚠️  Direct Temporal connection failed (expected if server not running)"
    else
        echo "⚠️  grpcurl not found. Install with: go install github.com/fullstorydev/grpcurl/cmd/grpcurl@latest"
    fi
}

# Test 2: gRPC call through proxy
test_proxy_temporal() {
    echo "🔄 Testing gRPC call through proxy..."
    
    if command -v grpcurl &> /dev/null; then
        echo "Testing ListNamespaces through proxy..."
        grpcurl -protoset "$PROTOSET" -insecure "localhost:5000" \
            temporal.api.workflowservice.v1.WorkflowService/ListNamespaces \
            || echo "⚠️  Proxy connection failed"
    fi
}

# Test 3: Start workflow with payload (this should trigger encryption)
test_workflow_with_payload() {
    echo "🎯 Testing StartWorkflowExecution with payload..."
    
    local workflow_input='{"message": "Hello from proxy test!", "data": "sensitive-information"}'
    
    if command -v grpcurl &> /dev/null; then
        echo "Starting workflow with payload through proxy..."
        grpcurl -protoset "$PROTOSET" -insecure \
            -d '{
                "namespace": "default",
                "workflow_id": "test-proxy-workflow-'$(date +%s)'",
                "workflow_type": {"name": "TestWorkflow"},
                "task_queue": {"name": "test-task-queue"},
                "input": {
                    "payloads": [{
                        "metadata": {"encoding": "anNvbi9wbGFpbg=="},
                        "data": "'$(echo -n "$workflow_input" | base64)'"
                    }]
                }
            }' \
            "localhost:5000" \
            temporal.api.workflowservice.v1.WorkflowService/StartWorkflowExecution \
            && echo "✅ Workflow started successfully through proxy"
    fi
}

# Test 4: Monitor proxy logs for encryption activity
test_encryption_logs() {
    echo "📊 Monitoring proxy logs for encryption activity..."
    echo "Start the proxy with: dotnet run --project TemporalGrpcProxy"
    echo "Then check logs for entries like:"
    echo "  - 'Transforming payload with key'"
    echo "  - 'Encrypted payload data'"
    echo "  - 'Found payload fields to transform'"
}

# Test 5: Performance test with multiple requests
test_performance() {
    echo "⚡ Running basic performance test..."
    
    local start_time=$(date +%s)
    local requests=10
    
    for i in $(seq 1 $requests); do
        echo -n "Request $i/$requests... "
        if curl -s -f "$PROXY_URL/health" > /dev/null; then
            echo "✓"
        else
            echo "✗"
        fi
    done
    
    local end_time=$(date +%s)
    local duration=$((end_time - start_time))
    
    echo "Completed $requests requests in ${duration}s"
    echo "Average: $((requests * 1000 / duration))ms per request"
}

# Test 6: Compare payload sizes (before/after encryption)
test_payload_size_comparison() {
    echo "📏 Testing payload size changes..."
    
    # Create test payload
    local test_payload='{"test": "data", "sensitive": "information"}'
    local original_size=${#test_payload}
    
    echo "Original payload size: $original_size bytes"
    echo "Note: Check proxy logs to see encrypted payload sizes"
    echo "Expected: Encrypted payloads should be larger due to encryption + metadata"
}

# Test 7: Verify key rotation capability
test_key_rotation() {
    echo "🔑 Testing key rotation readiness..."
    
    echo "To test key rotation:"
    echo "1. Start multiple workflows with different payloads"
    echo "2. Each should get a unique key ID"
    echo "3. Check proxy logs for different key IDs being used"
    echo "4. Verify old keys can still decrypt existing data"
}

# Main test execution
main() {
    echo "================================================"
    echo "     Temporal gRPC Proxy Manual Test Suite"
    echo "================================================"
    
    # check_proxy
    # echo ""
    
    test_direct_temporal
    echo ""
    
    test_proxy_temporal
    echo ""
    
    test_workflow_with_payload
    echo ""
    
    test_encryption_logs
    echo ""
    
    test_performance
    echo ""
    
    test_payload_size_comparison
    echo ""
    
    test_key_rotation
    echo ""
    
    echo "================================================"
    echo "✅ Manual testing completed!"
    echo ""
    echo "Next steps:"
    echo "1. Check proxy logs for encryption/decryption activity"
    echo "2. Monitor Temporal server logs to verify encrypted data"
    echo "3. Test with real Temporal workflows"
    echo "4. Verify key-id storage and retrieval"
    echo "================================================"
}

# Helper functions
install_dependencies() {
    echo "📦 Installing test dependencies..."
    
    # Install grpcurl if not present
    if ! command -v grpcurl &> /dev/null; then
        echo "Installing grpcurl..."
        if command -v go &> /dev/null; then
            go install github.com/fullstorydev/grpcurl/cmd/grpcurl@latest
        else
            echo "Please install Go first, then run: go install github.com/fullstorydev/grpcurl/cmd/grpcurl@latest"
        fi
    fi
    
    # Install temporal CLI if not present
    if ! command -v temporal &> /dev/null; then
        echo "Consider installing Temporal CLI:"
        echo "curl -sSf https://temporal.download/cli.sh | sh"
    fi
}

# Command line argument handling
case "${1:-}" in
    "install-deps")
        install_dependencies
        ;;
    "proxy-only")
        check_proxy
        test_proxy_temporal
        ;;
    "performance")
        check_proxy
        test_performance
        ;;
    *)
        main
        ;;
esac