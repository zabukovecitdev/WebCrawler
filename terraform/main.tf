terraform {
  required_version = ">= 1.0"
  
  required_providers {
    rabbitmq = {
      source  = "cyrilgdn/rabbitmq"
      version = "~> 1.8"
    }
  }
}

provider "rabbitmq" {
  endpoint = "http://localhost:15672"
  username = "guest"
  password = "guest"
}

# Exchange for URL discovery and scheduling
resource "rabbitmq_exchange" "cs" {
  name  = "cs"
  vhost = "/"
  
  settings {
    type        = "topic"
    durable     = true
    auto_delete = false
  }
}

# Queue for discovered URLs
resource "rabbitmq_queue" "discovered_urls" {
  name  = "discovered.urls"
  vhost = "/"
  
  settings {
    durable     = true
    auto_delete = false
  }
}

# Queue binding for discovered URLs
resource "rabbitmq_binding" "discovered_urls" {
  source           = rabbitmq_exchange.cs.name
  vhost            = "/"
  destination      = rabbitmq_queue.discovered_urls.name
  destination_type = "queue"
  routing_key      = "url.discovered"
}

# Queue for scheduled URLs
resource "rabbitmq_queue" "scheduled_urls" {
  name  = "scheduled.urls"
  vhost = "/"
  
  settings {
    durable     = true
    auto_delete = false
  }
}

# Queue binding for scheduled URLs
resource "rabbitmq_binding" "scheduled_urls" {
  source           = rabbitmq_exchange.cs.name
  vhost            = "/"
  destination      = rabbitmq_queue.scheduled_urls.name
  destination_type = "queue"
  routing_key      = "url.scheduled"
}
