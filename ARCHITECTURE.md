# SamoBot Architecture Overview

## Current Architecture Complexity

Your SamoBot application is a distributed web crawler with **9 separate projects** and complex interdependencies:

### Projects Breakdown
```
SamoBot.sln
├── SamoBot (Main Consumer)
├── Samobot.Crawler (3 Workers)
├── SamoBot.Scheduler (Scheduling Service)
├── SamoBot.Parser (HTML Parser)
├── Samobot.Domain (Models & Enums)
├── SamoBot.Infrastructure (Shared Services)
├── SamoBot.Migrations (Database Migrations)
├── SamoBot.Seeder (Data Seeding)
├── SamoBot.Settings (Configuration)
└── SamoBot.Tests (Unit Tests)
```

### Data Flow Complexity

```
URL Discovery → RabbitMQ → SamoBot Consumer → PostgreSQL
                       ↓
               Scheduler Service → Due Queue → Crawler Worker
                       ↓
               Robots.txt Policy → Fetch Content → Parser Worker
                       ↓
               Extract Links → MinIO Storage → Redis Cache
```

### Infrastructure Dependencies
- **PostgreSQL** - URL metadata, crawl history, parsed documents
- **RabbitMQ** - Message queuing for URL discovery and scheduling
- **Redis** - Caching and rate limiting
- **MinIO** - Object storage for HTML content

## Issues Identified

1. **Over-engineered**: 9 projects for a web crawler is excessive
2. **Deployment Complexity**: Only 2 services in docker-compose, others orphaned
3. **Hard to Debug**: Distributed flow makes tracing issues difficult
4. **Maintenance Burden**: Many small projects increase complexity
5. **Missing Documentation**: Process flow is not documented

## Recommended Simplifications

### Phase 1: Consolidate Projects
Merge related functionality into fewer, more focused projects:

```
SamoBot.sln (Simplified)
├── SamoBot.Core (Domain + Infrastructure)
├── SamoBot.Crawler (All crawling logic)
├── SamoBot.Workers (Scheduler + Parser workers)
├── SamoBot.Migrations
├── SamoBot.Tests
└── SamoBot.Settings
```

### Phase 2: Simplify Data Flow
Replace complex message queues with simpler patterns:

```
Simple Flow:
URLs → Database Queue → Single Crawler Service → Process & Store

Complex Flow (Current):
URLs → RabbitMQ → Consumer → Scheduler → Queue → Crawler → Parser → Storage
```

### Phase 3: Add Visualization
Create clear documentation and monitoring dashboards.

## Specific Simplification Proposals

### 1. Merge Domain + Infrastructure
**Current:** Separate `Samobot.Domain` and `SamoBot.Infrastructure` projects
**Proposed:** Combine into `SamoBot.Core` with clear folder separation:
```
SamoBot.Core/
├── Domain/ (Models, Enums)
├── Infrastructure/ (Services, Repositories, Policies)
└── Core.csproj
```

### 2. Consolidate Workers
**Current:** Separate Scheduler, Parser, and Crawler projects
**Proposed:** Single `SamoBot.Workers` project:
```
SamoBot.Workers/
├── SchedulerWorker.cs
├── ParserWorker.cs
├── CrawlerWorker.cs
├── RobotsTxtWorker.cs
└── Workers.csproj
```

### 3. Simplify Message Flow
**Current:** RabbitMQ + complex consumer patterns
**Proposed:** Database-based queuing (simpler, more reliable):
- Replace RabbitMQ consumers with database polling
- Use PostgreSQL tables as queues
- Single crawler service handles everything

### 4. Add Process Documentation
Create `PROCESS.md` explaining the crawling workflow step-by-step.

### 5. Unified Docker Orchestration
**Current:** Incomplete docker-compose
**Proposed:** Single compose file with all services:
```yaml
services:
  crawler: # Single service handling all workers
  database: # PostgreSQL
  storage:  # MinIO (optional)
  cache:    # Redis (optional)
```

Would you like me to start implementing these simplifications? I can begin with merging the Domain and Infrastructure projects, or help with the process documentation first.