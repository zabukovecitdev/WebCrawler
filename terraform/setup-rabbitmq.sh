#!/bin/bash

set -e

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo -e "${GREEN}RabbitMQ Infrastructure Setup${NC}"
echo "================================"
echo ""

# Check if Terraform is installed
if ! command -v terraform &> /dev/null; then
    echo -e "${RED}Error: Terraform is not installed${NC}"
    echo "Please install Terraform: https://www.terraform.io/downloads"
    exit 1
fi

# Check if RabbitMQ is accessible
echo -e "${YELLOW}Checking RabbitMQ connection...${NC}"
if ! curl -s -f -u guest:guest http://localhost:15672/api/overview > /dev/null 2>&1; then
    echo -e "${RED}Error: Cannot connect to RabbitMQ at http://localhost:15672${NC}"
    echo "Make sure RabbitMQ is running (e.g., docker compose up rabbitmq)"
    exit 1
fi
echo -e "${GREEN}✓ RabbitMQ is accessible${NC}"
echo ""

# Initialize Terraform if needed
if [ ! -d ".terraform" ]; then
    echo -e "${YELLOW}Initializing Terraform...${NC}"
    terraform init
    echo ""
fi

# Show what will be created
echo -e "${YELLOW}Planning changes...${NC}"
terraform plan
echo ""

# Ask for confirmation
read -p "Apply these changes? (y/N) " -n 1 -r
echo ""
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Cancelled."
    exit 0
fi

# Apply the configuration
echo -e "${YELLOW}Applying Terraform configuration...${NC}"
terraform apply

echo ""
echo -e "${GREEN}✓ RabbitMQ infrastructure setup complete!${NC}"
echo ""
echo "Created resources:"
echo "  - Exchange: cs (topic)"
echo "  - Queue: discovered.urls"
echo "  - Queue: scheduled.urls"
echo "  - Bindings with routing keys"
