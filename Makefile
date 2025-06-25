# Makefile for building Temporal API FileDescriptorSet using buf
# Uses Method 1: Build directly from BSR with latest version

# Variables
OUTPUT_DIR := ./generated
TEMPORAL_API_MODULE := buf.build/temporalio/api
BINARY_OUTPUT := $(OUTPUT_DIR)/temporal-api.binpb
JSON_OUTPUT := $(OUTPUT_DIR)/temporal-api.json

# Default target
.PHONY: all
all: build

# Create output directory
$(OUTPUT_DIR):
	@mkdir -p $(OUTPUT_DIR)

# Build Temporal API as FileDescriptorSet (binary format)
.PHONY: build
build: $(OUTPUT_DIR)
	@echo "Building Temporal API FileDescriptorSet from $(TEMPORAL_API_MODULE)..."
	buf build $(TEMPORAL_API_MODULE) \
		-o $(BINARY_OUTPUT) \
		--as-file-descriptor-set
	@echo "✓ Temporal API FileDescriptorSet built: $(BINARY_OUTPUT)"

# Build as JSON format for inspection
.PHONY: build-json
build-json: $(OUTPUT_DIR)
	@echo "Building Temporal API as JSON from $(TEMPORAL_API_MODULE)..."
	buf build $(TEMPORAL_API_MODULE) \
		-o $(JSON_OUTPUT)#format=json \
		--as-file-descriptor-set
	@echo "✓ Temporal API JSON built: $(JSON_OUTPUT)"

# Build both binary and JSON formats
.PHONY: build-all
build-all: build build-json

# Verify the build by checking file size and contents
.PHONY: verify
verify: build
	@echo "Verifying built FileDescriptorSet..."
	@if [ -f "$(BINARY_OUTPUT)" ]; then \
		echo "✓ File exists: $(BINARY_OUTPUT)"; \
		echo "  Size: $$(du -h $(BINARY_OUTPUT) | cut -f1)"; \
		echo "  Type: $$(file $(BINARY_OUTPUT))"; \
	else \
		echo "✗ File not found: $(BINARY_OUTPUT)"; \
		exit 1; \
	fi

# Clean generated files
.PHONY: clean
clean:
	@echo "Cleaning generated files..."
	@rm -rf $(OUTPUT_DIR)
	@echo "✓ Cleaned"

# Show help
.PHONY: help
help:
	@echo "Temporal API FileDescriptorSet Builder"
	@echo ""
	@echo "Available targets:"
	@echo "  build       - Build Temporal API as binary FileDescriptorSet (default)"
	@echo "  build-json  - Build Temporal API as JSON FileDescriptorSet"
	@echo "  build-all   - Build both binary and JSON formats"
	@echo "  verify      - Verify the built FileDescriptorSet"
	@echo "  clean       - Remove generated files"
	@echo "  help        - Show this help message"
	@echo ""
	@echo "Output files:"
	@echo "  $(BINARY_OUTPUT)"
	@echo "  $(JSON_OUTPUT)"

# Check if buf is installed
.PHONY: check-buf
check-buf:
	@which buf > /dev/null || (echo "Error: buf CLI not found. Install with: brew install bufbuild/buf/buf" && exit 1)
	@echo "✓ buf CLI found: $$(buf --version)"

# Build with dependency check
.PHONY: build-safe
build-safe: check-buf build

# Print build info
.PHONY: info
info:
	@echo "Build Configuration:"
	@echo "  Module: $(TEMPORAL_API_MODULE)"
	@echo "  Output Dir: $(OUTPUT_DIR)"
	@echo "  Binary Output: $(BINARY_OUTPUT)"
	@echo "  JSON Output: $(JSON_OUTPUT)"
