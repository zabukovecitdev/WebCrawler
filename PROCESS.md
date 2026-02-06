# SamoBot Crawling Process

## Current Process Flow (Complex)

```
1. URL DISCOVERY PHASE
   └── URLs discovered → Published to RabbitMQ (DiscoveredUrlQueue)

2. CONSUMER PHASE
   └── SamoBot Consumer → Receives messages → Stores in PostgreSQL

3. SCHEDULING PHASE
   └── SamoBot.Scheduler Worker → Checks due URLs → Publishes to ScheduledUrlQueue

4. CRAWLING PHASE
   └── Samobot.Crawler.DueQueueWorker → Polls ScheduledUrlQueue
   └── Samobot.Crawler.CrawlerWorker → Fetches content with policies:
       ├── RobotsTxtPolicy (checks robots.txt)
       └── PolitenessPolicy (rate limiting via Redis)
   └── Content stored in MinIO

5. PARSING PHASE
   └── SamoBot.Parser Worker → Parses HTML → Extracts links
   └── New URLs → Back to step 1 (RabbitMQ)

6. ROBOTS.TXT REFRESH
   └── Samobot.Crawler.RobotsTxtRefreshWorker → Updates robots.txt cache
```

## Key Issues with Current Flow

1. **Too Many Moving Parts**: 4+ separate services communicating via queues
2. **Complex Debugging**: Hard to trace URL from discovery to parsing
3. **Deployment Complexity**: Each service needs separate containers
4. **Message Loss Risk**: RabbitMQ messages can be lost on service crashes
5. **Resource Intensive**: Multiple containers, connections, and processes

## Simplified Process Proposal

```
1. URL DISCOVERY
   └── URLs → Insert into PostgreSQL queue table

2. SINGLE CRAWLER SERVICE
   └── Poll database for due URLs
   └── Check robots.txt & politeness policies
   └── Fetch content → Store in MinIO
   └── Parse HTML → Extract links → Insert back to queue
   └── Update crawl metadata in PostgreSQL
```

### Benefits of Simplified Approach

- **Single Service**: One container handles all crawling logic
- **Database Transactions**: ACID compliance for queue operations
- **Easier Debugging**: All logic in one place
- **Simpler Deployment**: Fewer containers to manage
- **Better Reliability**: No message broker to fail

## Implementation Plan

### Phase 1: Merge Projects
1. Combine `Samobot.Domain` + `SamoBot.Infrastructure` → `SamoBot.Core`
2. Combine all workers into `SamoBot.Workers`

### Phase 2: Simplify Queueing
1. Replace RabbitMQ with PostgreSQL table-based queues
2. Remove consumer/producer abstractions
3. Use database transactions for reliability

### Phase 3: Single Service Architecture
1. One `SamoBot.Crawler` service handles all operations
2. Built-in scheduler, parser, and crawler workers
3. Optional Redis/MinIO for performance/scalability

Would you like me to start implementing this simplified architecture?