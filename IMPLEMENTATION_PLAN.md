# SamoBot Simplification Implementation Plan

## Current State Analysis
- **9 projects** with complex interdependencies
- **Distributed architecture** with RabbitMQ, Redis, PostgreSQL, MinIO
- **Incomplete orchestration** (only 2 services in docker-compose)
- **Hard to understand** process flow

## Phase 1: Project Consolidation (Week 1-2)

### Step 1.1: Merge Domain + Infrastructure
```bash
# Create new SamoBot.Core project
mkdir src/SamoBot.Core
mv src/Samobot.Domain/* src/SamoBot.Core/
mv src/SamoBot.Infrastructure/* src/SamoBot.Core/
```

**Files to merge:**
- `Samobot.Domain.csproj` + `SamoBot.Infrastructure.csproj` → `SamoBot.Core.csproj`
- Update all project references across solution

### Step 1.2: Consolidate Workers
```bash
# Create unified workers project
mkdir src/SamoBot.Workers
mv src/SamoBot.Scheduler/* src/SamoBot.Workers/
mv src/SamoBot.Parser/* src/SamoBot.Workers/
mv src/Samobot.Crawler/* src/SamoBot.Workers/
```

**Benefits:**
- Fewer projects to maintain
- Clearer separation of concerns
- Easier debugging

## Phase 2: Simplify Data Flow (Week 3-4)

### Step 2.1: Replace RabbitMQ with Database Queues
**Current:** Message broker with consumers/producers
**Proposed:** PostgreSQL table-based queuing

```sql
-- New queue table
CREATE TABLE url_queue (
    id SERIAL PRIMARY KEY,
    url TEXT NOT NULL,
    status VARCHAR(20) DEFAULT 'pending',
    priority INTEGER DEFAULT 0,
    created_at TIMESTAMP DEFAULT NOW(),
    scheduled_at TIMESTAMP DEFAULT NOW()
);
```

### Step 2.2: Single Crawler Service
**Current:** 4 separate services (Consumer, Scheduler, Crawler, Parser)
**Proposed:** One unified crawler with internal workers

```csharp
// Single Program.cs
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddCore(builder.Configuration);

// Add all workers to single service
builder.Services.AddHostedService<QueueProcessorWorker>();
builder.Services.AddHostedService<CrawlerWorker>();
builder.Services.AddHostedService<ParserWorker>();
builder.Services.AddHostedService<RobotsTxtWorker>();
```

## Phase 3: Testing & Migration (Week 5-6)

### Step 3.1: Update Tests
- Move tests to consolidated project structure
- Update namespace references
- Ensure all functionality still works

### Step 3.2: Migration Scripts
- Database migration for queue table
- Configuration migration
- Backward compatibility layer (optional)

## Phase 4: Documentation & Monitoring (Week 7-8)

### Step 4.1: Process Documentation
- Create `docs/` folder with architecture diagrams
- API documentation for public interfaces
- Troubleshooting guides

### Step 4.2: Observability
- Add health checks
- Structured logging
- Performance metrics

## Success Metrics

### Before Simplification
- 9 projects, complex deployment
- Hard to debug issues
- Resource intensive (4+ containers)

### After Simplification
- 5 projects, simple deployment
- Single crawler container
- Clear process flow
- Easier maintenance

## Risk Mitigation

1. **Incremental Changes**: Each phase can be rolled back
2. **Backward Compatibility**: Keep legacy services during transition
3. **Comprehensive Testing**: Full test coverage before deployment
4. **Staged Rollout**: Deploy simplified version alongside legacy

## Quick Wins (Can start immediately)

1. **Fix Docker Compose**: Add missing services to orchestration
2. **Add Process Docs**: Document current flow before changing it
3. **Consolidate Small Projects**: Merge Settings into Infrastructure

Would you like me to start with any of these phases? I recommend beginning with Phase 1 (project consolidation) as it provides immediate benefits with manageable risk.