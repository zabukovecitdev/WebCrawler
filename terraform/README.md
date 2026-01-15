# RabbitMQ Terraform Configuration

This Terraform configuration manages RabbitMQ exchanges, queues, and bindings for the SamoBot project.

## Prerequisites

1. **Terraform** installed (>= 1.0)
2. **RabbitMQ** running with management plugin enabled (default on port 15672)
3. RabbitMQ credentials: `guest`/`guest` (default)

## Usage

### Quick Setup (Recommended)

Simply run the setup script:

```bash
./terraform/setup-rabbitmq.sh
```

This script will:
- Check if Terraform is installed
- Verify RabbitMQ is accessible
- Initialize Terraform if needed
- Show what will be created
- Apply the configuration

### Manual Setup

1. **Initialize Terraform**:
   ```bash
   cd terraform
   terraform init
   ```

2. **Plan the changes**:
   ```bash
   terraform plan
   ```

3. **Apply the configuration**:
   ```bash
   terraform apply
   ```

### Destroy Resources

To remove all RabbitMQ resources:

```bash
./terraform/destroy-rabbitmq.sh
```

Or manually:
```bash
cd terraform
terraform destroy
```

## Resources Created

- **Exchange**: `cs` (topic exchange)
- **Queue**: `discovered.urls` (for discovered URLs)
- **Queue**: `scheduled.urls` (for scheduled URLs)
- **Bindings**: 
  - `discovered.urls` bound to `cs` with routing key `url.discovered`
  - `scheduled.urls` bound to `cs` with routing key `url.scheduled`

## Configuration

To change the RabbitMQ endpoint or credentials, modify the `provider "rabbitmq"` block in `main.tf` or use environment variables:

```bash
export RABBITMQ_ENDPOINT=http://localhost:15672
export RABBITMQ_USERNAME=guest
export RABBITMQ_PASSWORD=guest
```

## Note

When using Terraform to manage RabbitMQ resources, you should **remove** the exchange/queue declaration code from your application workers, as Terraform will handle the infrastructure setup. The application code should only consume/publish to existing queues.
