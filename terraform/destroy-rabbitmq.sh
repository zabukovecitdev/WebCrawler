#!/bin/bash

set -e

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo -e "${RED}RabbitMQ Infrastructure Destruction${NC}"
echo "======================================"
echo ""

# Check if Terraform is installed
if ! command -v terraform &> /dev/null; then
    echo -e "${RED}Error: Terraform is not installed${NC}"
    exit 1
fi

# Show what will be destroyed
echo -e "${YELLOW}Planning destruction...${NC}"
terraform plan -destroy
echo ""

# Ask for confirmation
read -p "Destroy all RabbitMQ resources? (y/N) " -n 1 -r
echo ""
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Cancelled."
    exit 0
fi

# Destroy the resources
echo -e "${YELLOW}Destroying Terraform resources...${NC}"
terraform destroy -auto-approve

echo ""
echo -e "${GREEN}✓ RabbitMQ infrastructure destroyed${NC}"
